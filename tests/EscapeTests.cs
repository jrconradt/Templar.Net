using Templar.Rendering;
using Templar.Tests.Models.Values;
using Xunit;

namespace Templar.Tests;

public class EscapeTests
{
    [Fact]
    public void EscapedOpenBraces_EmitLiteral()
    {
        Assert.Equal("{{x}}", Compositor.ResolveTemplate(new Nothing(), "Escape/OpenBraces"));
    }

    [Fact]
    public void EscapedOpen_DoesNotSubstitute()
    {
        Assert.Equal("{{ x }}", Compositor.ResolveTemplate(new Text("Jeff"), "Escape/OpenBeforeSpot"));
    }

    [Fact]
    public void EscapedClose_EmitsLiteral()
    {
        Assert.Equal("a}}b", Compositor.ResolveTemplate(new Nothing(), "Escape/Close"));
    }

    [Fact]
    public void EscapedBackslash_EmitsSingleBackslash()
    {
        Assert.Equal(@"a\b", Compositor.ResolveTemplate(new Nothing(), "Escape/Backslash"));
    }

    [Fact]
    public void BackslashBeforeNormalChar_PreservedLiteral()
    {
        Assert.Equal(@"a\nb", Compositor.ResolveTemplate(new Nothing(), "Escape/BackslashBeforeLetter"));
    }

    [Fact]
    public void EscapeAndSubstitution_Mix()
    {
        Assert.Equal("{{ X }}", Compositor.ResolveTemplate(new Text("X"), "Escape/Mix"));
    }

    [Fact]
    public void DoubleBackslashBeforeBraces_EmitsBackslashThenSpot()
    {
        Assert.Equal(@"\X", Compositor.ResolveTemplate(new Text("X"), "Escape/DoubleBackslash"));
    }
}
