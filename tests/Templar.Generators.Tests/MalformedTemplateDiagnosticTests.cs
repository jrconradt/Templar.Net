using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Templar.Generators.Tests;

public class MalformedTemplateDiagnosticTests
{
    private static GeneratorHarness.Run Run(string path, string content)
    {
        return GeneratorHarness.Generate(new[] { "namespace App; public sealed class Good { public string Name { get; init; } = \"\"; }" },
                                         new[] { (path, content) });
    }

    [Fact]
    public void Conditional_marker_reports_diagnostic_and_emits_nothing()
    {
        var run = Run("/proj/Cond.tpl", "{{? cond }}x{{?}}");

        Assert.Contains(run.Diagnostics, d => d.Id == "TMPLR002");
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Raw_marker_reports_diagnostic_and_emits_nothing()
    {
        var run = Run("/proj/Raw.tpl", "{{& x }}");

        Assert.Contains(run.Diagnostics, d => d.Id == "TMPLR002");
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Slot_marker_reports_diagnostic_and_emits_nothing()
    {
        var run = Run("/proj/Slot.tpl", "{{> x }}");

        Assert.Contains(run.Diagnostics, d => d.Id == "TMPLR002");
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Filter_pipe_reports_diagnostic_and_emits_nothing()
    {
        var run = Run("/proj/Piped.tpl", "{{ x | upper }}");

        Assert.Contains(run.Diagnostics, d => d.Id == "TMPLR002");
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Unclosed_tag_reports_diagnostic_and_emits_nothing()
    {
        var run = Run("/proj/Open.tpl", "Hello {{ name");

        Assert.Contains(run.Diagnostics, d => d.Id == "TMPLR002");
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Diagnostic_carries_template_file_path()
    {
        var run = Run("/proj/Cond.tpl", "{{? cond }}x");

        var diagnostic = run.Diagnostics.Single(d => d.Id == "TMPLR002");
        Assert.Equal("/proj/Cond.tpl", diagnostic.Location.GetLineSpan().Path);
    }

    [Fact]
    public void Diagnostic_carries_line_of_offending_tag()
    {
        var run = Run("/proj/Late.tpl", "first\nsecond {{? cond }}");

        var diagnostic = run.Diagnostics.Single(d => d.Id == "TMPLR002");
        Assert.Equal(1, diagnostic.Location.GetLineSpan().StartLinePosition.Line);
    }

    [Fact]
    public void Well_formed_template_produces_no_diagnostic_and_emits_source()
    {
        var run = Run("/proj/Good.tpl", "Hello {{ name }}");

        Assert.Empty(run.Diagnostics);
        Assert.NotEmpty(run.Sources);
    }
}
