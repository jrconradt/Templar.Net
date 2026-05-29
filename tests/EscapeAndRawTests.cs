using Templar.Rendering;
using Xunit;

namespace Templar.Tests;

public class EscapeAndRawTests
{
    private sealed class Raw : IRawContent
    {
        public Raw(string value)
        {
            Value = value;
        }

        public string Value { get; }
    }

    private static RenderOptions Escaping => new()
    {
        Escape = s => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;"),
    };

    [Fact]
    public void Plain_placeholder_is_escaped_when_escape_is_set()
    {
        var t = Template.Parse("{{ x }}").WithOptions(Escaping);
        t["x"] = "<b>&</b>";
        Assert.Equal("&lt;b&gt;&amp;&lt;/b&gt;", t.Render());
    }

    [Fact]
    public void Raw_marker_bypasses_escape()
    {
        var t = Template.Parse("{{& x }}").WithOptions(Escaping);
        t["x"] = "<b>";
        Assert.Equal("<b>", t.Render());
    }

    [Fact]
    public void RawContent_value_bypasses_escape()
    {
        var t = Template.Parse("{{ x }}").WithOptions(Escaping);
        t["x"] = new Raw("<b>");
        Assert.Equal("<b>", t.Render());
    }

    [Fact]
    public void No_escape_by_default_keeps_codegen_behavior()
    {
        var t = Template.Parse("{{ x }}");
        t["x"] = "<b>";
        Assert.Equal("<b>", t.Render());
    }

    [Fact]
    public void Slot_marker_renders_variable()
    {
        var t = Template.Parse("{{> slot }}").WithOptions(Escaping);
        t["slot"] = "plain";
        Assert.Equal("plain", t.Render());
    }

    [Fact]
    public void Filter_output_is_escaped_when_escape_is_set()
    {
        var t = Template.Parse("{{ x | upper }}").WithOptions(Escaping);
        t["x"] = "<a>";
        Assert.Equal("&lt;A&gt;", t.Render());
    }

    [Fact]
    public void Raw_marker_preserves_column_for_multiline_value()
    {
        var t = Template.Parse("    {{& x }}").WithOptions(Escaping);
        t["x"] = "line1\nline2";
        Assert.Equal("    line1\n    line2", t.Render());
    }
}
