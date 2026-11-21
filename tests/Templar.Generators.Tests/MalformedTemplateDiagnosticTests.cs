using System.Linq;
using Xunit;

namespace Templar.Generators.Tests;

public class MalformedTemplateDiagnosticTests
{
    private static GeneratorHarness.Run Run(string path, string content)
    {
        return GeneratorHarness.Generate(new[] { "namespace App; [Templar.Rendering.Template(\"Good\")] public sealed record Good(string Name);" },
                                         new[] { ("/proj/Templates/Good.tpl", "Hello {{ name }}"), (path, content) });
    }

    private static void AssertMalformedAndNotEmitted(GeneratorHarness.Run run, string hintName)
    {
        Assert.Contains(run.Diagnostics, d => d.Id == "TMPLR002");
        Assert.DoesNotContain(hintName, run.Sources.Keys);
    }

    [Fact]
    public void Conditional_marker_reports_diagnostic_and_emits_nothing()
    {
        AssertMalformedAndNotEmitted(Run("/proj/Templates/Cond.tpl", "{{? cond }}x{{?}}"), "Cond.g.cs");
    }

    [Fact]
    public void Raw_marker_reports_diagnostic_and_emits_nothing()
    {
        AssertMalformedAndNotEmitted(Run("/proj/Templates/Raw.tpl", "{{& x }}"), "Raw.g.cs");
    }

    [Fact]
    public void Slot_marker_reports_diagnostic_and_emits_nothing()
    {
        AssertMalformedAndNotEmitted(Run("/proj/Templates/Slot.tpl", "{{> x }}"), "Slot.g.cs");
    }

    [Fact]
    public void Filter_pipe_reports_diagnostic_and_emits_nothing()
    {
        AssertMalformedAndNotEmitted(Run("/proj/Templates/Piped.tpl", "{{ x | upper }}"), "Piped.g.cs");
    }

    [Fact]
    public void Unclosed_tag_reports_diagnostic_and_emits_nothing()
    {
        AssertMalformedAndNotEmitted(Run("/proj/Templates/Open.tpl", "Hello {{ name"), "Open.g.cs");
    }

    [Fact]
    public void Comment_containing_marker_characters_is_still_a_comment()
    {
        var run = Run("/proj/Templates/Note.tpl", "a{{# ? & > | : }}b");

        Assert.Empty(run.Diagnostics);
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
        Assert.Equal("Unsupported tag marker '?' at line 2", diagnostic.GetMessage());
    }

    [Fact]
    public void Unclosed_tag_diagnostic_carries_the_line_it_opens_on()
    {
        var run = Run("/proj/Templates/Open.tpl", "line1\nline2 {{ unclosed");

        var diagnostic = run.Diagnostics.Single(d => d.Id == "TMPLR002");
        Assert.Equal(1, diagnostic.Location.GetLineSpan().StartLinePosition.Line);
        Assert.Equal("Unclosed tag starting at line 2", diagnostic.GetMessage());
    }

    [Fact]
    public void Well_formed_template_produces_no_diagnostic_and_emits_source()
    {
        var run = Run("/proj/Templates/Other.tpl", "Other {{ name }}");

        Assert.Empty(run.Diagnostics);
        Assert.Contains("Good.g.cs", run.Sources.Keys);
    }
}
