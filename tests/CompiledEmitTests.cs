using Templar.Rendering;
using Templar.Tests.Models.Compiled;
using Xunit;

namespace Templar.Tests;

public class CompiledEmitTests
{
    [Fact]
    public void Compiled_spots_render_their_values()
    {
        Assert.Equal("Hello World and Templar!", Compositor.ResolveTemplate(new Greeting("World", "Templar")));
    }

    [Fact]
    public void Compiled_nested_type_renders_its_lines_at_the_spot_column()
    {
        Assert.Equal("before\n    line-a\n    line-b\nafter", Compositor.ResolveTemplate(new Outer(new TwoLines())));
    }

    [Fact]
    public void Compiled_deep_nesting_does_not_overflow()
    {
        var acc = new Wrap(null, "X");
        for (int i = 0; i < 5000; i++)
        {
            acc = new Wrap(acc);
        }
        Assert.Equal("X", Compositor.ResolveTemplate(acc));
    }
}
