using Templar.Tests.Models.Writer;
using Xunit;

namespace Templar.Tests;

public class TemplarWriterComposeTests
{
    [Fact]
    public void Compose_renders_the_mapped_type_inside_its_host()
    {
        string rendered = new Hosting { Child = new Inner() }.Render();
        Assert.Equal("[INNER]", rendered);
    }
}
