using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Templar.Generators.Tests;

public class EmissionTests
{
    private const string Model = "namespace App;\n"
        + "public enum Color { Red, Green }\n"
        + "public interface IShape { }\n"
        + "public sealed class Circle : IShape { public int Radius { get; init; } }\n"
        + "public sealed class Square : IShape { public int Side { get; init; } }\n"
        + "public sealed class Item { public required string Name { get; init; } }\n"
        + "public sealed class Card\n"
        + "{\n"
        + "    public required string Title { get; init; }\n"
        + "    public int Count { get; init; }\n"
        + "    public double? Ratio { get; init; }\n"
        + "    public Color Color { get; init; }\n"
        + "    public Item? Lead { get; init; }\n"
        + "    public required Templar.Rendering.Sequence<Item> Items { get; init; }\n"
        + "    public System.Collections.Generic.IReadOnlyList<string> Notes { get; init; } = [];\n"
        + "    public int[] Numbers { get; init; } = [];\n"
        + "    public required IShape Shape { get; init; }\n"
        + "    public System.Collections.Generic.IEnumerable<IShape> Shapes { get; init; } = [];\n"
        + "    public System.Collections.Generic.IEnumerable<Templar.Rendering.Sequence<Item>> Groups { get; init; } = [];\n"
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
                                         });
    }

    [Fact]
    public void Each_hole_write_is_chosen_from_the_declared_type_of_its_property()
    {
        var run = Generate();
        Assert.Empty(run.Diagnostics);
        var source = run.Sources["App.Card.g.cs"];

        Assert.Contains("        w.Text(model.Title);\n", source);
        Assert.Contains("        w.Format(model.Count);\n", source);
        Assert.Contains("        w.Format(model.Ratio);\n", source);
        Assert.Contains("        w.Format(model.Color);\n", source);
        Assert.Contains("        yield return w.Compose(model.Lead, global::App.ItemRenderer.Steps);\n", source);
        Assert.Contains("        yield return w.List(model.Items, global::App.ItemRenderer.Steps);\n", source);
        Assert.Contains("        yield return w.List(model.Notes, global::Templar.Rendering.TemplarWriter.TextItem);\n", source);
        Assert.Contains("        yield return w.List(model.Numbers, global::Templar.Rendering.TemplarWriter.FormatItem<int>);\n", source);
        Assert.Contains("        yield return w.Compose(model.Shape, global::Templar.Rendering.RenderRegistry.Steps);\n", source);
        Assert.Contains("        yield return w.List(model.Shapes, global::Templar.Rendering.RenderRegistry.Steps);\n", source);
        Assert.Contains("        yield return w.List(model.Groups, (w1, m1) => global::Templar.Rendering.TemplarWriter.ListItem(w1, m1, global::App.ItemRenderer.Steps));\n", source);
        Assert.Contains("        w.Text(model.@class);\n", source);
        Assert.Contains("        w.Literal(\"\\nclass \");\n", source);
        Assert.Contains("        w.Literal(\"{{ escaped }}\");\n", source);
        Assert.Contains("        w.Literal(\"card \\\"\");\n", source);
    }

    [Fact]
    public void Generated_renderers_hold_no_conditional_and_no_loop()
    {
        var run = Generate();
        Assert.Empty(run.Diagnostics);
        Assert.Equal(4, run.Sources.Count);

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
    public void Emits_the_render_entry_the_flat_renderer_and_the_registration()
    {
        var run = GeneratorHarness.Generate(new[]
                                            {
                                                "namespace App.Orders; public sealed class Requisite { public required string Name { get; init; } public required Templar.Rendering.Sequence<Line> Lines { get; init; } }",
                                                "namespace App.Orders; public sealed class Line { public required string Sku { get; init; } }",
                                            },
                                            new[]
                                            {
                                                ("/proj/Templates/Orders/Requisite.tpl", "requisite {{ name }}\n{\n    {{ lines }}\n}"),
                                                ("/proj/Templates/Orders/Line.tpl", "line {{ sku }}"),
                                            });

        Assert.Empty(run.Diagnostics);
        var expected = "// <auto-generated/>\n"
            + "#nullable enable\n"
            + "\n"
            + "namespace App.Orders;\n"
            + "\n"
            + "public static class RequisiteRenderer\n"
            + "{\n"
            + "    public static string Render(this global::App.Orders.Requisite model, global::Templar.Rendering.RenderOptions? options = null)\n"
            + "    {\n"
            + "        return global::Templar.Rendering.TemplarWriter.Render(model, Steps, options);\n"
            + "    }\n"
            + "\n"
            + "    internal static global::System.Collections.Generic.IEnumerator<object?> Steps(global::Templar.Rendering.TemplarWriter w, global::App.Orders.Requisite model)\n"
            + "    {\n"
            + "        w.Literal(\"requisite \");\n"
            + "        w.Text(model.Name);\n"
            + "        w.Literal(\"\\n{\\n    \");\n"
            + "        yield return w.List(model.Lines, global::App.Orders.LineRenderer.Steps);\n"
            + "        w.Literal(\"\\n}\");\n"
            + "        yield break;\n"
            + "    }\n"
            + "\n"
            + "    [global::System.Runtime.CompilerServices.ModuleInitializer]\n"
            + "    internal static void Register()\n"
            + "    {\n"
            + "        global::Templar.Rendering.RenderRegistry.Register<global::App.Orders.Requisite>(Steps);\n"
            + "    }\n"
            + "}\n";
        Assert.Equal(expected, run.Sources["App.Orders.Requisite.g.cs"]);
    }

    [Fact]
    public void Template_with_no_holes_emits_a_renderer_with_no_writes_but_its_literal()
    {
        var run = GeneratorHarness.Generate(new[] { "namespace App; public sealed class Bare { }" },
                                            new[] { ("/proj/Templates/Bare.tpl", "no placeholders here") });

        Assert.Empty(run.Diagnostics);
        var source = run.Sources["App.Bare.g.cs"];
        Assert.Contains("        w.Literal(\"no placeholders here\");\n        yield break;\n", source);
        Assert.DoesNotContain("w.Text", source);
    }
}
