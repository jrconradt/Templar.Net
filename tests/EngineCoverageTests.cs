using Templar.Rendering;
using Templar.Tests.Models.Engine;
using Templar.Tests.Models.Values;
using Xunit;

namespace Templar.Tests;

public class EngineCoverageTests
{
    [Fact]
    public void Template_typed_value_at_line_start()
    {
        Assert.Equal("hi", Compositor.ResolveTemplate(new LitHolder(new Lit("hi"))));
    }

    [Fact]
    public void List_value_at_line_start()
    {
        Assert.Equal("a\nb", Compositor.ResolveTemplate(new LitList([new Lit("a"), new Lit("b")])));
    }

    [Fact]
    public void Number_value_renders_invariant_text()
    {
        Assert.Equal("42", Compositor.ResolveTemplate(new Number(42), "Values/X"));
    }

    [Fact]
    public void Multiline_value_normalizes_crlf()
    {
        Assert.Equal("  a\n  b", Compositor.ResolveTemplate(new Text("a\r\nb"), "Values/Indented2"));
    }

    [Fact]
    public void Tag_spanning_a_newline_parses_and_renders()
    {
        Assert.Equal("aZb", Compositor.ResolveTemplate(new Text("Z"), "Engine/SpanningTag"));
    }

    [Fact]
    public void Empty_sequence_renders_empty()
    {
        Assert.Equal("", Compositor.ResolveTemplate(new LitSequence(Sequence.Lines(Array.Empty<Lit>()))));
    }

    [Fact]
    public void Comma_list_joins_with_comma_space()
    {
        var list = Sequence.CommaList(new[] { new Lit("a"), new Lit("b") });
        Assert.Equal("a, b", Compositor.ResolveTemplate(new LitSequence(list)));
    }

    [Fact]
    public void Write_only_property_is_skipped_in_binding()
    {
        Assert.Equal("v", Compositor.ResolveTemplate(new WriteOnlyProp()));
    }

    [Fact]
    public void Blank_lines_sequence_joins_with_blank_line()
    {
        var list = Sequence.BlankLines(new[] { new Lit("a"), new Lit("b") });
        Assert.Equal("a\n\nb", Compositor.ResolveTemplate(new LitSequence(list)));
    }
}
