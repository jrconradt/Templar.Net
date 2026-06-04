using Templar.Rendering;
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
    public void NestedCompositor_RendersEmpty_SwallowsHostLine()
    {
        var t = Template.Parse(
            """
            before
                {{ inner }}
            after
            """);
        t["inner"] = Sequence.Lines([]);
        Assert.Equal("before\nafter", t.Render());
    }

    private sealed class EmptyComposite : Compositor
    {
        protected override string Structure => "";
    }

    [Fact]
    public void NestedCompositor_WithEmptyStructure_SwallowsHostLine()
    {
        var t = Template.Parse(
            """
            before
                {{ inner }}
            after
            """);
        t["inner"] = new EmptyComposite();
        Assert.Equal("before\nafter", t.Render());
    }
}

public class StrictUndefinedTests
{
    [Fact]
    public void StrictUndefined_Off_ByDefault()
    {
        var t = Template.Parse("[{{ x }}]");
        Assert.Equal("[]", t.Render());
    }

    [Fact]
    public void StrictUndefined_On_ThrowsOnMissingVariable()
    {
        var t = Template.Parse("[{{ x }}]")
            .WithOptions(new RenderOptions { StrictUndefined = true });
        var ex = Assert.Throws<TemplateRenderException>(() => t.Render());
        Assert.Contains("Undefined variable 'x'", ex.Message);
        Assert.Equal("x", ex.VariableName);
    }

    [Fact]
    public void StrictUndefined_On_VariableSetToEmptyDoesNotThrow()
    {
        var t = Template.Parse("[{{ x }}]")
            .WithOptions(new RenderOptions { StrictUndefined = true });
        t["x"] = "";
        Assert.Equal("[]", t.Render());
    }

    [Fact]
    public void StrictUndefined_On_VariableSetToNullDoesNotThrow()
    {
        var t = Template.Parse("[{{ x }}]")
            .WithOptions(new RenderOptions { StrictUndefined = true });
        t["x"] = null;
        Assert.Equal("[]", t.Render());
    }

    private sealed class StrictComposite : Compositor
    {
        public string Defined { get; init; } = "";
        protected override string Structure => "{{ defined }}-{{ missing }}";
    }

    [Fact]
    public void StrictUndefined_On_Compositor_ThrowsOnUnboundPlaceholder()
    {
        var c = new StrictComposite { Defined = "ok" }
            .WithOptions(new RenderOptions { StrictUndefined = true });
        var ex = Assert.Throws<TemplateRenderException>(() => c.Render());
        Assert.Contains("missing", ex.Message);
    }
}

public class PlaceholderScannerHonorsNewSyntaxSmokeTest
{
    [Fact]
    public void Conditional_Around_EscapedBraces_AroundPlaceholder()
    {
        var t = Template.Parse(@"{{?show}}\{{ {{ name }} \}}{{?}}");
        t["show"] = true;
        t["name"] = "X";
        Assert.Equal("{{ X }}", t.Render());
    }
}
