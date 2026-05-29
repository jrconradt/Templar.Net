using System.Collections.Generic;
using Templar.Rendering;
using Xunit;

namespace Templar.Tests;

public class EngineCoverageTests
{
    private sealed class Lit : Compositor
    {
        public string V { get; init; } = "";
        protected override string Structure => "{{ v }}";
    }

    private sealed class Verb : IVerbatimContent
    {
        public Verb(string value)
        {
            Value = value;
        }

        public string Value { get; }
    }

    private sealed class Raw : IRawContent
    {
        public Raw(string value)
        {
            Value = value;
        }

        public string Value { get; }
    }

    private sealed class Angle
    {
        public override string ToString() => "<x>";
    }

    private sealed class NoStructure : Compositor
    {
    }

    private sealed class WriteOnlyProp : Compositor
    {
        private string _w = "";
        public string Sink
        {
            set => _w = value;
        }
        public string Used => _w.Length >= 0 ? "v" : "";
        protected override string Structure => "{{ used }}";
    }

    private static RenderOptions Escaping => new()
    {
        Escape = s => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;"),
    };

    [Fact]
    public void Compositor_value_at_line_start()
    {
        var t = Template.Parse("{{ c }}");
        t["c"] = new Lit { V = "hi" };
        Assert.Equal("hi", t.Render());
    }

    [Fact]
    public void Compositor_sequence_value_at_line_start()
    {
        var t = Template.Parse("{{ items }}");
        t["items"] = new List<Compositor> { new Lit { V = "a" }, new Lit { V = "b" } };
        Assert.Equal("a\nb", t.Render());
    }

    [Fact]
    public void Fallback_value_renders_to_string()
    {
        var t = Template.Parse("{{ x }}");
        t["x"] = 42;
        Assert.Equal("42", t.Render());
    }

    [Fact]
    public void Fallback_value_escaped_and_unescaped()
    {
        var escaped = Template.Parse("{{ x }}").WithOptions(Escaping);
        escaped["x"] = new Angle();
        Assert.Equal("&lt;x&gt;", escaped.Render());

        var plain = Template.Parse("{{ x }}");
        plain["x"] = new Angle();
        Assert.Equal("<x>", plain.Render());
    }

    [Fact]
    public void String_enumerable_value_escaped()
    {
        var t = Template.Parse("{{ x }}").WithOptions(Escaping);
        t["x"] = new[] { "<a>", "<b>" };
        Assert.Equal("&lt;a&gt;\n&lt;b&gt;", t.Render());
    }

    [Fact]
    public void Multiline_value_normalizes_crlf()
    {
        var t = Template.Parse("  {{ x }}");
        t["x"] = "a\r\nb";
        Assert.Equal("  a\n  b", t.Render());
    }

    [Fact]
    public void Verbatim_value_normalizes_crlf_without_reindent()
    {
        var t = Template.Parse("  {{ x }}");
        t["x"] = new Verb("a\r\nb");
        Assert.Equal("  a\nb", t.Render());
    }

    [Fact]
    public void Raw_content_value_is_written_unescaped()
    {
        var t = Template.Parse("{{ x }}").WithOptions(Escaping);
        t["x"] = new Raw("<b>");
        Assert.Equal("<b>", t.Render());
    }

    [Fact]
    public void False_conditional_skips_escapes_in_body()
    {
        var t = Template.Parse("{{? f }}\\\\\\{{\\}}{{?}}done");
        t["f"] = false;
        Assert.Equal("done", t.Render());
    }

    [Fact]
    public void False_conditional_skips_nested_conditional_and_value()
    {
        var t = Template.Parse("{{? f }}x{{? g }}y{{?}}{{ v }}{{?}}done");
        t["f"] = false;
        Assert.Equal("done", t.Render());
    }

    [Fact]
    public void Else_branch_is_taken_when_false()
    {
        var t = Template.Parse("{{? f }}A{{?else}}B{{?}}");
        t["f"] = false;
        Assert.Equal("B", t.Render());
    }

    [Fact]
    public void Else_branch_is_skipped_when_true_with_escapes()
    {
        var t = Template.Parse("{{? f }}A{{?else}}\\\\{{?}}");
        t["f"] = true;
        Assert.Equal("A", t.Render());
    }

    [Fact]
    public void Else_branch_skips_nested_conditional_when_true()
    {
        var t = Template.Parse("{{? f }}A{{?else}}{{? g }}z{{?}}tail{{?}}");
        t["f"] = true;
        Assert.Equal("A", t.Render());
    }

    [Fact]
    public void Negated_conditional()
    {
        var present = Template.Parse("{{?!f}}shown{{?}}");
        present["f"] = false;
        Assert.Equal("shown", present.Render());

        var hidden = Template.Parse("{{?!f}}shown{{?}}");
        hidden["f"] = true;
        Assert.Equal("", hidden.Render());
    }

    [Fact]
    public void Conditional_truthiness_of_enumerable_disposes_enumerator()
    {
        var nonEmpty = Template.Parse("{{? items }}yes{{?}}");
        nonEmpty["items"] = new List<int> { 1 };
        Assert.Equal("yes", nonEmpty.Render());

        var empty = Template.Parse("{{? items }}yes{{?}}");
        empty["items"] = new List<int>();
        Assert.Equal("", empty.Render());
    }

    [Fact]
    public void Tag_spanning_a_newline_parses_and_renders()
    {
        var t = Template.Parse("a{{ x\n}}b");
        t["x"] = "Z";
        Assert.Equal("aZb", t.Render());
    }

    [Fact]
    public void Indexer_get_returns_value_or_null()
    {
        var t = Template.Parse("x");
        Assert.Null(t["missing"]);
        t["k"] = "v";
        Assert.Equal("v", t["k"]);
    }

    [Fact]
    public void Empty_sequence_renders_empty()
    {
        Assert.Equal("", new Lines().Render());
    }

    [Fact]
    public void Comma_list_joins_with_comma_space()
    {
        var list = new CommaList { Items = new Compositor[] { new Lit { V = "a" }, new Lit { V = "b" } } };
        Assert.Equal("a, b", list.Render());
    }

    [Fact]
    public void Unknown_filter_throws_with_diagnostics()
    {
        var t = Template.Parse("{{ x | nope }}");
        t["x"] = "v";
        var ex = Assert.Throws<TemplateRenderException>(() => t.Render());
        Assert.Equal("nope", ex.FilterName);
        Assert.Equal("x", ex.VariableName);
        Assert.Null(ex.TemplateName);
    }

    [Fact]
    public void Compositor_to_string_renders()
    {
        Assert.Equal("hi", new Lit { V = "hi" }.ToString());
    }

    [Fact]
    public void Compositor_without_structure_or_resource_throws()
    {
        Assert.Throws<System.InvalidOperationException>(() => new NoStructure().Render());
    }

    [Fact]
    public void Write_only_property_is_skipped_in_binding()
    {
        Assert.Equal("v", new WriteOnlyProp().Render());
    }

    [Fact]
    public void Embedded_resource_loads_by_full_name()
    {
        var set = new TemplateSet().AddEmbeddedResource(typeof(EngineCoverageTests).Assembly,
                                                        "Templar.Tests.Fixtures.Sample.tpl");
        var t = set.Get("Sample");
        t["who"] = "world";
        Assert.Equal("hello world", t.Render());
    }

    [Fact]
    public void Embedded_templates_load_by_prefix()
    {
        var set = new TemplateSet().AddEmbeddedTemplates(typeof(EngineCoverageTests).Assembly,
                                                         "Templar.Tests.Fixtures.");
        Assert.True(set.Contains("Sample"));
    }

    [Fact]
    public void Embedded_resource_with_single_dot_name()
    {
        var set = new TemplateSet().AddEmbeddedResource(typeof(EngineCoverageTests).Assembly, "Solo.tpl");
        var t = set.Get("Solo");
        t["x"] = "k";
        Assert.Equal("solo k", t.Render());
    }

    [Fact]
    public void Literal_carriage_return_in_source_is_dropped()
    {
        Assert.Equal("a\nb", Template.Parse("a\r\nb").Render());
    }

    [Fact]
    public void Empty_verbatim_value_renders_nothing()
    {
        var t = Template.Parse("[{{ x }}]");
        t["x"] = new Verb("");
        Assert.Equal("[]", t.Render());
    }

    [Fact]
    public void Else_arm_skips_brace_escapes_when_true()
    {
        var t = Template.Parse("{{? f }}A{{?else}}\\{{ \\}}{{?}}");
        t["f"] = true;
        Assert.Equal("A", t.Render());
    }

    [Fact]
    public void False_conditional_skips_nested_if_else()
    {
        var t = Template.Parse("{{? f }}{{? g }}x{{?else}}y{{?}}{{?}}done");
        t["f"] = false;
        Assert.Equal("done", t.Render());
    }

    [Fact]
    public void Blank_lines_sequence_joins_with_blank_line()
    {
        var list = new BlankLines { Items = new Compositor[] { new Lit { V = "a" }, new Lit { V = "b" } } };
        Assert.Equal("a\n\nb", list.Render());
    }
}
