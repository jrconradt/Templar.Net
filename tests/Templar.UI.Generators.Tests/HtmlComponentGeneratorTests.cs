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
        var map = GeneratorHarness.RunVerified(new HtmlComponentGenerator(), new[] { (fileName, content) }, options);
        return map.Count > 0 ? map.Values.First() : "";
    }

    [Fact]
    public void Placeholders_are_typed_by_marker()
    {
        var src = Run("Card.html.tpl", "<div>{{ title }}{{& body }}{{> slot }}</div>");
        Assert.Contains("namespace MyApp;\n", src);
        Assert.Contains("public sealed class Card : global::Templar.UI.UIComponent\n", src);
        Assert.Contains("    public string Title { get; init; } = \"\";\n", src);
        Assert.Contains("    public global::Templar.UI.RawHtml Body { get; init; }\n", src);
        Assert.Contains("    public global::Templar.Rendering.Compositor? Slot { get; init; }\n", src);
    }

    [Fact]
    public void Filtered_placeholder_is_typed_as_string()
    {
        Assert.Contains("    public string Name { get; init; } = \"\";\n", Run("X.html.tpl", "{{ name | upper }}"));
    }

    [Fact]
    public void Comment_and_conditional_markers_are_skipped()
    {
        var src = Run("X.html.tpl", "{{# note }}{{? flag }}a{{?}}{{ keep }}");
        Assert.Contains("    public string Keep { get; init; } = \"\";\n", src);
        Assert.DoesNotContain("Note", src);
        Assert.DoesNotContain("Flag", src);
    }

    [Fact]
    public void Children_placeholder_is_not_redeclared()
    {
        var src = Run("X.html.tpl", "<div>{{ children }}</div>");
        Assert.DoesNotContain("Children", src);
        Assert.DoesNotContain("{ get; init; }", src);
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
        Assert.Contains("Structure => \"q\\\"b\\\\c\\nd\\te\\rf\";", src);
    }

    [Fact]
    public void Invalid_identifier_placeholder_is_skipped()
    {
        var src = Run("X.html.tpl", "{{ 1bad }}{{ good }}");
        Assert.Contains("    public string Good { get; init; } = \"\";\n", src);
        Assert.Equal(1, src.Split("{ get; init; }").Length - 1);
    }

    [Fact]
    public void Escape_sequences_are_skipped_by_scanner()
    {
        var src = Run("X.html.tpl", "\\{{ \\}} \\\\ {{ real }}");
        Assert.Contains("    public string Real { get; init; } = \"\";\n", src);
        Assert.Equal(1, src.Split("{ get; init; }").Length - 1);
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
        Assert.Contains("public sealed class My_card : global::Templar.UI.UIComponent\n", Run("my-card.html.tpl", "{{ x }}"));
    }

    [Fact]
    public void Unclosed_placeholder_stops_scanning()
    {
        var src = Run("X.html.tpl", "before {{ x");
        Assert.Contains("public sealed class X : global::Templar.UI.UIComponent\n", src);
        Assert.DoesNotContain("{ get; init; }", src);
    }

    [Fact]
    public void Empty_placeholder_name_is_skipped()
    {
        var src = Run("X.html.tpl", "{{ }}{{ ok }}");
        Assert.Contains("    public string Ok { get; init; } = \"\";\n", src);
        Assert.Equal(1, src.Split("{ get; init; }").Length - 1);
    }

    [Fact]
    public void Placeholder_with_invalid_later_char_is_skipped()
    {
        var src = Run("X.html.tpl", "{{ a-b }}{{ ok }}");
        Assert.Contains("    public string Ok { get; init; } = \"\";\n", src);
        Assert.Equal(1, src.Split("{ get; init; }").Length - 1);
    }

    [Fact]
    public void Empty_leaf_file_name_sanitizes_to_underscore()
    {
        Assert.Contains("public sealed class _ : global::Templar.UI.UIComponent\n", Run(".html.tpl", "{{ a }}"));
    }

    [Fact]
    public void Non_html_tpl_file_is_ignored()
    {
        var map = GeneratorHarness.RunVerified(new HtmlComponentGenerator(),
                                               new[] { ("notes.txt", "{{ a }}") },
                                               new Dictionary<string, string> { ["build_property.RootNamespace"] = "MyApp" });
        Assert.Empty(map);
    }

    [Fact]
    public void Same_leaf_in_different_folders_emits_two_distinct_compilable_outputs()
    {
        var options = new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "MyApp",
            ["build_property.ProjectDir"] = "/proj/",
        };
        var map = GeneratorHarness.RunVerified(new HtmlComponentGenerator(),
                                               new[]
                                               {
                                                   ("/proj/A/Card.html.tpl", "<div>{{ a }}</div>"),
                                                   ("/proj/B/Card.html.tpl", "<span>{{ b }}</span>"),
                                               },
                                               options);

        Assert.Equal(2, map.Count);
        Assert.True(map.ContainsKey("A.Card.g.cs"));
        Assert.True(map.ContainsKey("B.Card.g.cs"));

        Assert.Contains("namespace MyApp.A;\n", map["A.Card.g.cs"]);
        Assert.Contains("public sealed class Card : global::Templar.UI.UIComponent\n", map["A.Card.g.cs"]);
        Assert.Contains("    public string A { get; init; } = \"\";\n", map["A.Card.g.cs"]);

        Assert.Contains("namespace MyApp.B;\n", map["B.Card.g.cs"]);
        Assert.Contains("public sealed class Card : global::Templar.UI.UIComponent\n", map["B.Card.g.cs"]);
        Assert.Contains("    public string B { get; init; } = \"\";\n", map["B.Card.g.cs"]);
    }
}
