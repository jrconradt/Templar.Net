using Templar.Rendering;
using Templar.Tests.Models.Engine;
using Xunit;

namespace Templar.Tests;

public class EngineCoverageTests
{
    [Fact]
    public void Mapped_type_value_at_line_start()
    {
        Assert.Equal("hi", new LitHolder { C = new Lit { V = "hi" } }.Render());
    }

    [Fact]
    public void List_value_at_line_start()
    {
        Assert.Equal("a\nb", new LitList { Items = [new Lit { V = "a" }, new Lit { V = "b" }] }.Render());
    }

    [Fact]
    public void Fallback_value_renders_to_string()
    {
        var t = Template.Parse("{{ x }}");
        t["x"] = 42;
        Assert.Equal("42", t.Render());
    }

    [Fact]
    public void Multiline_value_normalizes_crlf()
    {
        var t = Template.Parse("  {{ x }}");
        t["x"] = "a\r\nb";
        Assert.Equal("  a\n  b", t.Render());
    }

    [Fact]
    public void Tag_spanning_a_newline_parses_and_renders()
    {
        var t = Template.Parse("a{{ x\n}}b");
        t["x"] = "Z";
        Assert.Equal("aZb", t.Render());
    }

    [Fact]
    public void Indexer_get_returns_value_or_null()
    {
        var t = Template.Parse("x");
        Assert.Null(t["missing"]);
        t["k"] = "v";
        Assert.Equal("v", t["k"]);
    }

    [Fact]
    public void Empty_sequence_renders_empty()
    {
        Assert.Equal("", new LitSequence { Items = Sequence.Lines(Array.Empty<Lit>()) }.Render());
    }

    [Fact]
    public void Comma_list_joins_with_comma_space()
    {
        var list = Sequence.CommaList(new[] { new Lit { V = "a" }, new Lit { V = "b" } });
        Assert.Equal("a, b", new LitSequence { Items = list }.Render());
    }

    [Fact]
    public void Write_only_property_is_skipped_in_binding()
    {
        Assert.Equal("v", new WriteOnlyProp().Render());
    }

    [Fact]
    public void Embedded_resource_loads_by_full_name()
    {
        var set = new TemplateSet().AddEmbeddedResource(typeof(EngineCoverageTests).Assembly,
                                                        "Templar.Tests.Fixtures.Sample.tpl");
        var t = set.Get("Sample");
        t["who"] = "world";
        Assert.Equal("hello world", t.Render());
    }

    [Fact]
    public void Embedded_templates_load_by_prefix()
    {
        var set = new TemplateSet().AddEmbeddedTemplates(typeof(EngineCoverageTests).Assembly,
                                                         "Templar.Tests.Fixtures.");
        Assert.True(set.Contains("Sample"));
    }

    [Fact]
    public void Embedded_resource_with_single_dot_name()
    {
        var set = new TemplateSet().AddEmbeddedResource(typeof(EngineCoverageTests).Assembly, "Solo.tpl");
        var t = set.Get("Solo");
        t["x"] = "k";
        Assert.Equal("solo k", t.Render());
    }

    [Fact]
    public void Literal_carriage_return_in_source_is_dropped()
    {
        Assert.Equal("a\nb", Template.Parse("a\r\nb").Render());
    }

    [Fact]
    public void Blank_lines_sequence_joins_with_blank_line()
    {
        var list = Sequence.BlankLines(new[] { new Lit { V = "a" }, new Lit { V = "b" } });
        Assert.Equal("a\n\nb", new LitSequence { Items = list }.Render());
    }
}
