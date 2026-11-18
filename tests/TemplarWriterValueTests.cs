using Templar.Rendering;
using Xunit;

namespace Templar.Tests;

public class TemplarWriterValueTests
{
    private sealed class Inner : Compositor
    {
        protected override string Structure => "INNER";
    }

    private sealed class Hosting : Compositor
    {
        public required object? Child { get; init; }

        public override void RenderInto(TemplarWriter w)
        {
            w.Compiled(Steps(w));
        }

        private IEnumerator<IComposable> Steps(TemplarWriter w)
        {
            w.Literal("[");
            {
                var c = w.Value(Child);
                if (c is not null)
                {
                    yield return c;
                }
            }
            w.Literal("]");
        }
    }

    [Fact]
    public void Value_composable_content_renders_inner_composition()
    {
        string rendered = new Hosting { Child = new Inner() }.Render();
        Assert.Equal("[INNER]", rendered);
    }
}
