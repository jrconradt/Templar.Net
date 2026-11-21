using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Templar.Generators.Tests;

public class EmissionTests
{
    private const string Model = "namespace App;\n"
        + "using Templar.Rendering;\n"
        + "public enum Color { Red, Green }\n"
        + "[Template(\"Item\")] public sealed record Item(string Name);\n"
        + "[Template(\"Tag\")] public sealed record Tag(string Name);\n"
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
        + "    public System.Collections.Generic.IEnumerable<Sequence<Item>> Groups { get; init; } = [];\n"
        + "    public Tag? Badge { get; init; }\n"
        + "    public required Sequence<Tag> Tags { get; init; }\n"
        + "    public string @class => \"c\";\n"
        + "}\n"
        + "public static class Probe\n"
        + "{\n"
        + "    public static string Named(Card card) => Compositor.ResolveTemplate(card, \"Card\");\n"
        + "    public static string Default(Card card) => Compositor.ResolveTemplate(card);\n"
        + "}\n";

    private const string CardTemplate = "card \"{{ title }}\"\n"
        + "count {{ count }} ratio {{ ratio }} color {{ color }}\n"
        + "{{# a comment }}lead {{ lead }}\n"
        + "items {{ items }}\n"
        + "notes {{ notes }}\n"
        + "numbers {{ numbers }}\n"
        + "groups {{ groups }}\n"
        + "badge {{ badge }} tags {{ tags }}\n"
        + "class {{ CLASS }}\\{{ escaped \\}}";

    private static GeneratorHarness.Run Generate()
    {
        return GeneratorHarness.Generate(new[] { Model },
                                         new[]
                                         {
                                             ("/proj/Templates/Card.tpl", CardTemplate),
                                             ("/proj/Templates/Item.tpl", "{{ name }}"),
                                             ("/proj/Templates/Tag.tpl", "#{{ name }}"),
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
        Assert.Contains("        yield return w.List(model.Groups, (w1, m1) => global::Templar.Rendering.TemplarWriter.ListItem(w1, m1, global::App.Templates.ItemTemplate.Steps));\n", source);
        Assert.Contains("        yield return w.Compose(model.Badge, global::App.Templates.TagTemplate.Steps);\n", source);
        Assert.Contains("        yield return w.List(model.Tags, global::App.Templates.TagTemplate.Steps);\n", source);
        Assert.Contains("        w.Text(model.@class);\n", source);
        Assert.Contains("        w.Literal(\"\\nclass \");\n", source);
        Assert.Contains("        w.Literal(\"{{ escaped }}\");\n", source);
        Assert.Contains("        w.Literal(\"card \\\"\");\n", source);
    }

    [Fact]
    public void Emitted_renderers_hold_no_conditional_and_no_loop()
    {
        var run = Generate();
        Assert.Empty(run.Diagnostics);
        Assert.Equal(new[] { "Card.g.cs", "Item.g.cs", "Tag.g.cs", "Templar.Interceptors.g.cs" },
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
    public void Emitted_code_holds_no_registry_and_no_module_initializer()
    {
        var run = Generate();
        Assert.Empty(run.Diagnostics);
        Assert.NotEmpty(run.Sources);

        foreach (var (hint, text) in run.Sources)
        {
            Assert.False(text.Contains("RenderRegistry"), $"{hint} names RenderRegistry");
            Assert.False(text.Contains("ModuleInitializer"), $"{hint} names ModuleInitializer");
        }
    }

    [Fact]
    public void Interface_typed_spot_renders_the_interface_template_bound_at_compile_time()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "[Template(\"Shape\")] public interface IShape { string Name { get; } }\n"
            + "public sealed record Circle(string Name, int Radius) : IShape;\n"
            + "[Template(\"Card\")] public sealed record Card(IShape Main, Sequence<IShape> Shapes);\n"
            + "public static class Probe\n"
            + "{\n"
            + "    public static string Run() => Compositor.ResolveTemplate(new Card(new Circle(\"a\", 1), Sequence.CommaList<IShape>([new Circle(\"b\", 2), new Circle(\"c\", 3)])));\n"
            + "}\n";
        var run = GeneratorHarness.Generate(new[] { source },
                                            new[]
                                            {
                                                ("/proj/Templates/Card.tpl", "main {{ main }} shapes {{ shapes }}"),
                                                ("/proj/Templates/Shape.tpl", "shape {{ name }}"),
                                            });

        Assert.Empty(run.Diagnostics);
        var card = run.Sources["Card.g.cs"];
        Assert.Contains("        yield return w.Compose(model.Main, global::App.Templates.ShapeTemplate.Steps);\n", card);
        Assert.Contains("        yield return w.List(model.Shapes, global::App.Templates.ShapeTemplate.Steps);\n", card);
        Assert.Contains("Steps(global::Templar.Rendering.TemplarWriter w, global::App.IShape model)", run.Sources["Shape.g.cs"]);
        Assert.Equal("main shape a shapes shape b, shape c", GeneratorHarness.Probe(GeneratorHarness.Load(run)));
    }

    [Fact]
    public void Emits_one_class_per_template_with_a_renderer_per_pair()
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

    private static string[] InterceptsLocations(GeneratorHarness.Run run, string source)
    {
        var tree = run.Output.SyntaxTrees.Single(t => t.ToString() == source);
        var model = run.Output.GetSemanticModel(tree);
        return tree.GetRoot()
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(call => model.GetInterceptableLocation(call)!)
            .Select(location => $"        [global::System.Runtime.CompilerServices.InterceptsLocation({location.Version}, \"{location.Data}\")]\n")
            .ToArray();
    }

    [Fact]
    public void Named_call_sites_are_intercepted_by_one_method_that_renders_their_pair_directly()
    {
        var probe = "namespace App;\n"
            + "public static class Probe\n"
            + "{\n"
            + "    public static string Run(Book book) => Templar.Rendering.Compositor.ResolveTemplate(book, \"Card\");\n"
            + "    public static string Again(Book book) => Templar.Rendering.Compositor.ResolveTemplate(book, \"Card\", null);\n"
            + "}\n";
        var run = GeneratorHarness.Generate(new[] { "namespace App; public sealed record Book(string Title);", probe },
                                            new[] { ("/proj/Templates/Card.tpl", "card {{ title }}") });

        Assert.Empty(run.Diagnostics);
        var calls = InterceptsLocations(run, probe);
        Assert.Equal(2, calls.Length);
        var expected = "// <auto-generated/>\n"
            + "#nullable enable\n"
            + "\n"
            + "namespace System.Runtime.CompilerServices\n"
            + "{\n"
            + "    [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]\n"
            + "    file sealed class InterceptsLocationAttribute : global::System.Attribute\n"
            + "    {\n"
            + "        public InterceptsLocationAttribute(int version, string data)\n"
            + "        {\n"
            + "        }\n"
            + "    }\n"
            + "}\n"
            + "\n"
            + "namespace Templar.Generated\n"
            + "{\n"
            + "    file static class Interceptors\n"
            + "    {\n"
            + calls[0]
            + calls[1]
            + "        internal static string ResolveTemplate0(global::App.Book model, string template, global::Templar.Rendering.RenderOptions? options = null)\n"
            + "        {\n"
            + "            return global::Templar.Rendering.TemplarWriter.Render(model, global::App.Templates.CardTemplate.Steps, options);\n"
            + "        }\n"
            + "    }\n"
            + "}\n";
        var interceptors = run.Sources["Templar.Interceptors.g.cs"];
        Assert.Equal(expected, interceptors);
        Assert.DoesNotContain("RenderRegistry", interceptors);
    }

    [Fact]
    public void Default_call_site_is_intercepted_by_a_method_that_renders_the_Template_pair_directly()
    {
        var probe = "namespace App.Orders;\n"
            + "public static class Probe\n"
            + "{\n"
            + "    public static string Run(Line line) => Templar.Rendering.Compositor.ResolveTemplate(line);\n"
            + "}\n";
        var run = GeneratorHarness.Generate(new[] { "namespace App.Orders; [Templar.Rendering.Template(\"Orders/Line\")] public sealed record Line(string Sku);", probe },
                                            new[] { ("/proj/Templates/Orders/Line.tpl", "line {{ sku }}") });

        Assert.Empty(run.Diagnostics);
        var call = Assert.Single(InterceptsLocations(run, probe));
        var interceptor = call
            + "        internal static string ResolveTemplate0(global::App.Orders.Line model, global::Templar.Rendering.RenderOptions? options = null)\n"
            + "        {\n"
            + "            return global::Templar.Rendering.TemplarWriter.Render(model, global::App.Templates.Orders.LineTemplate.Steps, options);\n"
            + "        }\n";
        var interceptors = run.Sources["Templar.Interceptors.g.cs"];
        Assert.Contains(interceptor, interceptors);
        Assert.DoesNotContain("RenderRegistry", interceptors);
    }
}
