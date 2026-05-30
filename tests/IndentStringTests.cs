using Templar.Rendering;
using Xunit;

namespace Templar.Tests;

public class IndentStringTests
{
    [Fact]
    public void Tab_indent_string_reindents_continuation_lines_with_tabs()
    {
        var t = Template.Parse("  {{ x }}").WithOptions(new RenderOptions
        {
            IndentString = "\t",
        });
        t["x"] = "line1\nline2";
        Assert.Equal("  line1\n\t\tline2", t.Render());
    }

    [Fact]
    public void Default_indent_string_reindents_continuation_lines_with_spaces()
    {
        var t = Template.Parse("  {{ x }}");
        t["x"] = "line1\nline2";
        Assert.Equal("  line1\n  line2", t.Render());
    }

    [Fact]
    public void Indent_string_unit_repeats_to_fill_full_units()
    {
        var t = Template.Parse("    {{ x }}").WithOptions(new RenderOptions
        {
            IndentString = "..",
        });
        t["x"] = "line1\nline2";
        Assert.Equal("    line1\n....line2", t.Render());
    }
}
