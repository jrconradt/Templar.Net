using Templar.Rendering;
using Templar.Tests.Models.Swallow;
using Templar.Tests.Models.Values;
using Xunit;

namespace Templar.Tests;

public class SwallowTests
{
    private const string Collapsed =
        """
        class Outer
        {
        }
        """;

    [Fact]
    public void NullSpot_OnOwnLine_CollapsesLine()
    {
        Assert.Equal(Collapsed, Compositor.ResolveTemplate(new Text(null), "Indent/ClassBody"));
    }

    [Fact]
    public void EmptyStringSpot_OnOwnLine_AlsoCollapses()
    {
        Assert.Equal(Collapsed, Compositor.ResolveTemplate(new Text(""), "Indent/ClassBody"));
    }

    [Fact]
    public void NonEmptySpot_KeepsLine()
    {
        var expected =
            """
            class Outer
            {
                X
            }
            """;
        Assert.Equal(expected, Compositor.ResolveTemplate(new Text("X"), "Indent/ClassBody"));
    }

    [Fact]
    public void DeliberateBlankLine_Preserved()
    {
        Assert.Equal("A\n\nB", Compositor.ResolveTemplate(new Nothing(), "Swallow/BlankLine"));
    }

    [Fact]
    public void MixedLine_SpotEmpty_LiteralPresent_KeepsLine()
    {
        Assert.Equal("    foo \nafter", Compositor.ResolveTemplate(new Text(""), "Swallow/MixedLine"));
    }

    [Fact]
    public void MultipleEmptySpots_OnSameLine_StillSwallows()
    {
        Assert.Equal("before\nafter", Compositor.ResolveTemplate(new Two(null, null), "Swallow/TwoEmpty"));
    }

    [Fact]
    public void NestedList_RendersEmpty_SwallowsHostLine()
    {
        var host = new ListHost(Sequence.Lines(Array.Empty<Empty>()));
        Assert.Equal("before\nafter", Compositor.ResolveTemplate(host));
    }

    [Fact]
    public void NestedType_WithEmptyTemplate_SwallowsHostLine()
    {
        var host = new TypeHost(new Empty());
        Assert.Equal("before\nafter", Compositor.ResolveTemplate(host));
    }
}

public class SpotScannerHonorsEscapesSmokeTest
{
    [Fact]
    public void EscapedBraces_AroundSpot()
    {
        Assert.Equal("{{ X }}", Compositor.ResolveTemplate(new Text("X"), "Escape/Mix"));
    }
}
