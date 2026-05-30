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
    public void Filter_Upper()
    {
        var t = Template.Parse("{{ name | upper }}");
        t["name"] = "hello";
        Assert.Equal("HELLO", t.Render());
    }

    [Fact]
    public void Filter_Lower()
    {
        var t = Template.Parse("{{ name | lower }}");
        t["name"] = "HELLO";
        Assert.Equal("hello", t.Render());
    }

    [Fact]
    public void Filter_Pascal()
    {
        var t = Template.Parse("{{ name | pascal }}");
        t["name"] = "foo_bar_baz";
        Assert.Equal("FooBarBaz", t.Render());
    }

    [Fact]
    public void Filter_Camel()
    {
        var t = Template.Parse("{{ name | camel }}");
        t["name"] = "foo_bar_baz";
        Assert.Equal("fooBarBaz", t.Render());
    }

    [Fact]
    public void Filter_Pascal_DashDelimiter()
    {
        var t = Template.Parse("{{ name | pascal }}");
        t["name"] = "foo-bar-baz";
        Assert.Equal("FooBarBaz", t.Render());
    }

    [Fact]
    public void Filter_Pascal_SpaceDelimiter()
    {
        var t = Template.Parse("{{ name | pascal }}");
        t["name"] = "foo bar baz";
        Assert.Equal("FooBarBaz", t.Render());
    }

    [Fact]
    public void Filter_Pascal_MixedDelimiters()
    {
        var t = Template.Parse("{{ name | pascal }}");
        t["name"] = "foo-bar baz_qux";
        Assert.Equal("FooBarBazQux", t.Render());
    }

    [Fact]
    public void Filter_Pascal_ConsecutiveDelimiters()
    {
        var t = Template.Parse("{{ name | pascal }}");
        t["name"] = "foo -_ bar";
        Assert.Equal("FooBar", t.Render());
    }

    [Fact]
    public void Filter_Camel_DashDelimiter()
    {
        var t = Template.Parse("{{ name | camel }}");
        t["name"] = "foo-bar-baz";
        Assert.Equal("fooBarBaz", t.Render());
    }

    [Fact]
    public void Filter_Camel_SpaceDelimiter()
    {
        var t = Template.Parse("{{ name | camel }}");
        t["name"] = "foo bar baz";
        Assert.Equal("fooBarBaz", t.Render());
    }

    [Fact]
    public void Filter_Camel_MixedDelimiters()
    {
        var t = Template.Parse("{{ name | camel }}");
        t["name"] = "foo-bar baz_qux";
        Assert.Equal("fooBarBazQux", t.Render());
    }

    [Fact]
    public void Filter_Custom()
    {
        var t = Template.Parse("{{ val | shout }}");
        t.AddFilter("shout", v => (v?.ToString() ?? "") + "!!!");
        t["val"] = "hey";
        Assert.Equal("hey!!!", t.Render());
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
    public void UnknownFilter_ThrowsRenderException()
    {
        var t = Template.Parse("{{ x | bogus }}");
        t["x"] = "v";
        Assert.Throws<TemplateRenderException>(() => t.Render());
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
