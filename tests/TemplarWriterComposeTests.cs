using Templar.Rendering;
using Templar.Tests.Models.Writer;
using Xunit;

namespace Templar.Tests;

public class TemplarWriterComposeTests
{
    [Fact]
    public void Compose_renders_the_template_typed_value_inside_its_host()
    {
        string rendered = Compositor.ResolveTemplate(new Hosting(new Inner()));
        Assert.Equal("[INNER]", rendered);
    }
}
