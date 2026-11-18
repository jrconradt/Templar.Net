using Templar.Rendering;
using Xunit;

namespace Templar.Tests;

public class EscapeAndRawTests
{
    [Fact]
    public void No_escape_by_default_keeps_codegen_behavior()
    {
        var t = Template.Parse("{{ x }}");
        t["x"] = "<b>";
        Assert.Equal("<b>", t.Render());
    }
}
