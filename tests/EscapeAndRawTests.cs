using Templar.Rendering;
using Templar.Tests.Models.Values;
using Xunit;

namespace Templar.Tests;

public class EscapeAndRawTests
{
    [Fact]
    public void No_escape_by_default_keeps_codegen_behavior()
    {
        Assert.Equal("<b>", Compositor.ResolveTemplate(new Text("<b>"), "Values/X"));
    }
}
