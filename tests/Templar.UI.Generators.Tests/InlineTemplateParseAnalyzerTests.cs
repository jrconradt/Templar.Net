using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Templar.Generators;
using Templar.Rendering;
using Xunit;

namespace Templar.UI.Generators.Tests;

public class InlineTemplateParseAnalyzerTests
{
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

    private static ImmutableArray<Diagnostic> Analyze(string body)
    {
        var source = "using Templar.Rendering;\n"
            + "public class Probe\n"
            + "{\n"
            + "    public void Run(string variable)\n"
            + "    {\n"
            + $"        {body}\n"
            + "    }\n"
            + "}\n";

        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create("AnalyzerProbe",
                                                   syntaxTrees: new[] { tree },
                                                   references: PlatformReferences(),
                                                   options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var withAnalyzers = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new InlineTemplateParseAnalyzer()));
        return withAnalyzers.GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public void Reports_TMPLR001_for_string_literal_argument()
    {
        var diagnostics = Analyze("var t = Template.Parse(\"Hello {{ who }}\");");
        Assert.Contains(diagnostics, d => d.Id == "TMPLR001");
    }

    [Fact]
    public void Reports_TMPLR001_for_interpolated_string_argument()
    {
        var diagnostics = Analyze("var t = Template.Parse($\"Hello {variable}\");");
        Assert.Contains(diagnostics, d => d.Id == "TMPLR001");
    }

    [Fact]
    public void Does_not_report_for_variable_argument()
    {
        var diagnostics = Analyze("var t = Template.Parse(variable);");
        Assert.DoesNotContain(diagnostics, d => d.Id == "TMPLR001");
    }
}
