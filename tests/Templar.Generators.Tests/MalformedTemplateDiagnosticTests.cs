using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Templar.Generators.Tests;

public class MalformedTemplateDiagnosticTests
{
    private static GeneratorHarness.DiagnosticRun Run(string path, string content)
    {
        var options = new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "App",
            ["build_property.ProjectDir"] = "/proj/",
        };
        return GeneratorHarness.RunCapturingDiagnostics(new Templar.Generators.TemplateAccessorGenerator(),
                                                        new[] { (path, content) },
                                                        options);
    }

    [Fact]
    public void Conditional_marker_reports_diagnostic_and_emits_nothing()
    {
        var run = Run("/proj/Templates/Cond.tpl", "{{? cond }}x{{?}}");

        Assert.Contains(run.Diagnostics, d => d.Id == "TMPLR002");
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Raw_marker_reports_diagnostic_and_emits_nothing()
    {
        var run = Run("/proj/Templates/Raw.tpl", "{{& x }}");

        Assert.Contains(run.Diagnostics, d => d.Id == "TMPLR002");
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Slot_marker_reports_diagnostic_and_emits_nothing()
    {
        var run = Run("/proj/Templates/Slot.tpl", "{{> x }}");

        Assert.Contains(run.Diagnostics, d => d.Id == "TMPLR002");
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Filter_pipe_reports_diagnostic_and_emits_nothing()
    {
        var run = Run("/proj/Templates/Piped.tpl", "{{ x | upper }}");

        Assert.Contains(run.Diagnostics, d => d.Id == "TMPLR002");
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Unclosed_tag_reports_diagnostic_and_emits_nothing()
    {
        var run = Run("/proj/Templates/Open.tpl", "Hello {{ name");

        Assert.Contains(run.Diagnostics, d => d.Id == "TMPLR002");
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Diagnostic_carries_template_file_path()
    {
        var run = Run("/proj/Templates/Cond.tpl", "{{? cond }}x");

        var diagnostic = run.Diagnostics.Single(d => d.Id == "TMPLR002");
        Assert.Equal("/proj/Templates/Cond.tpl", diagnostic.Location.GetLineSpan().Path);
    }

    [Fact]
    public void Diagnostic_carries_line_of_offending_tag()
    {
        var run = Run("/proj/Templates/Late.tpl", "first\nsecond {{? cond }}");

        var diagnostic = run.Diagnostics.Single(d => d.Id == "TMPLR002");
        Assert.Equal(1, diagnostic.Location.GetLineSpan().StartLinePosition.Line);
    }

    [Fact]
    public void Well_formed_template_produces_no_diagnostic_and_emits_source()
    {
        var run = Run("/proj/Templates/Good.tpl", "Hello {{ name }}");

        Assert.DoesNotContain(run.Diagnostics, d => d.Id == "TMPLR002");
        Assert.NotEmpty(run.Sources);
    }
}
