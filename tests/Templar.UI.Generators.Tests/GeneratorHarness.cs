using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Templar.UI.Generators.Tests;

internal static class GeneratorHarness
{
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

    public static IReadOnlyDictionary<string, string> Run(IIncrementalGenerator generator,
                                                          (string Path, string Text)[] files,
                                                          Dictionary<string, string>? globalOptions = null)
    {
        var run = Generate(generator,
                           files,
                           globalOptions);
        return run.Sources;
    }

    public static IReadOnlyDictionary<string, string> RunVerified(IIncrementalGenerator generator,
                                                                  (string Path, string Text)[] files,
                                                                  Dictionary<string, string>? globalOptions = null)
    {
        var run = Generate(generator,
                           files,
                           globalOptions);
        VerifyCompiles(run.Trees);
        return run.Sources;
    }

    private readonly record struct GenerationRun(IReadOnlyDictionary<string, string> Sources,
                                                 ImmutableArray<SyntaxTree> Trees);

    private static GenerationRun Generate(IIncrementalGenerator generator,
                                          (string Path, string Text)[] files,
                                          Dictionary<string, string>? globalOptions)
    {
        var compilation = CSharpCompilation.Create("Test",
                                                   references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                                                   options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var texts = files.Select(f => (AdditionalText)new InMemoryText(f.Path, f.Text)).ToImmutableArray();
        var provider = new Provider(new Options(globalOptions ?? new Dictionary<string, string>()));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generators: new[] { generator.AsSourceGenerator() },
                                                              additionalTexts: texts,
                                                              parseOptions: null,
                                                              optionsProvider: provider);

        driver = driver.RunGenerators(compilation);
        var runResult = driver.GetRunResult();

        var failed = runResult.Results
            .Where(r => r.Exception is not null)
            .Select(r => $"{r.Generator.GetGeneratorType().Name} threw: {r.Exception}")
            .ToList();
        if (failed.Count > 0)
        {
            throw new InvalidOperationException(string.Join("\n", failed));
        }

        var diagnostics = runResult.Diagnostics
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .ToList();
        if (diagnostics.Count > 0)
        {
            throw new InvalidOperationException("Generator produced diagnostics:\n"
                + string.Join("\n", diagnostics.Select(d => d.ToString())));
        }

        var map = new Dictionary<string, string>();
        var trees = ImmutableArray.CreateBuilder<SyntaxTree>();
        foreach (var generatorResult in runResult.Results)
        {
            foreach (var source in generatorResult.GeneratedSources)
            {
                if (map.ContainsKey(source.HintName))
                {
                    throw new InvalidOperationException($"Duplicate generated hint name: {source.HintName}");
                }
                map[source.HintName] = source.SourceText.ToString();
                trees.Add(source.SyntaxTree);
            }
        }
        return new GenerationRun(map, trees.ToImmutable());
    }

    private static void VerifyCompiles(ImmutableArray<SyntaxTree> trees)
    {
        var verification = CSharpCompilation.Create("Verify",
                                                    syntaxTrees: trees,
                                                    references: VerificationReferences.Value,
                                                    options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var errors = verification.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Generated source did not compile:\n"
                + string.Join("\n", errors.Select(d => d.ToString())));
        }
    }

    private static readonly Lazy<ImmutableArray<MetadataReference>> VerificationReferences =
        new Lazy<ImmutableArray<MetadataReference>>(BuildVerificationReferences);

    private static ImmutableArray<MetadataReference> BuildVerificationReferences()
    {
        var references = ImmutableArray.CreateBuilder<MetadataReference>();

        var trusted = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "")
            .Split(Path.PathSeparator)
            .Where(p => p.Length > 0 && p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
        foreach (var path in trusted)
        {
            references.Add(MetadataReference.CreateFromFile(path));
        }

        foreach (var path in LocateTemplarAssemblies())
        {
            references.Add(MetadataReference.CreateFromFile(path));
        }

        return references.ToImmutable();
    }

    private static IEnumerable<string> LocateTemplarAssemblies()
    {
        var asmDir = Path.GetDirectoryName(typeof(GeneratorHarness).Assembly.Location) ?? AppContext.BaseDirectory;
        var configuration = asmDir.Replace('\\', '/').Contains("/Release/") ? "Release" : "Debug";

        var root = asmDir;
        while (root is not null && !File.Exists(Path.Combine(root, "Templar.slnx")))
        {
            root = Path.GetDirectoryName(root);
        }
        if (root is null)
        {
            yield break;
        }

        var uiBin = Path.Combine(root,
                                 "src",
                                 "Templar.UI",
                                 "bin",
                                 configuration,
                                 "net10.0");
        foreach (var name in new[] { "Templar.dll", "Templar.UI.dll" })
        {
            var candidate = Path.Combine(uiBin, name);
            if (File.Exists(candidate))
            {
                yield return candidate;
            }
        }
    }
}
