using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Templar.Rendering;
using Xunit;

namespace Templar.UI.Generators.Tests;

public class TemplateAccessorGeneratorEndToEndTests
{
    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly string _text;

        public InMemoryAdditionalText(string path, string text)
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

    private sealed class GlobalOnlyOptions : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _values;

        public GlobalOnlyOptions(Dictionary<string, string> values)
        {
            _values = values;
        }

        public override bool TryGetValue(string key, out string value)
        {
            return _values.TryGetValue(key, out value!);
        }
    }

    private sealed class GlobalOnlyProvider : Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptionsProvider
    {
        private readonly Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions _global;

        public GlobalOnlyProvider(Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions global)
        {
            _global = global;
        }

        public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GlobalOptions => _global;

        public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _global;

        public override Microsoft.CodeAnalysis.Diagnostics.AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _global;
    }

    private static ImmutableArray<MetadataReference> PlatformReferences()
    {
        var trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "";
        var builder = ImmutableArray.CreateBuilder<MetadataReference>();
        foreach (var path in trusted.Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            builder.Add(MetadataReference.CreateFromFile(path));
        }
        builder.Add(MetadataReference.CreateFromFile(typeof(Compositor).Assembly.Location));
        return builder.ToImmutable();
    }

    private static (string Source, Assembly Assembly) CompileGeneratedAccessor(string templatePath,
                                                                              string templateText,
                                                                              string rootNamespace,
                                                                              string projectDir)
    {
        var references = PlatformReferences();
        var compilation = CSharpCompilation.Create("AccessorHost",
                                                   references: references,
                                                   options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var options = new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = rootNamespace,
            ["build_property.ProjectDir"] = projectDir,
        };
        var provider = new GlobalOnlyProvider(new GlobalOnlyOptions(options));
        var texts = ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText(templatePath, templateText));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generators: new[] { new Templar.Generators.TemplateAccessorGenerator().AsSourceGenerator() },
                                                              additionalTexts: texts,
                                                              parseOptions: null,
                                                              optionsProvider: provider);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation,
                                                          out var output,
                                                          out var generatorDiagnostics);

        Assert.Empty(generatorDiagnostics);

        var generatedSource = "";
        foreach (var result in driver.GetRunResult().Results)
        {
            foreach (var source in result.GeneratedSources)
            {
                generatedSource = source.SourceText.ToString();
            }
        }
        Assert.NotEqual("", generatedSource);

        using var peStream = new System.IO.MemoryStream();
        var emitResult = output.Emit(peStream);
        var errors = emitResult.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.GetMessage())
            .ToArray();
        Assert.True(emitResult.Success, string.Join("\n", errors));

        var assembly = Assembly.Load(peStream.ToArray());
        return (generatedSource, assembly);
    }

    [Fact]
    public void Generated_accessor_compiles_and_renders_template_content_at_runtime()
    {
        var (source, assembly) = CompileGeneratedAccessor("/proj/Templates/Greeting.tpl",
                                                          "Hello {{ who }}, welcome to {{ place }}!",
                                                          "App",
                                                          "/proj/");

        Assert.Contains("public override void RenderInto", source);

        var type = assembly.GetType("App.Templates.Greeting");
        Assert.NotNull(type);

        var instance = (Compositor)Activator.CreateInstance(type!)!;
        type!.GetProperty("Who")!.SetValue(instance, "Ada");
        type.GetProperty("Place")!.SetValue(instance, "Templar");

        var rendered = instance.Render();
        Assert.Equal("Hello Ada, welcome to Templar!", rendered);
    }

    [Fact]
    public void Generated_accessor_compiles_conditionals_and_filters()
    {
        var (source, assembly) = CompileGeneratedAccessor("/proj/Templates/Cond.tpl",
                                                          "{{? show }}Hi {{ name | upper }}{{?else}}bye{{?}}",
                                                          "App",
                                                          "/proj/");

        Assert.Contains("w.Truthy(", source);

        var type = assembly.GetType("App.Templates.Cond")!;

        var shown = (Compositor)Activator.CreateInstance(type)!;
        type.GetProperty("Show")!.SetValue(shown, true);
        type.GetProperty("Name")!.SetValue(shown, "ada");
        Assert.Equal("Hi ADA", shown.Render());

        var hidden = (Compositor)Activator.CreateInstance(type)!;
        type.GetProperty("Show")!.SetValue(hidden, false);
        type.GetProperty("Name")!.SetValue(hidden, "ada");
        Assert.Equal("bye", hidden.Render());
    }

    [Fact]
    public void Generated_accessor_preserves_quotes_newlines_and_braces_in_structure()
    {
        var templateText = "line1 \"quoted\"\nline2\twith \\{{ escaped {{ x }}";
        var (_, assembly) = CompileGeneratedAccessor("/proj/Templates/Tricky.tpl",
                                                     templateText,
                                                     "App",
                                                     "/proj/");

        var type = assembly.GetType("App.Templates.Tricky");
        Assert.NotNull(type);

        var instance = (Compositor)Activator.CreateInstance(type!)!;
        type!.GetProperty("X")!.SetValue(instance, "value");

        var rendered = instance.Render();
        Assert.Equal("line1 \"quoted\"\nline2\twith {{ escaped value", rendered);
    }
}
