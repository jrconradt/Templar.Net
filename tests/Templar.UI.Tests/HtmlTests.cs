using Templar.UI;
using Xunit;

namespace Templar.UI.Tests;

public class HtmlTests
{
    [Fact]
    public void Escape_encodes_all_markup_metacharacters()
    {
        Assert.Equal("a&lt;b&gt;&amp;&quot;&#39;", Markup.Escape("a<b>&\"'"));
    }

    [Fact]
    public void Escape_returns_input_unchanged_when_safe()
    {
        Assert.Equal("plain text", Markup.Escape("plain text"));
    }

    [Fact]
    public void Escape_handles_empty()
    {
        Assert.Equal("", Markup.Escape(""));
    }

    [Fact]
    public void Raw_carries_value_verbatim()
    {
        Assert.Equal("<b>hi</b>", Markup.Raw("<b>hi</b>").Value);
    }

    [Fact]
    public void Default_RawHtml_has_empty_value()
    {
        Assert.Equal("", default(RawHtml).Value);
    }
}
