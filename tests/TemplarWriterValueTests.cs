using Templar.Rendering;
using Xunit;

namespace Templar.Tests;

public class TemplarWriterValueTests
{
    private sealed class Filtered : Compositor
    {
        public required object? Name { get; init; }

        public override void RenderInto(TemplarWriter w)
        {
            w.Compiled(Steps(w));
        }

        private IEnumerator<IComposable> Steps(TemplarWriter w)
        {
            {
                var c = w.Value(Name, "upper");
                if (c is not null)
                {
                    yield return c;
                }
            }
        }
    }

    private sealed class Pre : IPreformattedContent
    {
        public required string Value { get; init; }
    }

    private sealed class Preformatted : Compositor
    {
        public required object? Content { get; init; }

        public override void RenderInto(TemplarWriter w)
        {
            w.Compiled(Steps(w));
        }

        private IEnumerator<IComposable> Steps(TemplarWriter w)
        {
            w.Literal("    ");
            {
                var c = w.Value(Content);
                if (c is not null)
                {
                    yield return c;
                }
            }
        }
    }

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
    public void Value_filtered_expression_applies_filter()
    {
        string rendered = new Filtered { Name = "world" }.Render();
        Assert.Equal("WORLD", rendered);
    }

    [Fact]
    public void Value_preformatted_content_writes_verbatim_without_column_indent()
    {
        string rendered = new Preformatted { Content = new Pre { Value = "a\nb" } }.Render();
        Assert.Equal("    a\nb", rendered);
    }

    [Fact]
    public void Value_composable_content_renders_inner_composition()
    {
        string rendered = new Hosting { Child = new Inner() }.Render();
        Assert.Equal("[INNER]", rendered);
    }
}
