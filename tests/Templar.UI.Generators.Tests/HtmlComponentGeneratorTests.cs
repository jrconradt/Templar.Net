using Xunit;

namespace Templar.UI.Generators.Tests;

public class HtmlComponentGeneratorTests
{
    private static string Run(string fileName, string content, string? rootNamespace = "MyApp")
    {
        var options = new Dictionary<string, string>();
        if (rootNamespace is not null)
        {
            options["build_property.RootNamespace"] = rootNamespace;
        }
        var map = GeneratorHarness.Run(new HtmlComponentGenerator(), new[] { (fileName, content) }, options);
        return map.Count > 0 ? map.Values.First() : "";
    }

    [Fact]
    public void Placeholders_are_typed_by_marker()
    {
        var src = Run("Card.html.tpl", "<div>{{ title }}{{& body }}{{> slot }}</div>");
        Assert.Contains("namespace MyApp;", src);
        Assert.Contains("class Card : global::Templar.UI.UIComponent", src);
        Assert.Contains("public string Title { get; init; } = \"\";", src);
        Assert.Contains("public global::Templar.UI.RawHtml Body { get; init; }", src);
        Assert.Contains("public global::Templar.Rendering.Compositor? Slot { get; init; }", src);
    }

    [Fact]
    public void Filtered_placeholder_is_typed_as_string()
    {
        Assert.Contains("public string Name { get; init; }", Run("X.html.tpl", "{{ name | upper }}"));
    }

    [Fact]
    public void Comment_and_conditional_markers_are_skipped()
    {
        var src = Run("X.html.tpl", "{{# note }}{{? flag }}a{{?}}{{ keep }}");
        Assert.Contains("public string Keep", src);
        Assert.DoesNotContain("Note", src);
        Assert.DoesNotContain("Flag", src);
    }

    [Fact]
    public void Children_placeholder_is_not_redeclared()
    {
        var src = Run("X.html.tpl", "<div>{{ children }}</div>");
        Assert.DoesNotContain("public string Children", src);
    }

    [Fact]
    public void Missing_root_namespace_produces_no_output()
    {
        Assert.Equal("", Run("X.html.tpl", "{{ a }}", rootNamespace: null));
    }

    [Fact]
    public void Structure_literal_escapes_special_characters()
    {
        var src = Run("X.html.tpl", "q\"b\\c\nd\te\rf");
        Assert.Contains("\\\"", src);
        Assert.Contains("\\\\", src);
        Assert.Contains("\\n", src);
        Assert.Contains("\\t", src);
        Assert.Contains("\\r", src);
    }

    [Fact]
    public void Invalid_identifier_placeholder_is_skipped()
    {
        var src = Run("X.html.tpl", "{{ 1bad }}{{ good }}");
        Assert.Contains("public string Good { get; init; }", src);
        Assert.Equal(1, src.Split("{ get; init; }").Length - 1);
    }

    [Fact]
    public void Escape_sequences_are_skipped_by_scanner()
    {
        var src = Run("X.html.tpl", "\\{{ \\}} \\\\ {{ real }}");
        Assert.Contains("public string Real", src);
    }

    [Fact]
    public void Duplicate_placeholders_emit_one_property()
    {
        var src = Run("X.html.tpl", "{{ a }}{{ a }}");
        var first = src.IndexOf("public string A ", System.StringComparison.Ordinal);
        var last = src.LastIndexOf("public string A ", System.StringComparison.Ordinal);
        Assert.True(first >= 0 && first == last);
    }

    [Fact]
    public void Class_name_is_sanitized_from_file_name()
    {
        Assert.Contains("class My_card", Run("my-card.html.tpl", "{{ x }}"));
    }

    [Fact]
    public void Unclosed_placeholder_stops_scanning()
    {
        var src = Run("X.html.tpl", "before {{ x");
        Assert.Contains("class X", src);
        Assert.DoesNotContain("{ get; init; }", src);
    }

    [Fact]
    public void Empty_placeholder_name_is_skipped()
    {
        var src = Run("X.html.tpl", "{{ }}{{ ok }}");
        Assert.Contains("public string Ok { get; init; }", src);
        Assert.Equal(1, src.Split("{ get; init; }").Length - 1);
    }

    [Fact]
    public void Placeholder_with_invalid_later_char_is_skipped()
    {
        var src = Run("X.html.tpl", "{{ a-b }}{{ ok }}");
        Assert.Contains("public string Ok { get; init; }", src);
        Assert.Equal(1, src.Split("{ get; init; }").Length - 1);
    }

    [Fact]
    public void Empty_leaf_file_name_sanitizes_to_underscore()
    {
        Assert.Contains("public sealed class _ ", Run(".html.tpl", "{{ a }}"));
    }

    [Fact]
    public void Non_html_tpl_file_is_ignored()
    {
        var map = GeneratorHarness.Run(new HtmlComponentGenerator(),
                                       new[] { ("notes.txt", "{{ a }}") },
                                       new Dictionary<string, string> { ["build_property.RootNamespace"] = "MyApp" });
        Assert.Empty(map);
    }
}
