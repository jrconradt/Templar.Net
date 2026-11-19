using Templar.Rendering;
using Templar.Tests.Models.Values;
using Xunit;

namespace Templar.Tests;

public class IndentStringTests
{
    [Fact]
    public void Tab_indent_string_reindents_continuation_lines_with_tabs()
    {
        var options = new RenderOptions { IndentString = "\t" };
        Assert.Equal("  line1\n\t\tline2", Compositor.ResolveTemplate(new Text("line1\nline2"), "Values/Indented2", options));
    }

    [Fact]
    public void Default_indent_string_reindents_continuation_lines_with_spaces()
    {
        Assert.Equal("  line1\n  line2", Compositor.ResolveTemplate(new Text("line1\nline2"), "Values/Indented2"));
    }

    [Fact]
    public void Indent_string_unit_repeats_to_fill_full_units()
    {
        var options = new RenderOptions { IndentString = ".." };
        Assert.Equal("    line1\n....line2", Compositor.ResolveTemplate(new Text("line1\nline2"), "Values/Indented4", options));
    }
}
