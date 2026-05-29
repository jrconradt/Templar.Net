using Xunit;

namespace Templar.UI.Generators.Tests;

public class ElementFactoryGeneratorTests
{
    private static string Run(string table)
    {
        var map = GeneratorHarness.Run(new ElementFactoryGenerator(), new[] { ("Elements.elements", table) });
        return map.TryGetValue("H.g.cs", out var s) ? s : "";
    }

    [Fact]
    public void Empty_table_produces_no_output()
    {
        var map = GeneratorHarness.Run(new ElementFactoryGenerator(), new[] { ("Elements.elements", "# only a comment\n\n") });
        Assert.False(map.ContainsKey("H.g.cs"));
    }

    [Fact]
    public void All_layouts_emit_correct_shape()
    {
        var src = Run("div : block\nspan : inline\nbr : void\npre : verbatim");
        Assert.Contains("Element Div(object? children = null, string? classes = null, object? attrs = null)", src);
        Assert.Contains("ElementLayout.Block", src);
        Assert.Contains("Element Span(object? children = null, string? classes = null, object? attrs = null)", src);
        Assert.Contains("ElementLayout.Inline", src);
        Assert.Contains("Element Br(string? classes = null, object? attrs = null)", src);
        Assert.Contains("ElementLayout.Void", src);
        Assert.Contains("Element Pre(object? children = null, string? classes = null, object? attrs = null)", src);
        Assert.Contains("ElementLayout.Verbatim", src);
    }

    [Fact]
    public void Default_class_column_is_emitted()
    {
        Assert.Contains("DefaultClass = \"btn\"", Run("button : block : btn"));
    }

    [Fact]
    public void No_default_class_when_column_absent()
    {
        Assert.DoesNotContain("DefaultClass", Run("div : block"));
    }

    [Fact]
    public void Comments_blank_and_malformed_lines_are_skipped()
    {
        var src = Run("# comment\n\nnotalayout\ndiv : block");
        Assert.Contains("Element Div(", src);
        Assert.DoesNotContain("Notalayout", src);
    }

    [Fact]
    public void Multiple_names_on_a_line_each_emit()
    {
        var src = Run("a b c : inline");
        Assert.Contains("Element A(", src);
        Assert.Contains("Element B(", src);
        Assert.Contains("Element C(", src);
    }

    [Fact]
    public void Duplicate_names_emit_once()
    {
        var src = Run("div : block\ndiv : inline");
        var first = src.IndexOf("Element Div(", System.StringComparison.Ordinal);
        var last = src.LastIndexOf("Element Div(", System.StringComparison.Ordinal);
        Assert.True(first >= 0 && first == last);
        Assert.Contains("ElementLayout.Block", src);
    }

    [Fact]
    public void Pascal_handles_single_char_and_digits()
    {
        var src = Run("p : block\nh1 : block");
        Assert.Contains("Element P(", src);
        Assert.Contains("Element H1(", src);
    }

    [Fact]
    public void Generated_class_is_partial()
    {
        Assert.Contains("public static partial class H", Run("div : block"));
    }
}
