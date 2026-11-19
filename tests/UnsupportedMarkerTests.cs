using Templar.Rendering;
using Xunit;

namespace Templar.Tests;

public class UnsupportedMarkerTests
{
    [Fact]
    public void Conditional_marker_throws_with_position()
    {
        var ex = Assert.Throws<TemplateParseException>(() => Template.Parse("ab\n  {{? x }}y{{?}}"));
        Assert.Contains("'?'", ex.Message);
        Assert.Equal(2, ex.Line);
        Assert.Equal(3, ex.Column);
    }

    [Fact]
    public void Raw_marker_throws()
    {
        var ex = Assert.Throws<TemplateParseException>(() => Template.Parse("{{& x }}"));
        Assert.Contains("'&'", ex.Message);
    }

    [Fact]
    public void Slot_marker_throws()
    {
        var ex = Assert.Throws<TemplateParseException>(() => Template.Parse("{{> x }}"));
        Assert.Contains("'>'", ex.Message);
    }

    [Fact]
    public void Filter_pipe_throws_with_position()
    {
        var ex = Assert.Throws<TemplateParseException>(() => Template.Parse("a {{ x | upper }}"));
        Assert.Contains("'|'", ex.Message);
        Assert.Equal(1, ex.Line);
        Assert.Equal(3, ex.Column);
    }

    [Fact]
    public void Comment_containing_marker_characters_is_still_a_comment()
    {
        var t = Template.Parse("a{{# ? & > | }}b");
        Assert.Equal("ab", t.Render());
    }
}
