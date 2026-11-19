using Templar.Rendering;
using Templar.Tests.Models.Compiled;
using Xunit;

namespace Templar.Tests;

public class CompiledEmitTests
{
    [Fact]
    public void Compiled_scalar_matches_interpreted()
    {
        string interpreted = Template.Parse("Hello {{ who }} and {{ place }}!")
            .Set("who", "World")
            .Set("place", "Templar")
            .Render();
        string compiled = new Greeting { Who = "World", Place = "Templar" }.Render();
        Assert.Equal(interpreted, compiled);
    }

    [Fact]
    public void Compiled_nested_type_matches_interpreted_multiline_value()
    {
        string interpreted = Template.Parse("before\n    {{ inside }}\nafter")
            .Set("inside", "line-a\nline-b")
            .Render();
        string compiled = new Outer { Inside = new TwoLines() }.Render();
        Assert.Equal(interpreted, compiled);
    }

    [Fact]
    public void Compiled_deep_nesting_does_not_overflow()
    {
        var acc = new Wrap { Leaf = "X" };
        for (int i = 0; i < 5000; i++)
        {
            acc = new Wrap { Inside = acc };
        }
        Assert.Equal("X", acc.Render());
    }
}
