using Templar.Rendering;
using Xunit;

namespace Templar.Tests;

public class EscapeTests
{
    [Fact]
    public void EscapedOpenBraces_EmitLiteral()
    {
        var t = Template.Parse(@"\{{x}}");
        Assert.Equal("{{x}}", t.Render());
    }

    [Fact]
    public void EscapedOpen_DoesNotSubstitute()
    {
        var t = Template.Parse(@"\{{ name }}");
        t["name"] = "Jeff";
        Assert.Equal("{{ name }}", t.Render());
    }

    [Fact]
    public void EscapedClose_EmitsLiteral()
    {
        var t = Template.Parse(@"a\}}b");
        Assert.Equal("a}}b", t.Render());
    }

    [Fact]
    public void EscapedBackslash_EmitsSingleBackslash()
    {
        var t = Template.Parse(@"a\\b");
        Assert.Equal(@"a\b", t.Render());
    }

    [Fact]
    public void BackslashBeforeNormalChar_PreservedLiteral()
    {
        var t = Template.Parse(@"a\nb");
        Assert.Equal(@"a\nb", t.Render());
    }

    [Fact]
    public void EscapeAndSubstitution_Mix()
    {
        var t = Template.Parse(@"\{{ {{ var }} \}}");
        t["var"] = "X";
        Assert.Equal("{{ X }}", t.Render());
    }

    [Fact]
    public void DoubleBackslashBeforeBraces_EmitsBackslashThenTag()
    {
        var t = Template.Parse(@"\\{{ var }}");
        t["var"] = "X";
        Assert.Equal(@"\X", t.Render());
    }

    [Fact]
    public void EscapeInsideConditional()
    {
        var t = Template.Parse(@"{{?show}}\{{ x \}}{{?}}");
        t["show"] = true;
        Assert.Equal("{{ x }}", t.Render());
    }
}
