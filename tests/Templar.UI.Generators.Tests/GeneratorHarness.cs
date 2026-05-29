using System.Collections.Immutable;
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

        var map = new Dictionary<string, string>();
        foreach (var generatorResult in driver.GetRunResult().Results)
        {
            foreach (var source in generatorResult.GeneratedSources)
            {
                map[source.HintName] = source.SourceText.ToString();
            }
        }
        return map;
    }
}
