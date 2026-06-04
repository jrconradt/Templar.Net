using Templar.Rendering;
using Xunit;

namespace Templar.Tests;

public class CompiledEmitTests
{
    private sealed class Greeting : Compositor
    {
        public required object? Who { get; init; }
        public required object? Place { get; init; }

        public override void RenderInto(TemplarWriter w)
        {
            w.Compiled(Steps(w));
        }

        private IEnumerator<IComposable> Steps(TemplarWriter w)
        {
            w.Literal("Hello ");
            {
                var c = w.Value(Who);
                if (c is not null)
                {
                    yield return c;
                }
            }
            w.Literal(" and ");
            {
                var c = w.Value(Place);
                if (c is not null)
                {
                    yield return c;
                }
            }
            w.Literal("!");
        }
    }

    private sealed class TwoLines : Compositor
    {
        protected override string Structure => "line-a\nline-b";
    }

    private sealed class Outer : Compositor
    {
        public required object? Inside { get; init; }

        public override void RenderInto(TemplarWriter w)
        {
            w.Compiled(Steps(w));
        }

        private IEnumerator<IComposable> Steps(TemplarWriter w)
        {
            w.Literal("before\n    ");
            {
                var c = w.Value(Inside);
                if (c is not null)
                {
                    yield return c;
                }
            }
            w.Literal("\nafter");
        }
    }

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
    public void Compiled_nested_compositor_matches_interpreted()
    {
        string interpreted = Template.Parse("before\n    {{ inside }}\nafter")
            .Set("inside", new TwoLines())
            .Render();
        string compiled = new Outer { Inside = new TwoLines() }.Render();
        Assert.Equal(interpreted, compiled);
    }

    private sealed class Leaf : Compositor
    {
        protected override string Structure => "X";
    }

    private sealed class Wrap : Compositor
    {
        public required object? Inside { get; init; }

        public override void RenderInto(TemplarWriter w)
        {
            w.Compiled(Steps(w));
        }

        private IEnumerator<IComposable> Steps(TemplarWriter w)
        {
            {
                var c = w.Value(Inside);
                if (c is not null)
                {
                    yield return c;
                }
            }
        }
    }

    [Fact]
    public void Compiled_deep_nesting_does_not_overflow()
    {
        Compositor acc = new Leaf();
        for (int i = 0; i < 5000; i++)
        {
            acc = new Wrap { Inside = acc };
        }
        Assert.Equal("X", acc.Render());
    }
}
