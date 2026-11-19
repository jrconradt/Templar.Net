using Templar.Rendering;
using Templar.Tests.Models.Values;
using Xunit;

namespace Templar.Tests;

public class IndentBehaviorTests
{
    [Fact]
    public void NestedSpot_MultiLine_AlignsToInnerColumn()
    {
        var expected =
            """
            class Outer
            {
                void M()
                {
                    return;
                }
            }
            """;
        Assert.Equal(expected, Compositor.ResolveTemplate(new Text("void M()\n{\n    return;\n}"), "Indent/ClassBody"));
    }

    [Fact]
    public void TwoNestedSpots_BothAlignIndependently()
    {
        // a at col 0, b at col 2
        Assert.Equal("A\nB\n  X\n  Y", Compositor.ResolveTemplate(new Two("A\nB", "X\nY"), "Indent/TwoColumns"));
    }

    [Fact]
    public void IEnumerableString_RendersOneItemPerLineAtTheColumn()
    {
        Assert.Equal("    a\n    b\n    c", Compositor.ResolveTemplate(new Lines(["a", "b", "c"]), "Values/Indented4"));
    }

    [Fact]
    public void IEnumerableString_UsesConfiguredNewline()
    {
        var options = new RenderOptions { Newline = "\r\n" };
        Assert.Equal("a\r\nb\r\nc", Compositor.ResolveTemplate(new Lines(["a", "b", "c"]), "Values/X", options));
    }

    [Fact]
    public void EmptyStringValue_LeavesSpotEmpty_NoTrailingWhitespace()
    {
        Assert.Equal("[]", Compositor.ResolveTemplate(new Text(""), "Values/Bracketed"));
    }

    [Fact]
    public void CrlfInValue_NormalizedToConfiguredNewline()
    {
        Assert.Equal("a\nb", Compositor.ResolveTemplate(new Text("a\r\nb"), "Values/X"));
        Assert.Equal("a\r\nb", Compositor.ResolveTemplate(new Text("a\r\nb"), "Values/X", new RenderOptions { Newline = "\r\n" }));
        Assert.Equal("a\nb", Compositor.ResolveTemplate(new Text("a\rb"), "Values/X"));
    }
}
