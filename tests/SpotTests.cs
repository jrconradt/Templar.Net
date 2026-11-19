using Templar.Rendering;
using Templar.Tests.Models.Values;
using Xunit;

namespace Templar.Tests;

public class SpotTests
{
    [Fact]
    public void Literal_passes_through()
    {
        Assert.Equal("hello world", Compositor.ResolveTemplate(new Nothing(), "Spot/Literal"));
    }

    [Fact]
    public void Null_value_renders_empty()
    {
        Assert.Equal("[]", Compositor.ResolveTemplate(new Text(null), "Values/Bracketed"));
    }

    [Fact]
    public void Comment_is_stripped()
    {
        Assert.Equal("beforeafter", Compositor.ResolveTemplate(new Nothing(), "Spot/Comment"));
    }

    [Fact]
    public void Comment_containing_marker_characters_is_still_a_comment()
    {
        Assert.Equal("ab", Compositor.ResolveTemplate(new Nothing(), "Spot/CommentWithMarkers"));
    }

    [Fact]
    public void Spot_names_match_properties_case_insensitively()
    {
        Assert.Equal("1 + 2", Compositor.ResolveTemplate(new Two("1", "2"), "Spot/Sum"));
    }
}
