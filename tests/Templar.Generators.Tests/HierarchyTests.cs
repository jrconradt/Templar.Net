using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Templar.Generators.Tests;

// A declared type is resolved through its hierarchy at compile time: every concrete type of this project a value
// of it can be renders by the most-derived [Template] in its own chain. One resolution is a direct call to its
// pair's Steps; several are a type switch, one generated resolver per declared type in Templar.Resolvers.g.cs.
public sealed class HierarchyTests
{
    private const string Shapes = "namespace App;\n"
        + "using Templar.Rendering;\n"
        + "public interface IShape { string Name { get; } }\n"
        + "[Template(\"Circle\")] public sealed record Circle(string Name, int Radius) : IShape;\n"
        + "[Template(\"Square\")] public sealed record Square(string Name, int Side) : IShape;\n";

    private static readonly (string Path, string Text)[] ShapeTemplates =
    {
        ("/proj/Templates/Circle.tpl", "circle {{ name }} r{{ radius }}"),
        ("/proj/Templates/Square.tpl", "square {{ name }} s{{ side }}"),
    };

    private static GeneratorHarness.Run Generate(string source, params (string Path, string Text)[] templates)
    {
        return GeneratorHarness.Generate(new[] { Shapes, source }, ShapeTemplates.Concat(templates).ToArray());
    }

    [Fact]
    public void Interface_typed_property_renders_each_value_by_its_own_template()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "[Template(\"Card\")] public sealed record Card(IShape Main, IShape Other);\n"
            + "public static class Probe { public static string Run() => Compositor.ResolveTemplate(new Card(new Circle(\"a\", 1), new Square(\"b\", 2))); }\n";

        var run = Generate(source, ("/proj/Templates/Card.tpl", "main {{ main }}\nother {{ other }}"));

        Assert.Equal("main circle a r1\nother square b s2", GeneratorHarness.Probe(GeneratorHarness.Load(run)));
    }

    [Fact]
    public void Sequence_of_an_interface_renders_each_item_by_its_own_template_in_list_order()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "[Template(\"Drawing\")] public sealed record Drawing(Sequence<IShape> Shapes);\n"
            + "public static class Probe\n"
            + "{\n"
            + "    public static string Run() => Compositor.ResolveTemplate(new Drawing(Sequence.CommaList<IShape>([new Square(\"a\", 1), new Circle(\"b\", 2), new Square(\"c\", 3)])));\n"
            + "}\n";

        var run = Generate(source, ("/proj/Templates/Drawing.tpl", "drawing: {{ shapes }}"));

        Assert.Equal("drawing: square a s1, circle b r2, square c s3", GeneratorHarness.Probe(GeneratorHarness.Load(run)));
    }

    [Theory]
    [InlineData("public abstract record Shape(string Name);", "Shape(Name)")]
    [InlineData("public interface Shape { string Name { get; } }", "Shape")]
    public void Subtype_with_no_Template_renders_by_the_Template_of_its_base(string declaration, string baseList)
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "[Template(\"Shape\")] " + declaration + "\n"
            + $"[Template(\"Circle\")] public sealed record Circle(string Name, int Radius) : {baseList};\n"
            + $"public sealed record Dot(string Name) : {baseList};\n"
            + "[Template(\"Drawing\")] public sealed record Drawing(Sequence<Shape> Shapes);\n"
            + "public static class Probe\n"
            + "{\n"
            + "    public static string Run() => Compositor.ResolveTemplate(new Drawing(Sequence.CommaList<Shape>([new Circle(\"a\", 1), new Dot(\"b\")])));\n"
            + "}\n";

        var run = GeneratorHarness.Generate(new[] { source },
                                            new[]
                                            {
                                                ("/proj/Templates/Shape.tpl", "shape {{ name }}"),
                                                ("/proj/Templates/Circle.tpl", "circle {{ name }} r{{ radius }}"),
                                                ("/proj/Templates/Drawing.tpl", "{{ shapes }}"),
                                            });

        Assert.Equal("circle a r1, shape b", GeneratorHarness.Probe(GeneratorHarness.Load(run)));
    }

    [Fact]
    public void ResolveTemplate_on_a_base_typed_variable_renders_by_the_type_of_the_value()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "public static class Probe\n"
            + "{\n"
            + "    public static string Run()\n"
            + "    {\n"
            + "        IShape[] shapes = [new Circle(\"a\", 1), new Square(\"b\", 2)];\n"
            + "        return Compositor.ResolveTemplate(shapes[0]) + \"|\" + Compositor.ResolveTemplate(shapes[1]);\n"
            + "    }\n"
            + "}\n";

        var run = Generate(source);

        Assert.Equal("circle a r1|square b s2", GeneratorHarness.Probe(GeneratorHarness.Load(run)));
        Assert.Contains("            return global::Templar.Rendering.TemplarWriter.Render(model, global::App.Templates.Resolvers.App_IShapeResolver.Steps, options);\n",
                        run.Sources["Templar.Interceptors.g.cs"]);
    }

    [Fact]
    public void Resolver_file_holds_one_type_switch_per_declared_type_and_nothing_else()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "[Template(\"Card\")] public sealed record Card(IShape Main, Sequence<IShape> Shapes);\n"
            + "public static class Probe { public static string Run(IShape shape) => Compositor.ResolveTemplate(shape); }\n";

        var run = Generate(source, ("/proj/Templates/Card.tpl", "{{ main }}: {{ shapes }}"));

        Assert.Empty(run.Diagnostics);
        Assert.Equal(new[] { "Card.g.cs", "Circle.g.cs", "Square.g.cs", "Templar.Interceptors.g.cs", "Templar.Resolvers.g.cs" },
                     run.Sources.Keys.OrderBy(k => k, System.StringComparer.Ordinal).ToArray());
        var card = run.Sources["Card.g.cs"];
        Assert.Contains("        yield return w.Compose(model.Main, global::App.Templates.Resolvers.App_IShapeResolver.Steps);\n", card);
        Assert.Contains("        yield return w.List(model.Shapes, global::App.Templates.Resolvers.App_IShapeResolver.Steps);\n", card);
        var expected = "// <auto-generated/>\n"
            + "#nullable enable\n"
            + "\n"
            + "namespace App.Templates.Resolvers;\n"
            + "\n"
            + "internal static class App_IShapeResolver\n"
            + "{\n"
            + "    internal static global::System.Collections.Generic.IEnumerator<object?> Steps(global::Templar.Rendering.TemplarWriter w, global::App.IShape model)\n"
            + "    {\n"
            + "        return model switch\n"
            + "        {\n"
            + "            global::App.Circle m => global::App.Templates.CircleTemplate.Steps(w, m),\n"
            + "            global::App.Square m => global::App.Templates.SquareTemplate.Steps(w, m),\n"
            + "            _ => throw new global::System.InvalidOperationException(\"Type '\" + model.GetType() + \"' was not resolved for 'App.IShape' at compile time.\"),\n"
            + "        };\n"
            + "    }\n"
            + "}\n";
        Assert.Equal(expected, run.Sources["Templar.Resolvers.g.cs"]);

        foreach (var (hint, text) in run.Sources.Where(s => s.Key != "Templar.Resolvers.g.cs"))
        {
            var found = CSharpSyntaxTree.ParseText(text, GeneratorHarness.ParseOptions).GetRoot()
                .DescendantNodesAndSelf()
                .Where(n => EmissionTests.Forbidden.Contains(n.Kind()))
                .Select(n => $"{hint}: {n.Kind()} `{n}`")
                .ToList();
            Assert.True(found.Count == 0, string.Join("\n", found));
        }

        var resolvers = CSharpSyntaxTree.ParseText(run.Sources["Templar.Resolvers.g.cs"], GeneratorHarness.ParseOptions).GetRoot();
        var other = resolvers.DescendantNodesAndSelf()
            .Where(n => EmissionTests.Forbidden.Contains(n.Kind()))
            .Where(n => !n.IsKind(SyntaxKind.SwitchStatement) && !n.IsKind(SyntaxKind.SwitchExpression))
            .Select(n => $"{n.Kind()} `{n}`")
            .ToList();
        Assert.True(other.Count == 0, string.Join("\n", other));
        var arms = resolvers.DescendantNodes().OfType<SwitchExpressionArmSyntax>().ToList();
        Assert.NotEmpty(arms);
        foreach (var arm in arms)
        {
            Assert.True(arm.WhenClause is null
                        && (arm.Pattern is DeclarationPatternSyntax or TypePatternSyntax
                            || (arm.Pattern is DiscardPatternSyntax && arm.Expression is ThrowExpressionSyntax)),
                        $"`{arm}` is not a type pattern");
        }
        foreach (var label in resolvers.DescendantNodes().OfType<SwitchLabelSyntax>())
        {
            Assert.True(label is CasePatternSwitchLabelSyntax { WhenClause: null, Pattern: DeclarationPatternSyntax or TypePatternSyntax }
                        || (label is DefaultSwitchLabelSyntax && label.Parent is SwitchSectionSyntax section && section.Statements.All(s => s is ThrowStatementSyntax)),
                        $"`{label}` is not a type pattern");
        }
    }

    [Fact]
    public void Declared_type_with_one_candidate_is_a_direct_Steps_call_and_emits_no_resolver()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "[Template(\"Item\")] public sealed record Item(string Name);\n"
            + "[Template(\"Shape\")] public abstract record Shape(string Name);\n"
            + "public sealed record Dot(string Name) : Shape(Name);\n"
            + "[Template(\"Card\")] public sealed record Card(Item Lead, Sequence<Item> Items, Dot Spot);\n"
            + "public static class Probe\n"
            + "{\n"
            + "    public static string Run() => Compositor.ResolveTemplate(new Card(new Item(\"a\"), Sequence.CommaList([new Item(\"b\"), new Item(\"c\")]), new Dot(\"d\")));\n"
            + "}\n";

        var run = GeneratorHarness.Generate(new[] { source },
                                            new[]
                                            {
                                                ("/proj/Templates/Card.tpl", "{{ lead }} {{ items }} {{ spot }}"),
                                                ("/proj/Templates/Item.tpl", "item {{ name }}"),
                                                ("/proj/Templates/Shape.tpl", "shape {{ name }}"),
                                            });

        Assert.Empty(run.Diagnostics);
        var card = run.Sources["Card.g.cs"];
        Assert.Contains("        yield return w.Compose(model.Lead, global::App.Templates.ItemTemplate.Steps);\n", card);
        Assert.Contains("        yield return w.List(model.Items, global::App.Templates.ItemTemplate.Steps);\n", card);
        Assert.Contains("        yield return w.Compose(model.Spot, global::App.Templates.ShapeTemplate.Steps);\n", card);
        Assert.Contains("Steps(global::Templar.Rendering.TemplarWriter w, global::App.Dot model)", run.Sources["Shape.g.cs"]);
        Assert.DoesNotContain("Templar.Resolvers.g.cs", run.Sources.Keys);
        Assert.Equal("item a item b, item c shape d", GeneratorHarness.Probe(GeneratorHarness.Load(run)));
    }
}
