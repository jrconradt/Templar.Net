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
    public void Text_spots_render_their_property_values()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "[Template(\"Greeting\")] public sealed record Greeting(string Who, string Place);\n"
            + "public static class Probe { public static string Run() => Compositor.ResolveTemplate(new Greeting(\"Ada\", \"Templar\")); }\n";

        var rendered = Render(source, ("/proj/Templates/Greeting.tpl", "Hello {{ who }}, welcome to {{ place }}!"));

        Assert.Equal("Hello Ada, welcome to Templar!", rendered);
    }

    [Fact]
    public void Literal_quotes_newlines_tabs_and_escaped_braces_survive()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "[Template(\"Tricky\")] public sealed record Tricky(string X);\n"
            + "public static class Probe { public static string Run() => Compositor.ResolveTemplate(new Tricky(\"value\")); }\n";

        var rendered = Render(source, ("/proj/Templates/Tricky.tpl", "line1 \"quoted\"\nline2\twith \\{{ escaped {{ x }}"));

        Assert.Equal("line1 \"quoted\"\nline2\twith {{ escaped value", rendered);
    }

    [Fact]
    public void Carriage_returns_in_template_text_are_dropped()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "[Template(\"Crlf\")] public sealed record Crlf(string X);\n"
            + "public static class Probe { public static string Run() => Compositor.ResolveTemplate(new Crlf(\"b\")); }\n";

        var rendered = Render(source, ("/proj/Templates/Crlf.tpl", "a\r\n{{ x }}\r\nc"));

        Assert.Equal("a\nb\nc", rendered);
    }

    [Fact]
    public void Primitive_and_enum_spots_format_with_the_invariant_culture()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "public enum Mode { Fast, Slow }\n"
            + "[Template(\"Stats\")] public sealed class Stats { public int Count { get; init; } public double Ratio { get; init; } public bool On { get; init; } public Mode Mode { get; init; } public long? Missing { get; init; } }\n"
            + "public static class Probe { public static string Run() => Compositor.ResolveTemplate(new Stats { Count = 3, Ratio = 1.5, On = true, Mode = Mode.Slow }); }\n";

        var rendered = Render(source, ("/proj/Templates/Stats.tpl", "{{ count }}|{{ ratio }}|{{ on }}|{{ mode }}|{{ missing }}"));

        Assert.Equal("3|1.5|True|Slow|", rendered);
    }

    [Fact]
    public void Template_typed_spot_renders_by_its_template_at_the_spot_column()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "[Template(\"Outer\")] public sealed record Outer(Inner Inside);\n"
            + "[Template(\"Inner\")] public sealed record Inner(string Name);\n"
            + "public static class Probe { public static string Run() => Compositor.ResolveTemplate(new Outer(new Inner(\"x\"))); }\n";

        var rendered = Render(source,
                              ("/proj/Templates/Outer.tpl", "before\n    {{ inside }}\nafter"),
                              ("/proj/Templates/Inner.tpl", "L1 {{ name }}\nL2"));

        Assert.Equal("before\n    L1 x\n    L2\nafter", rendered);
    }

    [Fact]
    public void Null_spot_renders_nothing_and_its_line_is_dropped()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "[Template(\"Outer\")] public sealed record Outer(Inner? Inside);\n"
            + "[Template(\"Inner\")] public sealed record Inner;\n"
            + "public static class Probe { public static string Run() => Compositor.ResolveTemplate(new Outer(null)); }\n";

        var rendered = Render(source,
                              ("/proj/Templates/Outer.tpl", "before\n    {{ inside }}\nafter"),
                              ("/proj/Templates/Inner.tpl", "inner"));

        Assert.Equal("before\nafter", rendered);
    }

    [Fact]
    public void Sequence_joins_by_its_own_separator_and_a_plain_list_by_newline()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "[Template(\"Item\")] public sealed record Item(string Name);\n"
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
            + "        Item[] items = [new Item(\"a\"), new Item(\"b\")];\n"
            + "        return Compositor.ResolveTemplate(new Holder { Args = Sequence.CommaList(items), Blocks = Sequence.BlankLines(items), Lines = items, Notes = [\"n1\", \"n2\"] }, \"Holder\");\n"
            + "    }\n"
            + "}\n";

        var rendered = Render(source,
                              ("/proj/Templates/Holder.tpl", "call({{ args }})\n  {{ blocks }}\n  {{ lines }}\n  {{ notes }}"),
                              ("/proj/Templates/Item.tpl", "item {{ name }}"));

        Assert.Equal("call(item a, item b)\n  item a\n\n  item b\n  item a\n  item b\n  n1\n  n2", rendered);
    }

    [Fact]
    public void Empty_list_renders_nothing_and_its_line_is_dropped()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "[Template(\"Item\")] public sealed record Item;\n"
            + "public sealed record Holder(Sequence<Item> Items);\n"
            + "public static class Probe { public static string Run() => Compositor.ResolveTemplate(new Holder(Sequence.Lines(new Item[0])), \"Holder\"); }\n";

        var rendered = Render(source,
                              ("/proj/Templates/Holder.tpl", "before\n    {{ items }}\nafter"),
                              ("/proj/Templates/Item.tpl", "item"));

        Assert.Equal("before\nafter", rendered);
    }

    [Fact]
    public void List_separator_newlines_follow_the_render_options()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "[Template(\"Item\")] public sealed record Item(string Name);\n"
            + "public sealed record Holder(Sequence<Item> Items);\n"
            + "public static class Probe\n"
            + "{\n"
            + "    public static string Run() => Compositor.ResolveTemplate(new Holder(Sequence.BlankLines([new Item(\"a\"), new Item(\"b\")])),\n"
            + "                                                             \"Holder\",\n"
            + "                                                             new RenderOptions { Newline = \"\\r\\n\" });\n"
            + "}\n";

        var rendered = Render(source,
                              ("/proj/Templates/Holder.tpl", "start\n{{ items }}\nend"),
                              ("/proj/Templates/Item.tpl", "{{ name }}"));

        Assert.Equal("start\r\na\r\n\r\nb\r\nend", rendered);
    }

    [Fact]
    public void List_of_lists_nests_each_by_its_own_separator()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "[Template(\"Item\")] public sealed record Item(string Name);\n"
            + "[Template(\"Holder\")] public sealed record Holder(Sequence<Sequence<Item>> Rows);\n"
            + "public static class Probe\n"
            + "{\n"
            + "    public static string Run() => Compositor.ResolveTemplate(new Holder(\n"
            + "        Sequence.Lines([Sequence.CommaList([new Item(\"a\"), new Item(\"b\")]),\n"
            + "                        Sequence.CommaList([new Item(\"c\"), new Item(\"d\")])])));\n"
            + "}\n";

        var rendered = Render(source,
                              ("/proj/Templates/Holder.tpl", "{{ rows }}"),
                              ("/proj/Templates/Item.tpl", "item:{{ name }}"));

        Assert.Equal("item:a, item:b\nitem:c, item:d", rendered);
    }

    [Fact]
    public void Interface_spot_renders_each_value_by_its_runtime_type()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "public interface ILine { string Sku { get; } }\n"
            + "[Template(\"Stocked\")] public sealed record Stocked(string Sku) : ILine;\n"
            + "[Template(\"Backordered\")] public sealed record Backordered(string Sku, string Eta) : ILine;\n"
            + "[Template(\"Requisite\")] public sealed record Requisite(string Name, ILine First, Sequence<ILine> Lines);\n"
            + "public static class Probe\n"
            + "{\n"
            + "    public static string Run()\n"
            + "    {\n"
            + "        ILine[] lines = [new Stocked(\"A-100\"), new Backordered(\"B-200\", \"May\")];\n"
            + "        return Compositor.ResolveTemplate(new Requisite(\"R-1\", lines[1], Sequence.Lines(lines)));\n"
            + "    }\n"
            + "}\n";

        var rendered = Render(source,
                              ("/proj/Templates/Requisite.tpl", "requisite {{ name }} first {{ first }}\n{\n    {{ lines }}\n}"),
                              ("/proj/Templates/Stocked.tpl", "stocked {{ sku }}"),
                              ("/proj/Templates/Backordered.tpl", "backordered {{ sku }}\n    eta {{ eta }}"));

        var column = new string(' ', "requisite R-1 first ".Length);
        Assert.Equal("requisite R-1 first backordered B-200\n" + column + "    eta May\n{\n    stocked A-100\n    backordered B-200\n        eta May\n}", rendered);
    }

    [Fact]
    public void Interface_spot_holding_a_runtime_type_with_no_registered_template_throws()
    {
        var library = "namespace App;\n"
            + "public interface IShape { }\n"
            + "[Templar.Rendering.Template(\"Circle\")] public sealed record Circle : IShape;\n"
            + "[Templar.Rendering.Template(\"Frame\")] public sealed record Frame(IShape Shape);\n";
        var run = GeneratorHarness.Generate(new[] { library },
                                            new[] { ("/proj/Templates/Frame.tpl", "[{{ shape }}]"), ("/proj/Templates/Circle.tpl", "circle") });
        Assert.Empty(run.Diagnostics);

        var outsider = "namespace Outside;\n"
            + "public sealed class Square : App.IShape { }\n"
            + "public static class Probe\n"
            + "{\n"
            + "    public static string Circle() => Templar.Rendering.Compositor.ResolveTemplate(new App.Frame(new App.Circle()));\n"
            + "    public static string Square() => Templar.Rendering.Compositor.ResolveTemplate(new App.Frame(new Square()));\n"
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
    public void Template_type_from_a_referenced_assembly_renders_by_its_registered_default()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "[Template(\"Header\")] public sealed record Header(Templar.Presets.Using Import);\n"
            + "public static class Probe { public static string Run() => Compositor.ResolveTemplate(new Header(new Templar.Presets.Using { Name = \"System\" })); }\n";

        var rendered = Render(source, ("/proj/Templates/Header.tpl", "// header\n{{ import }}"));

        Assert.Equal("// header\nusing System;", rendered);
    }

    [Fact]
    public void Five_hundred_deep_nesting_renders_without_recursion()
    {
        var source = "namespace App;\n"
            + "using Templar.Rendering;\n"
            + "[Template(\"Nest\")] public sealed record Nest(Nest? Inner, string Leaf = \"\");\n"
            + "public static class Probe\n"
            + "{\n"
            + "    public static string Run()\n"
            + "    {\n"
            + "        var nest = new Nest(null, \"X\");\n"
            + "        for (int i = 0; i < 500; i++) { nest = new Nest(nest); }\n"
            + "        return Compositor.ResolveTemplate(nest);\n"
            + "    }\n"
            + "}\n";

        var rendered = Render(source, ("/proj/Templates/Nest.tpl", "[{{inner}}{{leaf}}]"));

        Assert.Equal(new string('[', 501) + "X" + new string(']', 501), rendered);
    }
}
