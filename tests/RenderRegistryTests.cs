using Templar.Rendering;
using Templar.Tests.Models.Variants;
using Xunit;

namespace Templar.Tests;

public class RenderRegistryTests
{
    private sealed class Probe
    {
    }

    private static IEnumerator<object?> First(TemplarWriter w, Probe probe)
    {
        w.Literal("first");
        yield break;
    }

    private static IEnumerator<object?> Second(TemplarWriter w, Probe probe)
    {
        w.Literal("second");
        yield break;
    }

    [Fact]
    public void Interface_holes_render_each_value_by_its_runtime_type()
    {
        var drawing = new Drawing
        {
            Main = new Square { Side = 2 },
            Shapes = Sequence.Lines(new IShape[] { new Circle { Radius = 1 }, new Square { Side = 3 } }),
        };

        Assert.Equal("main square\n         side=2\nshapes:\n    circle r=1\n    square\n        side=3", drawing.Render());
    }

    [Fact]
    public void Unregistered_runtime_type_throws_naming_the_type()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => TemplarWriter.Render<object>(new Probe(), RenderRegistry.Steps, null));

        Assert.Contains(typeof(Probe).FullName!, ex.Message);
    }

    [Fact]
    public void Register_overwrites_an_earlier_registration()
    {
        RenderRegistry.Register<Probe>(First);
        RenderRegistry.Register<Probe>(Second);

        Assert.Equal("second", TemplarWriter.Render<object>(new Probe(), RenderRegistry.Steps, null));
    }
}
