using Templar.Rendering;
using Xunit;

namespace Templar.Tests;

/// <summary>
/// IndentWriter is internal; these tests drive it through the public Template API
/// to verify the indentation-aware substitution behavior at various depths.
/// </summary>
public class IndentBehaviorTests
{
    [Fact]
    public void NestedPlaceholder_MultiLine_AlignsToInnerColumn()
    {
        var t = Template.Parse(
            """
            class Outer
            {
                {{ body }}
            }
            """);
        t["body"] = "void M()\n{\n    return;\n}";

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
        Assert.Equal(expected, t.Render());
    }

    [Fact]
    public void TwoNestedPlaceholders_BothAlignIndependently()
    {
        var t = Template.Parse(
            """
            {{ outer }}
              {{ inner }}
            """);
        t["outer"] = "A\nB";
        t["inner"] = "X\nY";

        // outer at col 0, inner at col 2
        var expected = "A\nB\n  X\n  Y";
        Assert.Equal(expected, t.Render());
    }

    [Fact]
    public void IEnumerableStringJoinsAsMultiLine()
    {
        var t = Template.Parse("    {{ lines }}");
        t["lines"] = new[] { "a", "b", "c" };
        Assert.Equal("    a\n    b\n    c", t.Render());
    }

    [Fact]
    public void IEnumerableStringJoinUsesConfiguredNewline()
    {
        var t = Template.Parse("{{ lines }}")
            .WithOptions(new RenderOptions { Newline = "\r\n" });
        t["lines"] = new[] { "a", "b", "c" };
        Assert.Equal("a\r\nb\r\nc", t.Render());
    }

    [Fact]
    public void EmptyStringValue_LeavesPlaceholderEmpty_NoTrailingWhitespace()
    {
        var t = Template.Parse("[{{ x }}]");
        t["x"] = "";
        Assert.Equal("[]", t.Render());
    }

    [Fact]
    public void CrlfInValue_NormalizedToConfiguredNewline()
    {
        var t = Template.Parse("{{ x }}");
        t["x"] = "a\r\nb";
        // Default newline is \n; CRLF input gets normalized through to that.
        Assert.Equal("a\nb", t.Render());

        var crlf = Template.Parse("{{ x }}")
            .WithOptions(new RenderOptions { Newline = "\r\n" });
        crlf["x"] = "a\r\nb";
        Assert.Equal("a\r\nb", crlf.Render());

        var loneCr = Template.Parse("{{ x }}");
        loneCr["x"] = "a\rb";
        Assert.Equal("a\nb", loneCr.Render());
    }
}
