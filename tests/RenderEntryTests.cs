using Templar.Rendering;
using Templar.Tests.Models.Entry;
using Xunit;

namespace Templar.Tests;

public class RenderEntryTests
{
    [Fact]
    public void Holes_bind_properties_by_name()
    {
        var result = new Simple { Name = "World", Verb = "Hello" }.Render();
        Assert.Equal("Hello, World!", result);
    }

    [Fact]
    public void Indexer_beside_the_properties_is_not_a_hole()
    {
        var result = new Indexed().Render();
        Assert.Equal("value", result);
    }

    [Fact]
    public void Render_options_apply_their_newline()
    {
        var result = new Crlf { Body = "line2" }.Render(new RenderOptions { Newline = "\r\n" });
        Assert.Equal("line1\r\nline2\r\nline3", result);
    }

    [Fact]
    public void Rendering_the_same_type_repeatedly_renders_each_value()
    {
        for (int i = 0; i < 100; i++)
        {
            var r = new Simple { Verb = "v", Name = i.ToString() }.Render();
            Assert.Equal($"v, {i}!", r);
        }
    }
}
