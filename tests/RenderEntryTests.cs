using Templar.Rendering;
using Templar.Tests.Models.Entry;
using Xunit;

namespace Templar.Tests;

public class RenderEntryTests
{
    [Fact]
    public void Spots_bind_properties_by_name()
    {
        var result = Compositor.ResolveTemplate(new Simple("Hello", "World"));
        Assert.Equal("Hello, World!", result);
    }

    [Fact]
    public void Indexer_beside_the_properties_is_not_a_spot()
    {
        var result = Compositor.ResolveTemplate(new Indexed());
        Assert.Equal("value", result);
    }

    [Fact]
    public void Render_options_apply_their_newline()
    {
        var result = Compositor.ResolveTemplate(new Crlf("line2"), new RenderOptions { Newline = "\r\n" });
        Assert.Equal("line1\r\nline2\r\nline3", result);
    }

    [Fact]
    public void Render_options_apply_their_newline_to_a_named_template()
    {
        var result = Compositor.ResolveTemplate(new Crlf("line2"), "Models/Entry/Crlf", new RenderOptions { Newline = "\r\n" });
        Assert.Equal("line1\r\nline2\r\nline3", result);
    }

    [Fact]
    public void Rendering_the_same_type_repeatedly_renders_each_value()
    {
        for (int i = 0; i < 100; i++)
        {
            var r = Compositor.ResolveTemplate(new Simple("v", i.ToString()));
            Assert.Equal($"v, {i}!", r);
        }
    }
}
