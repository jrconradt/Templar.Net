using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Templar.UI.Generators.Tests;

public class UnbalancedTemplateDiagnosticTests
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
    public void Extra_closers_report_diagnostic_and_emit_nothing()
    {
        var run = Run("/proj/Templates/Bad.tpl", "{{?a}}x{{?}}{{?}}{{?}}");

        Assert.Contains(run.Diagnostics, d => d.Id == "TMPLR002");
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Unterminated_opener_reports_diagnostic_and_emit_nothing()
    {
        var run = Run("/proj/Templates/Open.tpl", "{{? cond }}body with no closer");

        Assert.Contains(run.Diagnostics, d => d.Id == "TMPLR002");
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Diagnostic_carries_template_file_path()
    {
        var run = Run("/proj/Templates/Open.tpl", "{{? cond }}no closer");

        var diagnostic = run.Diagnostics.Single(d => d.Id == "TMPLR002");
        Assert.Equal("/proj/Templates/Open.tpl", diagnostic.Location.GetLineSpan().Path);
    }

    [Fact]
    public void Well_formed_template_produces_no_diagnostic_and_emits_source()
    {
        var run = Run("/proj/Templates/Good.tpl", "{{?a}}x{{?else}}y{{?}}");

        Assert.DoesNotContain(run.Diagnostics, d => d.Id == "TMPLR002");
        Assert.NotEmpty(run.Sources);
    }
}
