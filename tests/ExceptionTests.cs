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
}
