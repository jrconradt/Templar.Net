using Templar.Rendering;
using Xunit;

namespace Templar.Tests;

public class TemplateTests
{
    [Fact]
    public void Literal_PassesThrough()
    {
        var t = Template.Parse("hello world");
        Assert.Equal("hello world", t.Render());
    }

    [Fact]
    public void Variable_Substituted()
    {
        var t = Template.Parse("Hello, {{ name }}!");
        t["name"] = "Jeff";
        Assert.Equal("Hello, Jeff!", t.Render());
    }

    [Fact]
    public void Variable_Unset_RendersEmpty()
    {
        var t = Template.Parse("Hello, {{ name }}!");
        Assert.Equal("Hello, !", t.Render());
    }

    [Fact]
    public void Comment_Stripped()
    {
        var t = Template.Parse("before{{# this is a comment }}after");
        Assert.Equal("beforeafter", t.Render());
    }

    [Fact]
    public void MultiLine_Value_AlignsToPlaceholderColumn()
    {
        var t = Template.Parse("    {{ body }}");
        t["body"] = "line1\nline2\nline3";
        Assert.Equal("    line1\n    line2\n    line3", t.Render());
    }

    [Fact]
    public void Set_IsChainable()
    {
        var t = Template.Parse("{{ a }}{{ b }}");
        t.Set("a", "x").Set("b", "y");
        Assert.Equal("xy", t.Render());
    }

    [Fact]
    public void UnclosedTag_ThrowsParseException()
    {
        var ex = Assert.Throws<TemplateParseException>(() => Template.Parse("{{ unclosed"));
        Assert.Equal(1, ex.Line);
    }

    [Fact]
    public void VariableNameIsCaseInsensitive()
    {
        var t = Template.Parse("{{ NAME }}");
        t["name"] = "jeff";
        Assert.Equal("jeff", t.Render());
    }

    [Fact]
    public void MultipleVariables_EachSubstituted()
    {
        var t = Template.Parse("{{ a }} + {{ b }} = {{ c }}");
        t["a"] = "1";
        t["b"] = "2";
        t["c"] = "3";
        Assert.Equal("1 + 2 = 3", t.Render());
    }
}
