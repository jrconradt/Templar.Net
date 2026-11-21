using System.Linq;
using Xunit;

namespace Templar.Generators.Tests;

// A template is a view; any type that carries the properties it uses is a view model for it. The generator
// pairs (type, template) from [Template] types, ResolveTemplate call sites and the spots they reach, and
// compiles one flat renderer per pair; a template no pair reaches is not compiled in.
public class PairTests
{
    private static string Render(string source, params (string Path, string Text)[] templates)
    {
        var run = GeneratorHarness.Generate(new[] { source }, templates);
        return GeneratorHarness.Probe(GeneratorHarness.Load(run));
    }

    private const string TwoTypes = "namespace App;\n"
        + "using Templar.Rendering;\n"
        + "public sealed record Book(string Title, string Author);\n"
        + "public sealed class Film { public required string Title { get; init; } public int Year { get; init; } }\n"
        + "public static class Probe\n"
        + "{\n"
        + "    public static string Run() => Compositor.ResolveTemplate(new Book(\"Dune\", \"Herbert\"), \"Card\")\n"
        + "                                 + \"|\" + Compositor.ResolveTemplate(new Film { Title = \"Alien\", Year = 1979 }, \"Card\");\n"
        + "}\n";

    [Fact]
    public void One_template_renders_two_unrelated_types()
    {
        var rendered = Render(TwoTypes, ("/proj/Templates/Card.tpl", "card {{ title }}"));

        Assert.Equal("card Dune|card Alien", rendered);
    }

    [Fact]
    public void One_template_compiles_one_renderer_per_type_it_renders()
    {
        var run = GeneratorHarness.Generate(new[] { TwoTypes }, new[] { ("/proj/Templates/Card.tpl", "card {{ title }}") });

        Assert.Empty(run.Diagnostics);
        var source = run.Sources["Card.g.cs"];
        Assert.Contains("Steps(global::Templar.Rendering.TemplarWriter w, global::App.Book model)", source);
        Assert.Contains("Steps(global::Templar.Rendering.TemplarWriter w, global::App.Film model)", source);
    }

    [Fact]
    public void A_type_names_its_default_template_used_at_the_root_and_in_spots()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "[Template(\"Orders/Line\")] public sealed record Line(string Sku);\n"
            + "public sealed record Order(string Id, Sequence<Line> Lines);\n"
            + "public static class Probe\n"
            + "{\n"
            + "    public static string Run()\n"
            + "    {\n"
            + "        Line[] lines = [new Line(\"A-100\"), new Line(\"B-200\")];\n"
            + "        return Compositor.ResolveTemplate(new Order(\"R-1\", Sequence.Lines(lines)), \"Orders/Order\") + \"|\" + Compositor.ResolveTemplate(lines[0]);\n"
            + "    }\n"
            + "}\n";

        var rendered = Render(source,
                              ("/proj/Templates/Orders/Order.tpl", "order {{ id }}\n    {{ lines }}"),
                              ("/proj/Templates/Orders/Line.tpl", "line {{ sku }}"));

        Assert.Equal("order R-1\n    line A-100\n    line B-200|line A-100", rendered);
    }

    [Fact]
    public void A_template_no_pair_reaches_is_not_compiled_in()
    {
        var source = "namespace App;\n"
            + "[Templar.Rendering.Template(\"Used\")] public sealed record Card(string Title);\n";

        var run = GeneratorHarness.Generate(new[] { source },
                                            new[] { ("/proj/Templates/Used.tpl", "used {{ title }}"), ("/proj/Templates/Unused.tpl", "never {{ title }}") });

        Assert.Empty(run.Diagnostics);
        Assert.Equal(new[] { "Used.g.cs" }, run.Sources.Keys.ToArray());
        Assert.DoesNotContain(run.Sources.Values, s => s.Contains("Unused") || s.Contains("never"));
    }
}
