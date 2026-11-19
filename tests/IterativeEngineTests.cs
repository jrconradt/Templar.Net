using Templar.Rendering;
using Templar.Tests.Models.Iterative;
using Xunit;

namespace Templar.Tests;

public class IterativeEngineTests
{
    [Fact]
    public void DeeplyNested_Types_DoNotOverflowStack()
    {
        var leaf = new Nest(null, "X");
        for (int i = 0; i < 500; i++)
            leaf = new Nest(leaf);

        var output = Compositor.ResolveTemplate(leaf);
        Assert.EndsWith("[X]" + new string(']', 500), output);
        Assert.StartsWith(new string('[', 501), output);
    }

    [Fact]
    public void Sequence_ManyItems_DoNotOverflowStack()
    {
        var items = Enumerable.Range(0, 1000)
            .Select(i => new Item(i.ToString()))
            .ToArray();
        var output = Compositor.ResolveTemplate(new Items(Sequence.Lines(items)));
        var lines = output.Split('\n');
        Assert.Equal(1000, lines.Length);
        Assert.Equal("item:0", lines[0]);
        Assert.Equal("item:999", lines[999]);
    }

    [Fact]
    public void Sequence_OfSequences_NestsCorrectly()
    {
        var inner1 = Sequence.CommaList(new[]
        {
            new Item("a"),
            new Item("b"),
        });
        var inner2 = Sequence.CommaList(new[]
        {
            new Item("c"),
            new Item("d"),
        });
        var outer = new Rows(Sequence.Lines(new[] { inner1, inner2 }));

        Assert.Equal("item:a, item:b\nitem:c, item:d", Compositor.ResolveTemplate(outer));
    }

    [Fact]
    public void NestedType_AtIndentedColumn_AlignsContinuationLines()
    {
        var o = new Outer(new TwoLines());
        var expected = "before\n    L1\n    L2\nafter";
        Assert.Equal(expected, Compositor.ResolveTemplate(o));
    }
}
