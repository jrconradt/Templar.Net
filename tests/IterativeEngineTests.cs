using Templar.Rendering;
using Xunit;

namespace Templar.Tests;

public class IterativeEngineTests
{
    private sealed class Nest : Compositor
    {
        public Compositor? Inner { get; init; }
        public string Leaf { get; init; } = "";
        protected override string Structure => "[{{?inner}}{{inner}}{{?else}}{{leaf}}{{?}}]";
    }

    [Fact]
    public void DeeplyNested_Compositors_DoNotOverflowStack()
    {
        Compositor leaf = new Nest { Leaf = "X" };
        for (int i = 0; i < 500; i++)
            leaf = new Nest { Inner = leaf };

        var output = leaf.Render();
        Assert.EndsWith("[X]" + new string(']', 500), output);
        Assert.StartsWith(new string('[', 501), output);
    }

    private sealed class Item : Compositor
    {
        public string Name { get; init; } = "";
        protected override string Structure => "item:{{name}}";
    }

    [Fact]
    public void Sequence_ManyItems_DoNotOverflowStack()
    {
        var items = Enumerable.Range(0, 1000)
            .Select(i => (Compositor)new Item { Name = i.ToString() })
            .ToArray();
        var seq = Sequence.Lines(items);
        var output = seq.Render();
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
            (Compositor)new Item { Name = "a" },
            new Item { Name = "b" },
        });
        var inner2 = Sequence.CommaList(new[]
        {
            (Compositor)new Item { Name = "c" },
            new Item { Name = "d" },
        });
        var outer = Sequence.Lines(new IComposable[] { inner1, inner2 });

        Assert.Equal("item:a, item:b\nitem:c, item:d", outer.Render());
    }

    private sealed class Outer : Compositor
    {
        public Compositor? Inside { get; init; }
        protected override string Structure => "before\n    {{inside}}\nafter";
    }

    private sealed class TwoLines : Compositor
    {
        protected override string Structure => "L1\nL2";
    }

    [Fact]
    public void NestedCompositor_AtIndentedColumn_AlignsContinuationLines()
    {
        var o = new Outer { Inside = new TwoLines() };
        var expected = "before\n    L1\n    L2\nafter";
        Assert.Equal(expected, o.Render());
    }

    [Fact]
    public void Conditional_Nested_ManyLevels_Iterative()
    {
        var src = string.Concat(Enumerable.Repeat("{{?flag}}A", 200))
                + "X"
                + string.Concat(Enumerable.Repeat("{{?}}", 200));

        var t = Template.Parse(src);
        t["flag"] = true;
        var output = t.Render();
        Assert.Equal(new string('A', 200) + "X", output);
    }
}
