using Templar.UI;
using Xunit;

namespace Templar.UI.Tests;

public class SecurityTests
{
    [Fact]
    public void Tag_name_injection_is_rejected()
    {
        var element = new Element { Tag = "div onload=alert(1)" };
        Assert.Throws<MarkupSecurityException>(() => element.Render());
    }

    [Fact]
    public void Attribute_name_injection_is_rejected()
    {
        var attr = new Attr { Name = "x onclick=alert(1)", Value = "y" };
        Assert.Throws<MarkupSecurityException>(() => attr.Render());
    }

    [Fact]
    public void Event_handler_attribute_is_blocked()
    {
        var attr = new Attr { Name = "onclick", Value = "doThing()" };
        Assert.Throws<MarkupSecurityException>(() => attr.Render());
    }

    [Fact]
    public void Javascript_url_is_neutralized()
    {
        var attr = new Attr { Name = "href", Value = "javascript:alert(1)" };
        Assert.Equal(" href=\"about:invalid#blocked\"", attr.Render());
    }

    [Fact]
    public void Control_char_obfuscated_javascript_url_is_neutralized()
    {
        var attr = new Attr { Name = "href", Value = "java\tscript:alert(1)" };
        Assert.Equal(" href=\"about:invalid#blocked\"", attr.Render());
    }

    [Fact]
    public void Data_url_is_neutralized()
    {
        var attr = new Attr { Name = "src", Value = "data:text/html,<script>alert(1)</script>" };
        Assert.Equal(" src=\"about:invalid#blocked\"", attr.Render());
    }

    [Fact]
    public void Relative_url_is_preserved()
    {
        var attr = new Attr { Name = "href", Value = "/path?q=1" };
        Assert.Equal(" href=\"/path?q=1\"", attr.Render());
    }

    [Fact]
    public void Https_url_is_preserved_and_escaped()
    {
        var attr = new Attr { Name = "href", Value = "https://example.com/a?b=1&c=2" };
        Assert.Equal(" href=\"https://example.com/a?b=1&amp;c=2\"", attr.Render());
    }

    [Fact]
    public void Attribute_value_breakout_is_escaped()
    {
        var attr = new Attr { Name = "title", Value = "\"><script>" };
        Assert.Equal(" title=\"&quot;&gt;&lt;script&gt;\"", attr.Render());
    }

    [Fact]
    public void Boolean_attribute_renders_bare()
    {
        Assert.Equal(" disabled", new Attr { Name = "disabled", Boolean = true }.Render());
    }

    [Fact]
    public void Attrs_slot_rejects_plain_string()
    {
        Assert.Throws<MarkupSecurityException>(() => H.Div("x", attrs: "id=\"x\"").Render());
    }

    [Fact]
    public void Attrs_slot_accepts_escaped_attr()
    {
        var html = H.Div("x", attrs: new Attr { Name = "id", Value = "main" }).Render();
        Assert.Equal("<div id=\"main\">\n    x\n</div>", html);
    }

    [Fact]
    public void Attrs_slot_accepts_multiple_attrs()
    {
        var html = H.Div("x", attrs: new[]
        {
            new Attr { Name = "id", Value = "a" },
            new Attr { Name = "data-n", Value = "1" },
        }).Render();
        Assert.Equal("<div id=\"a\" data-n=\"1\">\n    x\n</div>", html);
    }

    [Fact]
    public void Url_attribute_through_element_is_sanitized()
    {
        var html = H.A("link", attrs: new Attr { Name = "href", Value = "javascript:alert(1)" }).Render();
        Assert.Equal("<a href=\"about:invalid#blocked\">link</a>", html);
    }

    [Fact]
    public void Raw_html_child_is_a_trusted_passthrough()
    {
        Assert.Equal("<div>\n    <b>ok</b>\n</div>", H.Div(Html.Raw("<b>ok</b>")).Render());
    }
}
