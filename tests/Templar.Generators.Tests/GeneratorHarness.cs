using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Templar.Rendering;
using Xunit;

namespace Templar.Generators.Tests;

internal static class GeneratorHarness
{
    public const string ProjectDir = "/proj/";

    private sealed class InMemoryText : AdditionalText
    {
        private readonly string _text;

        public InMemoryText(string path, string text)
        {
            Path = path;
            _text = text;
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default)
        {
            return SourceText.From(_text, Encoding.UTF8);
        }
    }

    private sealed class Options : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _values;

        public Options(Dictionary<string, string> values)
        {
            _values = values;
        }

        public override bool TryGetValue(string key, out string value)
        {
            return _values.TryGetValue(key, out value!);
        }
    }

    private sealed class Provider : AnalyzerConfigOptionsProvider
    {
        private readonly AnalyzerConfigOptions _global;

        public Provider(AnalyzerConfigOptions global)
        {
            _global = global;
        }

        public override AnalyzerConfigOptions GlobalOptions => _global;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _global;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _global;
    }

    public sealed record Run(IReadOnlyDictionary<string, string> Sources,
                             ImmutableArray<Diagnostic> Diagnostics,
                             Compilation Output,
                             GeneratorDriver Driver);

    public static readonly CSharpParseOptions ParseOptions = new CSharpParseOptions(LanguageVersion.Latest)
        .WithFeatures(new[] { new KeyValuePair<string, string>("InterceptorsNamespaces", "Templar.Generated") });

    public static Run Generate(string[] sources,
                               (string Path, string Text)[] templates,
                               string rootNamespace = "App",
                               string projectDir = ProjectDir)
    {
        var compilation = Compile(sources);
        var driver = CreateDriver(templates, rootNamespace, projectDir);
        return Advance(driver, compilation);
    }

    public static CSharpCompilation Compile(string[] sources, params MetadataReference[] extra)
    {
        var trees = sources.Select(s => CSharpSyntaxTree.ParseText(s, ParseOptions));
        return CSharpCompilation.Create("Generated" + Guid.NewGuid().ToString("N"),
                                        trees,
                                        References.Value.AddRange(extra),
                                        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                                                                     nullableContextOptions: NullableContextOptions.Enable));
    }

    public static GeneratorDriver CreateDriver((string Path, string Text)[] templates,
                                               string rootNamespace = "App",
                                               string projectDir = ProjectDir)
    {
        var texts = templates.Select(f => (AdditionalText)new InMemoryText(f.Path, f.Text)).ToImmutableArray();
        var values = new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = rootNamespace,
            ["build_property.ProjectDir"] = projectDir,
        };
        return CSharpGeneratorDriver.Create(generators: new[] { new RendererGenerator().AsSourceGenerator() },
                                            additionalTexts: texts,
                                            parseOptions: ParseOptions,
                                            optionsProvider: new Provider(new Options(values)),
                                            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None,
                                                                                      trackIncrementalGeneratorSteps: true));
    }

    public static Run Advance(GeneratorDriver driver, Compilation compilation)
    {
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        var result = driver.GetRunResult();

        var failed = result.Results
            .Where(r => r.Exception is not null)
            .Select(r => $"{r.Generator.GetGeneratorType().Name} threw: {r.Exception}")
            .ToList();
        if (failed.Count > 0)
        {
            throw new InvalidOperationException(string.Join("\n", failed));
        }

        var map = new Dictionary<string, string>();
        foreach (var generatorResult in result.Results)
        {
            foreach (var source in generatorResult.GeneratedSources)
            {
                map[source.HintName] = source.SourceText.ToString();
            }
        }
        return new Run(map, diagnostics, output, driver);
    }

    public static Assembly Load(Run run)
    {
        Assert.Empty(run.Diagnostics);
        var problems = run.Output.GetDiagnostics()
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .Select(d => d.ToString())
            .ToList();
        Assert.True(problems.Count == 0, string.Join("\n", problems));

        return Emit(run.Output);
    }

    public static Assembly Emit(Compilation compilation)
    {
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(emit.Success, string.Join("\n", emit.Diagnostics.Select(d => d.ToString())));
        return Assembly.Load(stream.ToArray());
    }

    public static Assembly[] LoadTogether(params Compilation[] compilations)
    {
        var context = new AssemblyLoadContext("Together" + Guid.NewGuid().ToString("N"));
        var loaded = new List<Assembly>();
        context.Resolving += (_, name) => loaded.FirstOrDefault(a => a.GetName().Name == name.Name);
        foreach (var compilation in compilations)
        {
            using var stream = new MemoryStream();
            var emit = compilation.Emit(stream);
            Assert.True(emit.Success, string.Join("\n", emit.Diagnostics.Select(d => d.ToString())));
            stream.Position = 0;
            loaded.Add(context.LoadFromStream(stream));
        }
        return loaded.ToArray();
    }

    public static string Probe(Assembly assembly, string method = "Run")
    {
        var probe = assembly.GetType("App.Probe")!;
        try
        {
            return (string)probe.GetMethod(method)!.Invoke(null, null)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    private static readonly Lazy<ImmutableArray<MetadataReference>> References = new(BuildReferences);

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var references = ImmutableArray.CreateBuilder<MetadataReference>();
        var trusted = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "")
            .Split(Path.PathSeparator)
            .Where(p => p.Length > 0 && p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
        foreach (var path in trusted)
        {
            references.Add(MetadataReference.CreateFromFile(path));
        }
        references.Add(MetadataReference.CreateFromFile(typeof(Compositor).Assembly.Location));
        return references.ToImmutable();
    }
}
