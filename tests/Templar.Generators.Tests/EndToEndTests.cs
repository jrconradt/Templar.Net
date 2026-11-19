using Templar.Rendering;
using Xunit;

namespace Templar.Generators.Tests;

public class EndToEndTests
{
    private static string Render(string source, params (string Path, string Text)[] templates)
    {
        var run = GeneratorHarness.Generate(new[] { source }, templates);
        return GeneratorHarness.Probe(GeneratorHarness.Load(run));
    }

    [Fact]
    public void Generated_renderer_matches_the_interpreted_template()
    {
        var source = "namespace App;\n"
            + "public sealed class Greeting { public required string Who { get; init; } public required string Place { get; init; } }\n"
            + "public static class Probe { public static string Run() => new Greeting { Who = \"Ada\", Place = \"Templar\" }.Render(); }\n";

        var rendered = Render(source, ("/proj/Greeting.tpl", "Hello {{ who }}, welcome to {{ place }}!"));

        var interpreted = Template.Parse("Hello {{ who }}, welcome to {{ place }}!").Set("who", "Ada").Set("place", "Templar").Render();
        Assert.Equal(interpreted, rendered);
        Assert.Equal("Hello Ada, welcome to Templar!", rendered);
    }

    [Fact]
    public void Literal_quotes_newlines_tabs_and_escaped_braces_survive()
    {
        var source = "namespace App;\n"
            + "public sealed class Tricky { public required string X { get; init; } }\n"
            + "public static class Probe { public static string Run() => new Tricky { X = \"value\" }.Render(); }\n";

        var rendered = Render(source, ("/proj/Tricky.tpl", "line1 \"quoted\"\nline2\twith \\{{ escaped {{ x }}"));

        Assert.Equal("line1 \"quoted\"\nline2\twith {{ escaped value", rendered);
    }

    [Fact]
    public void Primitive_and_enum_holes_format_with_the_invariant_culture()
    {
        var source = "namespace App;\n"
            + "public enum Mode { Fast, Slow }\n"
            + "public sealed class Stats { public int Count { get; init; } public double Ratio { get; init; } public bool On { get; init; } public Mode Mode { get; init; } public long? Missing { get; init; } }\n"
            + "public static class Probe { public static string Run() => new Stats { Count = 3, Ratio = 1.5, On = true, Mode = Mode.Slow }.Render(); }\n";

        var rendered = Render(source, ("/proj/Stats.tpl", "{{ count }}|{{ ratio }}|{{ on }}|{{ mode }}|{{ missing }}"));

        Assert.Equal("3|1.5|True|Slow|", rendered);
    }

    [Fact]
    public void Mapped_type_hole_renders_by_its_own_template_at_the_hole_column()
    {
        var source = "namespace App;\n"
            + "public sealed class Outer { public required Inner Inside { get; init; } }\n"
            + "public sealed class Inner { public required string Name { get; init; } }\n"
            + "public static class Probe { public static string Run() => new Outer { Inside = new Inner { Name = \"x\" } }.Render(); }\n";

        var rendered = Render(source,
                              ("/proj/Outer.tpl", "before\n    {{ inside }}\nafter"),
                              ("/proj/Inner.tpl", "L1 {{ name }}\nL2"));

        Assert.Equal("before\n    L1 x\n    L2\nafter", rendered);
    }

    [Fact]
    public void Null_mapped_hole_renders_nothing_and_its_line_is_dropped()
    {
        var source = "namespace App;\n"
            + "public sealed class Outer { public Inner? Inside { get; init; } }\n"
            + "public sealed class Inner { }\n"
            + "public static class Probe { public static string Run() => new Outer().Render(); }\n";

        var rendered = Render(source,
                              ("/proj/Outer.tpl", "before\n    {{ inside }}\nafter"),
                              ("/proj/Inner.tpl", "inner"));

        Assert.Equal("before\nafter", rendered);
    }

    [Fact]
    public void Sequence_joins_by_its_own_separator_and_a_plain_list_by_newline()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "public sealed class Item { public required string Name { get; init; } }\n"
            + "public sealed class Holder\n"
            + "{\n"
            + "    public required Sequence<Item> Args { get; init; }\n"
            + "    public required Sequence<Item> Blocks { get; init; }\n"
            + "    public required System.Collections.Generic.IReadOnlyList<Item> Lines { get; init; }\n"
            + "    public required System.Collections.Generic.IEnumerable<string> Notes { get; init; }\n"
            + "}\n"
            + "public static class Probe\n"
            + "{\n"
            + "    public static string Run()\n"
            + "    {\n"
            + "        Item[] items = [new Item { Name = \"a\" }, new Item { Name = \"b\" }];\n"
            + "        return new Holder { Args = Sequence.CommaList(items), Blocks = Sequence.BlankLines(items), Lines = items, Notes = [\"n1\", \"n2\"] }.Render();\n"
            + "    }\n"
            + "}\n";

        var rendered = Render(source,
                              ("/proj/Holder.tpl", "call({{ args }})\n  {{ blocks }}\n  {{ lines }}\n  {{ notes }}"),
                              ("/proj/Item.tpl", "item {{ name }}"));

        Assert.Equal("call(item a, item b)\n  item a\n\n  item b\n  item a\n  item b\n  n1\n  n2", rendered);
    }

    [Fact]
    public void Empty_list_renders_nothing_and_its_line_is_dropped()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "public sealed class Item { }\n"
            + "public sealed class Holder { public required Sequence<Item> Items { get; init; } }\n"
            + "public static class Probe { public static string Run() => new Holder { Items = Sequence.Lines(new Item[0]) }.Render(); }\n";

        var rendered = Render(source,
                              ("/proj/Holder.tpl", "before\n    {{ items }}\nafter"),
                              ("/proj/Item.tpl", "item"));

        Assert.Equal("before\nafter", rendered);
    }

    [Fact]
    public void List_separator_newlines_follow_the_render_options()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "public sealed class Item { public required string Name { get; init; } }\n"
            + "public sealed class Holder { public required Sequence<Item> Items { get; init; } }\n"
            + "public static class Probe\n"
            + "{\n"
            + "    public static string Run() => new Holder { Items = Sequence.BlankLines([new Item { Name = \"a\" }, new Item { Name = \"b\" }]) }\n"
            + "        .Render(new RenderOptions { Newline = \"\\r\\n\" });\n"
            + "}\n";

        var rendered = Render(source,
                              ("/proj/Holder.tpl", "start\n{{ items }}\nend"),
                              ("/proj/Item.tpl", "{{ name }}"));

        Assert.Equal("start\r\na\r\n\r\nb\r\nend", rendered);
    }

    [Fact]
    public void List_of_lists_nests_each_by_its_own_separator()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "public sealed class Item { public required string Name { get; init; } }\n"
            + "public sealed class Holder { public required Sequence<Sequence<Item>> Rows { get; init; } }\n"
            + "public static class Probe\n"
            + "{\n"
            + "    public static string Run() => new Holder\n"
            + "    {\n"
            + "        Rows = Sequence.Lines([Sequence.CommaList([new Item { Name = \"a\" }, new Item { Name = \"b\" }]),\n"
            + "                               Sequence.CommaList([new Item { Name = \"c\" }, new Item { Name = \"d\" }])]),\n"
            + "    }.Render();\n"
            + "}\n";

        var rendered = Render(source,
                              ("/proj/Holder.tpl", "{{ rows }}"),
                              ("/proj/Item.tpl", "item:{{ name }}"));

        Assert.Equal("item:a, item:b\nitem:c, item:d", rendered);
    }

    [Fact]
    public void Interface_hole_renders_each_value_by_its_runtime_type()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "public interface ILine { string Sku { get; } }\n"
            + "public sealed class Stocked : ILine { public required string Sku { get; init; } }\n"
            + "public sealed class Backordered : ILine { public required string Sku { get; init; } public required string Eta { get; init; } }\n"
            + "public sealed class Requisite { public required string Name { get; init; } public required ILine First { get; init; } public required Sequence<ILine> Lines { get; init; } }\n"
            + "public static class Probe\n"
            + "{\n"
            + "    public static string Run()\n"
            + "    {\n"
            + "        ILine[] lines = [new Stocked { Sku = \"A-100\" }, new Backordered { Sku = \"B-200\", Eta = \"May\" }];\n"
            + "        return new Requisite { Name = \"R-1\", First = lines[1], Lines = Sequence.Lines(lines) }.Render();\n"
            + "    }\n"
            + "}\n";

        var rendered = Render(source,
                              ("/proj/Requisite.tpl", "requisite {{ name }} first {{ first }}\n{\n    {{ lines }}\n}"),
                              ("/proj/Stocked.tpl", "stocked {{ sku }}"),
                              ("/proj/Backordered.tpl", "backordered {{ sku }}\n    eta {{ eta }}"));

        var column = new string(' ', "requisite R-1 first ".Length);
        Assert.Equal("requisite R-1 first backordered B-200\n" + column + "    eta May\n{\n    stocked A-100\n    backordered B-200\n        eta May\n}", rendered);
    }

    [Fact]
    public void Interface_hole_holding_an_unregistered_runtime_type_throws()
    {
        var library = "namespace App;\n"
            + "public interface IShape { }\n"
            + "public sealed class Circle : IShape { }\n"
            + "public sealed class Frame { public required IShape Shape { get; init; } }\n";
        var run = GeneratorHarness.Generate(new[] { library },
                                            new[] { ("/proj/Frame.tpl", "[{{ shape }}]"), ("/proj/Circle.tpl", "circle") });
        Assert.Empty(run.Diagnostics);

        var outsider = "namespace Outside;\n"
            + "public sealed class Square : App.IShape { }\n"
            + "public static class Probe\n"
            + "{\n"
            + "    public static string Circle() => App.FrameRenderer.Render(new App.Frame { Shape = new App.Circle() });\n"
            + "    public static string Square() => App.FrameRenderer.Render(new App.Frame { Shape = new Square() });\n"
            + "}\n";
        var outsiderCompilation = GeneratorHarness.Compile(new[] { outsider }, run.Output.ToMetadataReference());
        var loaded = GeneratorHarness.LoadTogether(run.Output, outsiderCompilation);
        var probe = loaded[1].GetType("Outside.Probe")!;

        Assert.Equal("[circle]", (string)probe.GetMethod("Circle")!.Invoke(null, null)!);
        var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => probe.GetMethod("Square")!.Invoke(null, null));
        var inner = Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Contains("Outside.Square", inner.Message);
    }

    [Fact]
    public void Five_hundred_deep_nesting_renders_without_recursion()
    {
        var source = "namespace App;\n"
            + "public sealed class Nest { public Nest? Inner { get; init; } public string Leaf { get; init; } = \"\"; }\n"
            + "public static class Probe\n"
            + "{\n"
            + "    public static string Run()\n"
            + "    {\n"
            + "        var nest = new Nest { Leaf = \"X\" };\n"
            + "        for (int i = 0; i < 500; i++) { nest = new Nest { Inner = nest }; }\n"
            + "        return nest.Render();\n"
            + "    }\n"
            + "}\n";

        var rendered = Render(source, ("/proj/Nest.tpl", "[{{inner}}{{leaf}}]"));

        Assert.Equal(new string('[', 501) + "X" + new string(']', 501), rendered);
    }
}
