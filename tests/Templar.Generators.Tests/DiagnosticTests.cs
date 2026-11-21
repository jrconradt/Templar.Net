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
        Assert.Equal("Templar", diagnostic.Descriptor.Category);
        return diagnostic;
    }

    private static GeneratorHarness.Run Generate(string source, params (string Path, string Text)[] templates)
    {
        return GeneratorHarness.Generate(new[] { source }, templates);
    }

    private static (int Line, int Character) Start(Diagnostic diagnostic)
    {
        var start = diagnostic.Location.GetLineSpan().StartLinePosition;
        return (start.Line, start.Character);
    }

    [Fact]
    public void Malformed_template_reports_TMPLR002_at_its_line_even_when_nothing_reaches_it()
    {
        var run = Generate("namespace App;", ("/proj/Templates/Bad.tpl", "first\n{{ a b }}"));

        var diagnostic = Single(run, "TMPLR002");
        Assert.Equal("/proj/Templates/Bad.tpl", diagnostic.Location.GetLineSpan().Path);
        Assert.Equal(1, Start(diagnostic).Line);
        Assert.Empty(run.Sources);
    }

    [Theory]
    [InlineData("{{ x : }}")]
    [InlineData("{{ : Card }}")]
    [InlineData("{{ x : Card : Other }}")]
    [InlineData("{{ x : Card/ }}")]
    [InlineData("{{ }}")]
    public void Spot_that_is_not_a_property_name_reports_TMPLR002(string spot)
    {
        var run = Generate("namespace App;", ("/proj/Templates/Bad.tpl", spot));

        Assert.Contains("Malformed spot", Single(run, "TMPLR002").GetMessage());
    }

    [Fact]
    public void Spot_naming_a_template_reports_TMPLR002()
    {
        var run = Generate("namespace App;", ("/proj/Templates/Card.tpl", "card\n{{ badge : Tag }}"), ("/proj/Templates/Tag.tpl", "#{{ name }}"));

        var diagnostic = Single(run, "TMPLR002");
        Assert.Equal("Malformed spot 'badge : Tag' at line 2: a spot is a property name", diagnostic.GetMessage());
        Assert.Equal("/proj/Templates/Card.tpl", diagnostic.Location.GetLineSpan().Path);
    }

    [Fact]
    public void Template_outside_the_Templates_folder_reports_TMPLR003()
    {
        var run = Generate("namespace App;", ("/proj/Orders/Card.tpl", "card"));

        var diagnostic = Single(run, "TMPLR003");
        Assert.Equal("Template '/proj/Orders/Card.tpl' is outside the Templates folder '/proj/Templates/', so it has no name",
                     diagnostic.GetMessage());
        Assert.Equal("/proj/Orders/Card.tpl", diagnostic.Location.GetLineSpan().Path);
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Template_attribute_naming_no_template_reports_TMPLR004_at_the_attribute()
    {
        var source = "namespace App;\n"
            + "[Templar.Rendering.Template(\"Missing\")]\n"
            + "public sealed record Card(string Title);\n";

        var run = Generate(source, ("/proj/Templates/Card.tpl", "card {{ title }}"));

        var diagnostic = Single(run, "TMPLR004");
        Assert.Equal("[Template] on 'App.Card' names template 'Missing', which is not declared under Templates/", diagnostic.GetMessage());
        Assert.Equal((1, 1), Start(diagnostic));
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Call_site_naming_no_template_reports_TMPLR004_at_the_call_site()
    {
        var source = "namespace App;\n"
            + "public sealed record Card(string Title);\n"
            + "public static class Probe { public static string Run() => Templar.Rendering.Compositor.ResolveTemplate(new Card(\"x\"), \"Missing\"); }\n";

        var run = Generate(source, ("/proj/Templates/Card.tpl", "card {{ title }}"));

        var diagnostic = Single(run, "TMPLR004");
        Assert.Equal("ResolveTemplate names template 'Missing', which is not declared under Templates/", diagnostic.GetMessage());
        Assert.Equal(2, Start(diagnostic).Line);
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Non_constant_template_name_reports_TMPLR005_at_the_call_site()
    {
        var source = "namespace App;\n"
            + "public sealed record Card(string Title);\n"
            + "public static class Probe { public static string Run(string name) => Templar.Rendering.Compositor.ResolveTemplate(new Card(\"x\"), name); }\n";

        var run = Generate(source, ("/proj/Templates/Card.tpl", "card {{ title }}"));

        var diagnostic = Single(run, "TMPLR005");
        Assert.Equal("The template name passed to ResolveTemplate is not a constant", diagnostic.GetMessage());
        Assert.Equal(2, Start(diagnostic).Line);
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Type_missing_a_property_reports_TMPLR006_at_the_call_site_naming_the_template_line()
    {
        var source = "namespace App;\n"
            + "public sealed record Book(string Title);\n"
            + "public static class Probe\n"
            + "{\n"
            + "    public static string Run() => Templar.Rendering.Compositor.ResolveTemplate(new Book(\"Dune\"), \"Card\");\n"
            + "}\n";

        var run = Generate(source, ("/proj/Templates/Card.tpl", "card\n{{ title }} by {{ author }}"));

        var diagnostic = Single(run, "TMPLR006");
        Assert.Equal("Spot 'author' at line 2 of template 'Card' names no readable property of 'App.Book'", diagnostic.GetMessage());
        Assert.Equal((4, 34), Start(diagnostic));
    }

    [Fact]
    public void Type_missing_a_property_reports_TMPLR006_at_its_Template_attribute()
    {
        var source = "namespace App;\n"
            + "[Templar.Rendering.Template(\"Card\")]\n"
            + "public sealed record Book(string Title);\n";

        var run = Generate(source, ("/proj/Templates/Card.tpl", "{{ title }} by {{ author }}"));

        var diagnostic = Single(run, "TMPLR006");
        Assert.Contains("'author'", diagnostic.GetMessage());
        Assert.Equal((1, 1), Start(diagnostic));
    }

    [Theory]
    [InlineData("private string _s = \"\"; public string Name { set => _s = value; } public string Used => _s;")]
    [InlineData("private string Name => \"\"; public string Other => Name;")]
    [InlineData("public static string Name => \"\";")]
    public void Spot_whose_property_is_write_only_private_or_static_reports_TMPLR006(string members)
    {
        var source = "namespace App;\n"
            + "[Templar.Rendering.Template(\"Card\")] public sealed class Card { " + members + " }\n";

        var run = Generate(source, ("/proj/Templates/Card.tpl", "{{ name }}"));

        Single(run, "TMPLR006");
    }

    [Fact]
    public void Spot_matching_two_properties_case_insensitively_reports_TMPLR007()
    {
        var source = "namespace App;\n"
            + "[Templar.Rendering.Template(\"Card\")] public sealed class Card { public string Name => \"a\"; public string NAME => \"b\"; }\n";

        var run = Generate(source, ("/proj/Templates/Card.tpl", "{{ name }}"));

        var diagnostic = Single(run, "TMPLR007");
        Assert.Equal("Spot 'name' at line 1 of template 'Card' matches more than one property of 'App.Card': Name, NAME", diagnostic.GetMessage());
    }

    [Theory]
    [InlineData("object")]
    [InlineData("System.Uri")]
    [InlineData("App.Plain")]
    [InlineData("System.Collections.Generic.List<object>")]
    [InlineData("App.Sequence")]
    public void Spot_whose_type_cannot_render_reports_TMPLR008(string type)
    {
        var source = "namespace App;\n"
            + "public sealed class Plain { }\n"
            + "public sealed class Sequence : System.Collections.Generic.IEnumerable<string>, System.Collections.Generic.IEnumerable<int>\n"
            + "{\n"
            + "    System.Collections.Generic.IEnumerator<string> System.Collections.Generic.IEnumerable<string>.GetEnumerator() => null!;\n"
            + "    System.Collections.Generic.IEnumerator<int> System.Collections.Generic.IEnumerable<int>.GetEnumerator() => null!;\n"
            + "    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => null!;\n"
            + "}\n"
            + $"[Templar.Rendering.Template(\"Card\")] public sealed class Card {{ public {type}? Value {{ get; init; }} }}\n";

        var run = Generate(source, ("/proj/Templates/Card.tpl", "{{ value }}"));

        Assert.Contains("at spot 'value' (line 1 of template 'Card', property 'Value')", Single(run, "TMPLR008").GetMessage());
    }

    [Theory]
    [InlineData("public interface IShape { }", "IShape")]
    [InlineData("public abstract record Shape;", "Shape")]
    public void Spot_whose_type_has_no_Template_reports_TMPLR008_though_its_implementations_have_one(string declaration, string type)
    {
        var source = "namespace App;\n"
            + declaration + "\n"
            + $"[Templar.Rendering.Template(\"Circle\")] public sealed record Circle : {type};\n"
            + $"[Templar.Rendering.Template(\"Card\")] public sealed record Card({type} Shape);\n";

        var run = Generate(source, ("/proj/Templates/Card.tpl", "{{ shape }}"), ("/proj/Templates/Circle.tpl", "circle"));

        var diagnostic = Single(run, "TMPLR008");
        Assert.Equal($"Type 'App.{type}' cannot render at spot 'shape' (line 1 of template 'Card', property 'Shape'): it is not a string, primitive, enum, Sequence<T>, IEnumerable<T> or type with [Template]",
                     diagnostic.GetMessage());
    }

    [Fact]
    public void ResolveTemplate_by_default_on_a_type_with_no_Template_reports_TMPLR008_at_the_call_site()
    {
        var source = "namespace App;\n"
            + "public sealed record Card(string Title);\n"
            + "public static class Probe { public static string Run() => Templar.Rendering.Compositor.ResolveTemplate(new Card(\"x\")); }\n";

        var run = Generate(source, ("/proj/Templates/Card.tpl", "card {{ title }}"));

        var diagnostic = Single(run, "TMPLR008");
        Assert.Equal("Type 'App.Card' cannot render as the model of ResolveTemplate: it has no [Template]", diagnostic.GetMessage());
        Assert.Equal(2, Start(diagnostic).Line);
    }

    [Fact]
    public void Template_on_a_generic_type_reports_TMPLR008_at_the_attribute()
    {
        var source = "namespace App;\n"
            + "[Templar.Rendering.Template(\"Card\")]\n"
            + "public sealed record Box<T>(string Title);\n";

        var run = Generate(source, ("/proj/Templates/Card.tpl", "card {{ title }}"));

        var diagnostic = Single(run, "TMPLR008");
        Assert.Equal("Type 'App.Box<T>' cannot render by its [Template]: it is generic", diagnostic.GetMessage());
        Assert.Equal((1, 1), Start(diagnostic));
    }

    [Fact]
    public void Spot_error_omits_the_spot_and_still_emits_the_renderer()
    {
        var source = "namespace App;\n"
            + "[Templar.Rendering.Template(\"Card\")] public sealed record Card(string Name);\n";

        var run = Generate(source, ("/proj/Templates/Card.tpl", "{{ name }}{{ missing }}"));

        Single(run, "TMPLR006");
        var emitted = run.Sources["Card.g.cs"];
        Assert.Contains("w.Text(model.Name);", emitted);
        Assert.DoesNotContain("issing", emitted);
    }

    [Fact]
    public void A_pair_reached_from_several_sites_reports_its_error_once()
    {
        var source = "namespace App;\n"
            + "[Templar.Rendering.Template(\"Card\")] public sealed record Card(string Name);\n"
            + "public static class Probe\n"
            + "{\n"
            + "    public static string One() => Templar.Rendering.Compositor.ResolveTemplate(new Card(\"a\"));\n"
            + "    public static string Two() => Templar.Rendering.Compositor.ResolveTemplate(new Card(\"b\"), \"Card\");\n"
            + "}\n";

        var run = Generate(source, ("/proj/Templates/Card.tpl", "{{ name }}{{ missing }}"));

        Assert.Equal((1, 1), Start(Single(run, "TMPLR006")));
    }
}
