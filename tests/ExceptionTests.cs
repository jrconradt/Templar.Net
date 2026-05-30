using Templar.Rendering;
using Xunit;

namespace Templar.Tests;

public class ExceptionTests
{
    [Fact]
    public void Parse_UnclosedTag_CarriesLineAndColumn()
    {
        var ex = Assert.Throws<TemplateParseException>(() =>
            Template.Parse("line1\nline2 {{ unclosed"));
        Assert.Equal(2, ex.Line);
        Assert.Equal(7, ex.Column);
    }

    [Fact]
    public void Render_UnknownFilter_CarriesFilterAndVariableNames()
    {
        var t = Template.Parse("{{ x | bogus }}");
        t["x"] = "v";

        var ex = Assert.Throws<TemplateRenderException>(() => t.Render());
        Assert.Equal("bogus", ex.FilterName);
        Assert.Equal("x", ex.VariableName);
    }

    [Fact]
    public void Render_UnknownFilter_MessageMentionsBoth()
    {
        var t = Template.Parse("{{ foo | nope }}");
        var ex = Assert.Throws<TemplateRenderException>(() => t.Render());
        Assert.Contains("nope", ex.Message);
        Assert.Contains("foo", ex.Message);
    }
}
