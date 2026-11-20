using Templar.Rendering;
using Templar.Tests.Models.Intercepted;
using Xunit;

namespace Templar.Tests;

// Each ResolveTemplate call the generator resolves to a pair is intercepted in place: it runs that pair's
// compiled renderer and renders the same text, with no registry lookup. Overwriting the registry entry
// for the pair shows the call no longer reads it.
public sealed class InterceptionTests
{
    private const string Name = "Models/Intercepted/Stamp";

    private static IEnumerator<object?> FromRegistry(TemplarWriter w, Stamp stamp)
    {
        w.Literal("from the registry");
        yield break;
    }

    [Fact]
    public void Named_call_renders_its_pair_in_place_without_the_registry()
    {
        RenderRegistry.Register<Stamp>(Name, FromRegistry);

        Assert.Equal("stamp A", Compositor.ResolveTemplate(new Stamp("A"), "Models/Intercepted/Stamp"));
    }

    [Fact]
    public void Default_call_renders_its_Template_pair_in_place_without_the_registry()
    {
        RenderRegistry.Register<Stamp>(Name, FromRegistry);

        Assert.Equal("stamp B", Compositor.ResolveTemplate(new Stamp("B")));
    }
}
