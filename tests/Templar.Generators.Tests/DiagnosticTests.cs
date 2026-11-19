using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Templar.Generators.Tests;

public class DiagnosticTests
{
    private static Diagnostic Single(GeneratorHarness.Run run, string id)
    {
        var diagnostic = Assert.Single(run.Diagnostics);
        Assert.Equal(id, diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        return diagnostic;
    }

    private static GeneratorHarness.Run One(string source, string template, string path = "/proj/Templates/Card.tpl")
    {
        return GeneratorHarness.Generate(new[] { source }, new[] { (path, template) });
    }

    [Fact]
    public void Template_outside_the_project_directory_reports_TMPLR003()
    {
        var run = One("namespace App; public sealed class Card { }", "card", "/elsewhere/Card.tpl");

        var diagnostic = Single(run, "TMPLR003");
        Assert.Equal("/elsewhere/Card.tpl", diagnostic.Location.GetLineSpan().Path);
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Template_outside_the_Templates_folder_reports_TMPLR003()
    {
        var run = One("namespace App.Orders; public sealed class Card { }", "card", "/proj/Orders/Card.tpl");

        var diagnostic = Single(run, "TMPLR003");
        Assert.Equal("Template '/proj/Orders/Card.tpl' is outside the Templates folder '/proj/Templates/', so no type can be resolved for it",
                     diagnostic.GetMessage());
        Assert.Equal("/proj/Orders/Card.tpl", diagnostic.Location.GetLineSpan().Path);
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Template_with_no_type_reports_TMPLR004_naming_the_type()
    {
        var run = One("namespace App; public sealed class Other { }", "card");

        var diagnostic = Single(run, "TMPLR004");
        Assert.Contains("'App.Card'", diagnostic.GetMessage());
        Assert.Equal("/proj/Templates/Card.tpl", diagnostic.Location.GetLineSpan().Path);
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Type_in_another_namespace_than_its_folder_reports_TMPLR004()
    {
        var run = One("namespace Elsewhere; public sealed class Card { }", "card");

        Single(run, "TMPLR004");
    }

    [Fact]
    public void Generic_type_reports_TMPLR005()
    {
        var run = One("namespace App; public sealed class Card<T> { }", "card");

        Assert.Contains("generic", Single(run, "TMPLR005").GetMessage());
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Nested_type_reports_TMPLR005()
    {
        var run = One("namespace App; public static class Outer { public sealed class Card { } }", "card", "/proj/Templates/Outer/Card.tpl");

        Assert.Contains("nested in 'App.Outer'", Single(run, "TMPLR005").GetMessage());
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Type_that_is_not_sealed_reports_TMPLR005()
    {
        var run = One("namespace App; public class Card { }", "card");

        Assert.Contains("not sealed", Single(run, "TMPLR005").GetMessage());
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Struct_reports_TMPLR005()
    {
        var run = One("namespace App; public struct Card { }", "card");

        Assert.Contains("not a class or record", Single(run, "TMPLR005").GetMessage());
    }

    [Fact]
    public void Static_class_reports_TMPLR005()
    {
        var run = One("namespace App; public static class Card { }", "card");

        Assert.Contains("static", Single(run, "TMPLR005").GetMessage());
    }

    [Fact]
    public void Hole_with_no_property_reports_TMPLR006_at_the_hole_line()
    {
        var run = One("namespace App; public sealed class Card { public string Name { get; init; } = \"\"; }", "first\n{{ name }}\n{{ missing }}");

        var diagnostic = Single(run, "TMPLR006");
        Assert.Contains("'missing'", diagnostic.GetMessage());
        Assert.Equal(2, diagnostic.Location.GetLineSpan().StartLinePosition.Line);
    }

    [Fact]
    public void Hole_whose_property_is_write_only_reports_TMPLR006()
    {
        var run = One("namespace App; public sealed class Card { private string _s = \"\"; public string Sink { set => _s = value; } public string Used => _s; }", "{{ sink }}");

        Single(run, "TMPLR006");
    }

    [Fact]
    public void Hole_whose_property_is_private_reports_TMPLR006()
    {
        var run = One("namespace App; public sealed class Card { private string Name => \"\"; public string Other => Name; }", "{{ name }}");

        Single(run, "TMPLR006");
    }

    [Fact]
    public void Hole_whose_property_is_static_reports_TMPLR006()
    {
        var run = One("namespace App; public sealed class Card { public static string Name => \"\"; }", "{{ name }}");

        Single(run, "TMPLR006");
    }

    [Fact]
    public void Hole_matching_two_properties_case_insensitively_reports_TMPLR007()
    {
        var run = One("namespace App; public sealed class Card { public string Name => \"a\"; public string NAME => \"b\"; }", "{{ name }}");

        var diagnostic = Single(run, "TMPLR007");
        Assert.Contains("Name", diagnostic.GetMessage());
        Assert.Contains("NAME", diagnostic.GetMessage());
    }

    [Theory]
    [InlineData("object")]
    [InlineData("System.Uri")]
    [InlineData("App.Untemplated")]
    [InlineData("App.Base")]
    [InlineData("System.Collections.Generic.List<object>")]
    [InlineData("App.Sequence")]
    public void Hole_whose_type_cannot_render_reports_TMPLR008(string type)
    {
        var source = "namespace App;\n"
            + "public sealed class Untemplated { }\n"
            + "public abstract class Base { }\n"
            + "public sealed class Sequence : System.Collections.Generic.IEnumerable<string>, System.Collections.Generic.IEnumerable<int>\n"
            + "{\n"
            + "    System.Collections.Generic.IEnumerator<string> System.Collections.Generic.IEnumerable<string>.GetEnumerator() => null!;\n"
            + "    System.Collections.Generic.IEnumerator<int> System.Collections.Generic.IEnumerable<int>.GetEnumerator() => null!;\n"
            + "    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => null!;\n"
            + "}\n"
            + $"public sealed class Card {{ public {type}? Value {{ get; init; }} }}\n";

        var run = One(source, "{{ value }}");

        Assert.Contains("'Value'", Single(run, "TMPLR008").GetMessage());
    }

    [Fact]
    public void Interface_hole_with_an_implementation_that_has_no_template_reports_TMPLR008()
    {
        var source = "namespace App;\n"
            + "public interface IShape { }\n"
            + "public sealed class Circle : IShape { }\n"
            + "public sealed class Square : IShape { }\n"
            + "public sealed class Card { public required IShape Shape { get; init; } }\n";

        var run = GeneratorHarness.Generate(new[] { source },
                                            new[] { ("/proj/Templates/Card.tpl", "{{ shape }}"), ("/proj/Templates/Circle.tpl", "circle") });

        Assert.Contains("'App.Square' has no template", Single(run, "TMPLR008").GetMessage());
    }

    [Fact]
    public void Hole_error_omits_the_hole_and_still_emits_the_renderer()
    {
        var run = One("namespace App; public sealed class Card { public string Name { get; init; } = \"\"; }", "{{ name }}{{ missing }}");

        Single(run, "TMPLR006");
        var source = run.Sources["App.Card.g.cs"];
        Assert.Contains("w.Text(model.Name);", source);
        Assert.DoesNotContain("issing", source);
    }
}
