using Templar.Rendering;
using Templar.Tests.Models.Variants;
using Xunit;

namespace Templar.Tests;

public class RenderRegistryTests
{
    private sealed class Probe
    {
    }

    private sealed class Stranger
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
    public void Interface_spots_render_each_value_by_its_runtime_type()
    {
        var drawing = new Drawing(new Square(2), Sequence.Lines(new IShape[] { new Circle(1), new Square(3) }));

        Assert.Equal("main square\n         side=2\nshapes:\n    circle r=1\n    square\n        side=3", Compositor.ResolveTemplate(drawing));
    }

    [Fact]
    public void Interface_typed_root_renders_by_its_runtime_type()
    {
        IShape shape = new Circle(1);

        Assert.Equal("circle r=1", Compositor.ResolveTemplate(shape));
    }

    [Fact]
    public void Unregistered_runtime_type_throws_naming_the_type()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => TemplarWriter.Render<object>(new Stranger(), RenderRegistry.Steps, null));

        Assert.Contains(typeof(Stranger).FullName!, ex.Message);
    }

    [Fact]
    public void Unregistered_pair_throws_naming_the_type_and_the_template()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => RenderRegistry.Find<Stranger>("Nowhere/Stranger"));

        Assert.Contains(typeof(Stranger).FullName!, ex.Message);
        Assert.Contains("'Nowhere/Stranger'", ex.Message);
    }

    [Fact]
    public void Register_overwrites_an_earlier_registration()
    {
        RenderRegistry.Register<Probe>("Probe", First);
        RenderRegistry.Register<Probe>("Probe", Second);
        RenderRegistry.RegisterDefault<Probe>("Probe");

        Assert.Equal("second", TemplarWriter.Render(new Probe(), RenderRegistry.Find<Probe>("Probe"), null));
        Assert.Equal("second", TemplarWriter.Render<object>(new Probe(), RenderRegistry.Steps, null));
    }
}
