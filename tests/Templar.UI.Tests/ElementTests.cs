using Templar.Rendering;
using Templar.UI;
using Xunit;

namespace Templar.UI.Tests;

public class ElementTests
{
    [Fact]
    public void Inline_element_renders_on_one_line()
    {
        Assert.Equal("<span>hi</span>", H.Span("hi").Render());
    }

    [Fact]
    public void Void_element_self_closes()
    {
        Assert.Equal("<br />", H.Br().Render());
    }

    [Fact]
    public void Block_element_indents_children()
    {
        Assert.Equal("<div>\n    <span>hi</span>\n</div>", H.Div(H.Span("hi")).Render());
    }

    [Fact]
    public void Text_children_are_escaped()
    {
        Assert.Equal("<div>\n    &lt;script&gt;\n</div>", H.Div("<script>").Render());
    }

    [Fact]
    public void Nested_blocks_preserve_indentation_at_depth()
    {
        var html = H.Ul(new[] { H.Li("a"), H.Li("b") }).Render();
        Assert.Equal(
            "<ul>\n    <li>\n        a\n    </li>\n    <li>\n        b\n    </li>\n</ul>",
            html);
    }

    [Fact]
    public void No_class_attribute_when_empty()
    {
        Assert.Equal("<span>x</span>", H.Span("x").Render());
    }

    [Fact]
    public void Classes_argument_sets_class()
    {
        Assert.Equal("<div class=\"card\">\n    x\n</div>", H.Div("x", "card").Render());
    }

    [Fact]
    public void Default_class_composes_with_caller_extras()
    {
        var button = new Element
        {
            Tag = "button",
            Layout = ElementLayout.Inline,
            DefaultClass = "btn",
            Class = "btn--primary",
            Children = "Go",
        };
        Assert.Equal("<button class=\"btn btn--primary\">Go</button>", button.Render());
    }

    [Fact]
    public void Multiple_extra_tokens_merge_after_default()
    {
        var div = new Element
        {
            Tag = "div",
            Layout = ElementLayout.Inline,
            DefaultClass = "btn",
            Class = "a b",
            Children = "",
        };
        Assert.Equal("<div class=\"btn a b\"></div>", div.Render());
    }

    [Fact]
    public void Class_tokens_are_escaped()
    {
        var div = new Element
        {
            Tag = "div",
            Layout = ElementLayout.Inline,
            Class = "a<b",
            Children = "",
        };
        Assert.Equal("<div class=\"a&lt;b\"></div>", div.Render());
    }

    [Fact]
    public void Style_fragment_composes_as_a_class()
    {
        var div = new Element
        {
            Tag = "div",
            Layout = ElementLayout.Inline,
            DefaultClass = "btn",
            Class = new Cls { Tokens = "x" },
            Children = "",
        };
        Assert.Equal("<div class=\"btn x\"></div>", div.Render());
    }

    [Fact]
    public void ClassList_space_joins_fragments()
    {
        var list = new ClassList
        {
            Items = new Compositor[] { new Cls { Tokens = "a" }, new Cls { Tokens = "b" } },
        };
        Assert.Equal("a b", list.Render());
    }

    [Fact]
    public void Attrs_slot_adds_raw_attributes()
    {
        var html = H.Div("x", attrs: Html.Raw("id=\"main\"")).Render();
        Assert.Equal("<div id=\"main\">\n    x\n</div>", html);
    }

    [Fact]
    public void Class_and_attrs_render_together()
    {
        var html = H.Div("x", "card", Html.Raw("id=\"main\"")).Render();
        Assert.Equal("<div class=\"card\" id=\"main\">\n    x\n</div>", html);
    }

    [Fact]
    public void Pre_preserves_whitespace_without_reindenting()
    {
        Assert.Equal("<pre>line1\nline2</pre>", H.Pre("line1\nline2").Render());
    }

    [Fact]
    public void Pre_nested_keeps_content_at_column_zero()
    {
        Assert.Equal("<div>\n    <pre>a\nb</pre>\n</div>", H.Div(H.Pre("a\nb")).Render());
    }

    [Fact]
    public void Pre_content_is_escaped()
    {
        Assert.Equal("<pre>a &lt; b</pre>", H.Pre("a < b").Render());
    }

    [Fact]
    public void Inline_fragment_concatenates_mixed_content()
    {
        var html = H.P(H.Inline("Hello ", H.Strong("world"), "!")).Render();
        Assert.Equal("<p>\n    Hello <strong>world</strong>!\n</p>", html);
    }

    [Fact]
    public void Inline_text_is_escaped()
    {
        Assert.Equal("&lt;x&gt;", H.Inline("<x>").Render());
    }

    [Fact]
    public void Inline_raw_part_passes_through()
    {
        Assert.Equal("<b>", H.Inline(Html.Raw("<b>")).Render());
    }
}
