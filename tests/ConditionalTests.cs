using Templar.Rendering;
using Xunit;

namespace Templar.Tests;

public class ConditionalTests
{
    [Fact]
    public void TruthyString_RendersThenBranch()
    {
        var t = Template.Parse("{{?name}}Hi {{name}}{{?}}");
        t["name"] = "Jeff";
        Assert.Equal("Hi Jeff", t.Render());
    }

    [Fact]
    public void EmptyString_IsFalsy()
    {
        var t = Template.Parse("{{?name}}Hi {{name}}{{?}}");
        t["name"] = "";
        Assert.Equal("", t.Render());
    }

    [Fact]
    public void NullValue_IsFalsy()
    {
        var t = Template.Parse("{{?name}}YES{{?}}");
        Assert.Equal("", t.Render());
    }

    [Fact]
    public void BoolFalse_IsFalsy()
    {
        var t = Template.Parse("{{?flag}}YES{{?}}");
        t["flag"] = false;
        Assert.Equal("", t.Render());
    }

    [Fact]
    public void BoolTrue_IsTruthy()
    {
        var t = Template.Parse("{{?flag}}YES{{?}}");
        t["flag"] = true;
        Assert.Equal("YES", t.Render());
    }

    [Fact]
    public void EmptyEnumerable_IsFalsy()
    {
        var t = Template.Parse("{{?items}}HAS{{?}}");
        t["items"] = System.Array.Empty<string>();
        Assert.Equal("", t.Render());
    }

    [Fact]
    public void NonEmptyEnumerable_IsTruthy()
    {
        var t = Template.Parse("{{?items}}HAS{{?}}");
        t["items"] = new[] { "a" };
        Assert.Equal("HAS", t.Render());
    }

    [Fact]
    public void ElseBranch_RendersWhenFalsy()
    {
        var t = Template.Parse("{{?name}}got it{{?else}}missing{{?}}");
        Assert.Equal("missing", t.Render());
    }

    [Fact]
    public void ElseBranch_SkippedWhenTruthy()
    {
        var t = Template.Parse("{{?name}}got it{{?else}}missing{{?}}");
        t["name"] = "x";
        Assert.Equal("got it", t.Render());
    }

    [Fact]
    public void Negation_InvertsTruthiness()
    {
        var t = Template.Parse("{{?!name}}MISSING{{?}}");
        Assert.Equal("MISSING", t.Render());
        t["name"] = "x";
        Assert.Equal("", t.Render());
    }

    [Fact]
    public void Nested_Conditionals()
    {
        var t = Template.Parse("{{?a}}A{{?b}}B{{?}}C{{?}}");
        t["a"] = true;
        t["b"] = true;
        Assert.Equal("ABC", t.Render());

        t["b"] = false;
        Assert.Equal("AC", t.Render());

        t["a"] = false;
        Assert.Equal("", t.Render());
    }

    [Fact]
    public void Conditional_PreservesIndentation_OfContainedExpression()
    {
        var t = Template.Parse("    {{?body}}{{body}}{{?}}");
        t["body"] = "line1\nline2";
        Assert.Equal("    line1\n    line2", t.Render());
    }

    [Fact]
    public void Conditional_GenericTypeArgs_CodegenShape()
    {
        var t = Template.Parse("class Foo{{?args}}<{{args}}>{{?}} { }");
        Assert.Equal("class Foo { }", t.Render());
        t["args"] = "T, U";
        Assert.Equal("class Foo<T, U> { }", t.Render());
    }

    [Fact]
    public void Unterminated_Conditional_Throws()
    {
        var ex = Assert.Throws<TemplateParseException>(() => Template.Parse("{{?x}}body"));
        Assert.Contains("Unterminated", ex.Message);
    }

    [Fact]
    public void StrayEnd_Throws()
    {
        var ex = Assert.Throws<TemplateParseException>(() => Template.Parse("body{{?}}"));
        Assert.Contains("no matching conditional", ex.Message);
    }

    [Fact]
    public void StrayElse_Throws()
    {
        var ex = Assert.Throws<TemplateParseException>(() => Template.Parse("body{{?else}}other"));
        Assert.Contains("no matching conditional", ex.Message);
    }

    [Fact]
    public void NegationWithNoOperand_Throws()
    {
        var ex = Assert.Throws<TemplateParseException>(() => Template.Parse("{{?!}}body{{?}}"));
        Assert.Contains("Empty conditional", ex.Message);
    }
}
