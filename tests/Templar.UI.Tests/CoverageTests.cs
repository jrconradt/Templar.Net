using Templar.Rendering;
using Templar.UI;
using Xunit;

namespace Templar.UI.Tests;

public class CoverageTests
{
    [Fact]
    public void Attr_with_no_value_and_not_boolean_renders_empty()
    {
        Assert.Equal("", new Attr { Name = "data-x" }.Render());
    }

    [Fact]
    public void Html_options_sets_newline_and_escape()
    {
        var options = Html.Options("\r\n", "  ");
        Assert.Equal("\r\n", options.Newline);
        Assert.Equal("  ", options.IndentString);
        Assert.NotNull(options.Escape);
        Assert.Equal("&lt;", options.Escape!("<"));
    }

    [Fact]
    public void RawHtml_to_string_and_of()
    {
        Assert.Equal("<x>", Html.Raw("<x>").ToString());
        Assert.Equal("<y>", RawHtml.Of("<y>").Value);
    }

    [Fact]
    public void Verbatim_to_string()
    {
        Assert.Equal("z\nz", new Verbatim("z\nz").ToString());
    }

    [Fact]
    public void Verbatim_raw_content_is_not_escaped()
    {
        var element = new Element
        {
            Tag = "pre",
            Layout = ElementLayout.Verbatim,
            RawContent = true,
            Children = Html.Raw("<x>"),
        };
        Assert.Equal("<pre><x></pre>", element.Render());
    }

    [Fact]
    public void Verbatim_compositor_content_is_rendered_then_escaped()
    {
        var element = new Element
        {
            Tag = "pre",
            Layout = ElementLayout.Verbatim,
            Children = H.Span("hi"),
        };
        Assert.Equal("<pre>&lt;span&gt;hi&lt;/span&gt;</pre>", element.Render());
    }

    [Fact]
    public void Verbatim_with_no_content_renders_empty_element()
    {
        Assert.Equal("<pre></pre>", new Element { Tag = "pre", Layout = ElementLayout.Verbatim }.Render());
    }

    [Fact]
    public void Verbatim_non_string_content_uses_to_string()
    {
        var element = new Element
        {
            Tag = "pre",
            Layout = ElementLayout.Verbatim,
            Children = 42,
        };
        Assert.Equal("<pre>42</pre>", element.Render());
    }

    [Fact]
    public void Class_as_compositor_sequence_merges()
    {
        var element = new Element
        {
            Tag = "div",
            Layout = ElementLayout.Inline,
            Class = new Compositor[] { new Cls { Tokens = "a" }, new Cls { Tokens = "b" } },
            Children = "",
        };
        Assert.Equal("<div class=\"a b\"></div>", element.Render());
    }

    [Fact]
    public void Class_as_string_sequence_skips_empty_tokens()
    {
        var element = new Element
        {
            Tag = "div",
            Layout = ElementLayout.Inline,
            DefaultClass = "btn",
            Class = new[] { "a", "", "b" },
            Children = "",
        };
        Assert.Equal("<div class=\"btn a b\"></div>", element.Render());
    }

    [Fact]
    public void Attrs_raw_with_leading_space_is_not_doubled()
    {
        Assert.Equal("<div id=\"x\"></div>",
            new Element { Tag = "div", Layout = ElementLayout.Inline, Attrs = Html.Raw(" id=\"x\""), Children = "" }.Render());
    }

    [Fact]
    public void Attrs_empty_raw_renders_nothing()
    {
        Assert.Equal("<div></div>",
            new Element { Tag = "div", Layout = ElementLayout.Inline, Attrs = Html.Raw(""), Children = "" }.Render());
    }

    [Fact]
    public void Empty_tag_name_is_rejected()
    {
        Assert.Throws<MarkupSecurityException>(() => new Element { Tag = "" }.Render());
    }

    [Fact]
    public void Tag_name_with_invalid_first_char_is_rejected()
    {
        Assert.Throws<MarkupSecurityException>(() => new Element { Tag = "1bad" }.Render());
    }

    [Fact]
    public void Empty_attribute_name_is_rejected()
    {
        Assert.Throws<MarkupSecurityException>(() => new Attr { Name = "", Value = "x" }.Render());
    }

    [Fact]
    public void Attribute_name_with_invalid_first_char_is_rejected()
    {
        Assert.Throws<MarkupSecurityException>(() => new Attr { Name = "1bad", Value = "x" }.Render());
    }

    [Fact]
    public void Schemeless_url_without_path_is_preserved()
    {
        Assert.Equal(" href=\"example.com\"", new Attr { Name = "href", Value = "example.com" }.Render());
    }

    [Fact]
    public void Attrs_slot_accepts_single_attr_compositor()
    {
        Assert.Equal("<div id=\"x\"></div>",
            new Element { Tag = "div", Layout = ElementLayout.Inline, Attrs = new Attr { Name = "id", Value = "x" }, Children = "" }.Render());
    }
}
