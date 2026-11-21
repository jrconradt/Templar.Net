using Templar.Rendering;
using Templar.Tests.Models.Intercepted;
using Xunit;

namespace Templar.Tests;

// Each ResolveTemplate call is intercepted in place: it runs its pair's compiled renderer. Compositor's own
// bodies only throw, so a call that renders text was intercepted.
public sealed class InterceptionTests
{
    [Fact]
    public void Named_call_renders_its_pair_in_place()
    {
        Assert.Equal("stamp A", Compositor.ResolveTemplate(new Stamp("A"), "Models/Intercepted/Stamp"));
    }

    [Fact]
    public void Default_call_renders_its_Template_pair_in_place()
    {
        Assert.Equal("stamp B", Compositor.ResolveTemplate(new Stamp("B")));
    }
}
