using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Templar.Generators.Tests;

public class EmissionTests
{
    private const string Model = "namespace App;\n"
        + "using Templar.Rendering;\n"
        + "public enum Color { Red, Green }\n"
        + "public interface IShape { }\n"
        + "[Template(\"Circle\")] public sealed record Circle(int Radius) : IShape;\n"
        + "[Template(\"Square\")] public sealed record Square(int Side) : IShape;\n"
        + "[Template(\"Item\")] public sealed record Item(string Name);\n"
        + "public sealed record Tag(string Name);\n"
        + "[Template(\"Card\")]\n"
        + "public sealed class Card\n"
        + "{\n"
        + "    public required string Title { get; init; }\n"
        + "    public int Count { get; init; }\n"
        + "    public double? Ratio { get; init; }\n"
        + "    public Color Color { get; init; }\n"
        + "    public Item? Lead { get; init; }\n"
        + "    public required Sequence<Item> Items { get; init; }\n"
        + "    public System.Collections.Generic.IReadOnlyList<string> Notes { get; init; } = [];\n"
        + "    public int[] Numbers { get; init; } = [];\n"
        + "    public required IShape Shape { get; init; }\n"
        + "    public System.Collections.Generic.IEnumerable<IShape> Shapes { get; init; } = [];\n"
        + "    public System.Collections.Generic.IEnumerable<Sequence<Item>> Groups { get; init; } = [];\n"
        + "    public Tag? Badge { get; init; }\n"
        + "    public required Sequence<Tag> Tags { get; init; }\n"
        + "    public Templar.Presets.Using? Import { get; init; }\n"
        + "    public string @class => \"c\";\n"
        + "}\n";

    private const string CardTemplate = "card \"{{ title }}\"\n"
        + "count {{ count }} ratio {{ ratio }} color {{ color }}\n"
        + "{{# a comment }}lead {{ lead }}\n"
        + "items {{ items }}\n"
        + "notes {{ notes }}\n"
        + "numbers {{ numbers }}\n"
        + "shape {{ shape }}\n"
        + "shapes {{ shapes }}\n"
        + "groups {{ groups }}\n"
        + "badge {{ badge : Tag }} tags {{ tags : Tag }}\n"
        + "import {{ import }}\n"
        + "self {{ : Summary }}\n"
        + "class {{ CLASS }}\\{{ escaped \\}}";

    private static GeneratorHarness.Run Generate()
    {
        return GeneratorHarness.Generate(new[] { Model },
                                         new[]
                                         {
                                             ("/proj/Templates/Card.tpl", CardTemplate),
                                             ("/proj/Templates/Item.tpl", "{{ name }}"),
                                             ("/proj/Templates/Circle.tpl", "circle {{ radius }}"),
                                             ("/proj/Templates/Square.tpl", "square {{ side }}"),
                                             ("/proj/Templates/Tag.tpl", "#{{ name }}"),
                                             ("/proj/Templates/Summary.tpl", "{{ title }} x{{ count }}"),
                                             ("/proj/Templates/Unused.tpl", "never {{ name }}"),
                                         });
    }

    [Fact]
    public void Each_spot_write_is_chosen_from_the_declared_type_of_its_property()
    {
        var run = Generate();
        Assert.Empty(run.Diagnostics);
        var source = run.Sources["Card.g.cs"];

        Assert.Contains("        w.Text(model.Title);\n", source);
        Assert.Contains("        w.Format(model.Count);\n", source);
        Assert.Contains("        w.Format(model.Ratio);\n", source);
        Assert.Contains("        w.Format(model.Color);\n", source);
        Assert.Contains("        yield return w.Compose(model.Lead, global::App.Templates.ItemTemplate.Steps);\n", source);
        Assert.Contains("        yield return w.List(model.Items, global::App.Templates.ItemTemplate.Steps);\n", source);
        Assert.Contains("        yield return w.List(model.Notes, global::Templar.Rendering.TemplarWriter.TextItem);\n", source);
        Assert.Contains("        yield return w.List(model.Numbers, global::Templar.Rendering.TemplarWriter.FormatItem<int>);\n", source);
        Assert.Contains("        yield return w.Compose(model.Shape, global::Templar.Rendering.RenderRegistry.Steps);\n", source);
        Assert.Contains("        yield return w.List(model.Shapes, global::Templar.Rendering.RenderRegistry.Steps);\n", source);
        Assert.Contains("        yield return w.List(model.Groups, (w1, m1) => global::Templar.Rendering.TemplarWriter.ListItem(w1, m1, global::App.Templates.ItemTemplate.Steps));\n", source);
        Assert.Contains("        yield return w.Compose(model.Badge, global::App.Templates.TagTemplate.Steps);\n", source);
        Assert.Contains("        yield return w.List(model.Tags, global::App.Templates.TagTemplate.Steps);\n", source);
        Assert.Contains("        yield return w.Compose(model.Import, global::Templar.Rendering.RenderRegistry.Find<global::Templar.Presets.Using>(\"Presets/Using\"));\n", source);
        Assert.Contains("        w.Text(model.@class);\n", source);
        Assert.Contains("        w.Literal(\"\\nclass \");\n", source);
        Assert.Contains("        w.Literal(\"{{ escaped }}\");\n", source);
        Assert.Contains("        w.Literal(\"card \\\"\");\n", source);
    }

    [Fact]
    public void A_self_spot_composes_the_model_by_the_named_template_for_the_same_type()
    {
        var run = Generate();
        Assert.Empty(run.Diagnostics);

        Assert.Contains("        w.Literal(\"\\nself \");\n        yield return w.Compose(model, global::App.Templates.SummaryTemplate.Steps);\n", run.Sources["Card.g.cs"]);
        var summary = run.Sources["Summary.g.cs"];
        Assert.Contains("Steps(global::Templar.Rendering.TemplarWriter w, global::App.Card model)", summary);
        Assert.Contains("        w.Text(model.Title);\n        w.Literal(\" x\");\n        w.Format(model.Count);\n", summary);
        Assert.Contains("RenderRegistry.Register<global::App.Card>(\"Summary\", Steps);", summary);
    }

    [Fact]
    public void Emitted_renderers_hold_no_conditional_and_no_loop()
    {
        var run = Generate();
        Assert.Empty(run.Diagnostics);
        Assert.Equal(new[] { "Card.g.cs", "Circle.g.cs", "Item.g.cs", "Square.g.cs", "Summary.g.cs", "Tag.g.cs" },
                     run.Sources.Keys.OrderBy(k => k, System.StringComparer.Ordinal).ToArray());

        var forbidden = new[]
        {
            SyntaxKind.IfStatement,
            SyntaxKind.SwitchStatement,
            SyntaxKind.SwitchExpression,
            SyntaxKind.ConditionalExpression,
            SyntaxKind.CoalesceExpression,
            SyntaxKind.CoalesceAssignmentExpression,
            SyntaxKind.ConditionalAccessExpression,
            SyntaxKind.IsPatternExpression,
            SyntaxKind.IsExpression,
            SyntaxKind.LogicalAndExpression,
            SyntaxKind.LogicalOrExpression,
            SyntaxKind.ForStatement,
            SyntaxKind.ForEachStatement,
            SyntaxKind.WhileStatement,
            SyntaxKind.DoStatement,
            SyntaxKind.GotoStatement,
        };
        foreach (var (hint, text) in run.Sources)
        {
            var root = CSharpSyntaxTree.ParseText(text, GeneratorHarness.ParseOptions).GetRoot();
            var found = root.DescendantNodesAndSelf()
                .Where(n => forbidden.Contains(n.Kind()))
                .Select(n => $"{hint}: {n.Kind()} `{n}`")
                .ToList();
            Assert.True(found.Count == 0, string.Join("\n", found));
        }
    }

    [Fact]
    public void Emits_one_class_per_template_with_a_renderer_per_pair_and_their_registration()
    {
        var run = GeneratorHarness.Generate(new[]
                                            {
                                                "namespace App.Orders; public sealed class Requisite { public required string Name { get; init; } public required Templar.Rendering.Sequence<Line> Lines { get; init; } }",
                                                "namespace App.Orders; [Templar.Rendering.Template(\"Orders/Line\")] public sealed record Line(string Sku);",
                                                "namespace App; public static class Probe { public static string Run(App.Orders.Requisite r) => Templar.Rendering.Compositor.ResolveTemplate(r, \"Orders/Requisite\"); }",
                                            },
                                            new[]
                                            {
                                                ("/proj/Templates/Orders/Requisite.tpl", "requisite {{ name }}\n{\n    {{ lines }}\n}"),
                                                ("/proj/Templates/Orders/Line.tpl", "line {{ sku }}"),
                                            });

        Assert.Empty(run.Diagnostics);
        var requisite = "// <auto-generated/>\n"
            + "#nullable enable\n"
            + "\n"
            + "namespace App.Templates.Orders;\n"
            + "\n"
            + "internal static class RequisiteTemplate\n"
            + "{\n"
            + "    internal static global::System.Collections.Generic.IEnumerator<object?> Steps(global::Templar.Rendering.TemplarWriter w, global::App.Orders.Requisite model)\n"
            + "    {\n"
            + "        w.Literal(\"requisite \");\n"
            + "        w.Text(model.Name);\n"
            + "        w.Literal(\"\\n{\\n    \");\n"
            + "        yield return w.List(model.Lines, global::App.Templates.Orders.LineTemplate.Steps);\n"
            + "        w.Literal(\"\\n}\");\n"
            + "        yield break;\n"
            + "    }\n"
            + "\n"
            + "    [global::System.Runtime.CompilerServices.ModuleInitializer]\n"
            + "    internal static void Register()\n"
            + "    {\n"
            + "        global::Templar.Rendering.RenderRegistry.Register<global::App.Orders.Requisite>(\"Orders/Requisite\", Steps);\n"
            + "    }\n"
            + "}\n";
        var line = "// <auto-generated/>\n"
            + "#nullable enable\n"
            + "\n"
            + "namespace App.Templates.Orders;\n"
            + "\n"
            + "internal static class LineTemplate\n"
            + "{\n"
            + "    internal static global::System.Collections.Generic.IEnumerator<object?> Steps(global::Templar.Rendering.TemplarWriter w, global::App.Orders.Line model)\n"
            + "    {\n"
            + "        w.Literal(\"line \");\n"
            + "        w.Text(model.Sku);\n"
            + "        yield break;\n"
            + "    }\n"
            + "\n"
            + "    [global::System.Runtime.CompilerServices.ModuleInitializer]\n"
            + "    internal static void Register()\n"
            + "    {\n"
            + "        global::Templar.Rendering.RenderRegistry.Register<global::App.Orders.Line>(\"Orders/Line\", Steps);\n"
            + "        global::Templar.Rendering.RenderRegistry.RegisterDefault<global::App.Orders.Line>(\"Orders/Line\");\n"
            + "    }\n"
            + "}\n";
        Assert.Equal(requisite, run.Sources["Orders.Requisite.g.cs"]);
        Assert.Equal(line, run.Sources["Orders.Line.g.cs"]);
    }

    [Fact]
    public void Template_with_no_spots_emits_a_renderer_with_no_writes_but_its_literal()
    {
        var run = GeneratorHarness.Generate(new[] { "namespace App; [Templar.Rendering.Template(\"Bare\")] public sealed record Bare;" },
                                            new[] { ("/proj/Templates/Bare.tpl", "no spots here") });

        Assert.Empty(run.Diagnostics);
        var source = run.Sources["Bare.g.cs"];
        Assert.Contains("        w.Literal(\"no spots here\");\n        yield break;\n", source);
        Assert.DoesNotContain("w.Text", source);
    }
}
