using Templar.Rendering;
using Templar.Tests.Models.Swallow;
using Xunit;

namespace Templar.Tests;

public class SwallowTests
{
    [Fact]
    public void EmptyExpression_OnOwnLine_CollapsesLine()
    {
        var t = Template.Parse(
            """
            class Outer
            {
                {{ body }}
            }
            """);
        var expected =
            """
            class Outer
            {
            }
            """;
        Assert.Equal(expected, t.Render());
    }

    [Fact]
    public void EmptyExpression_OnOwnLine_StringSetToEmpty_AlsoCollapses()
    {
        var t = Template.Parse(
            """
            class Outer
            {
                {{ body }}
            }
            """);
        t["body"] = "";
        var expected =
            """
            class Outer
            {
            }
            """;
        Assert.Equal(expected, t.Render());
    }

    [Fact]
    public void NonEmptyExpression_KeepsLine()
    {
        var t = Template.Parse(
            """
            class Outer
            {
                {{ body }}
            }
            """);
        t["body"] = "X";
        var expected =
            """
            class Outer
            {
                X
            }
            """;
        Assert.Equal(expected, t.Render());
    }

    [Fact]
    public void DeliberateBlankLine_Preserved()
    {
        var t = Template.Parse("A\n\nB");
        Assert.Equal("A\n\nB", t.Render());
    }

    [Fact]
    public void MixedLine_ExpressionEmpty_LiteralPresent_KeepsLine()
    {
        var t = Template.Parse("    foo {{ x }}\nafter");
        t["x"] = "";
        Assert.Equal("    foo \nafter", t.Render());
    }

    [Fact]
    public void MultipleEmptyExpressions_OnSameLine_StillSwallows()
    {
        var t = Template.Parse("before\n  {{ a }}  {{ b }}  \nafter");
        Assert.Equal("before\nafter", t.Render());
    }

    [Fact]
    public void NestedList_RendersEmpty_SwallowsHostLine()
    {
        var host = new ListHost { Inner = Sequence.Lines(Array.Empty<Empty>()) };
        Assert.Equal("before\nafter", host.Render());
    }

    [Fact]
    public void NestedType_WithEmptyTemplate_SwallowsHostLine()
    {
        var host = new TypeHost { Inner = new Empty() };
        Assert.Equal("before\nafter", host.Render());
    }
}

public class PlaceholderScannerHonorsNewSyntaxSmokeTest
{
    [Fact]
    public void EscapedBraces_AroundPlaceholder()
    {
        var t = Template.Parse(@"\{{ {{ name }} \}}");
        t["name"] = "X";
        Assert.Equal("{{ X }}", t.Render());
    }
}
