using Xunit;

namespace Templar.UI.Generators.Tests;

public class TemplateAccessorGeneratorTests
{
    private static string Run(string path, string content)
    {
        var options = new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "App",
            ["build_property.ProjectDir"] = "/proj/",
        };
        var map = GeneratorHarness.Run(new Templar.Generators.TemplateAccessorGenerator(),
                                       new[] { (path, content) },
                                       options);
        return map.Count > 0 ? map.Values.First() : "";
    }

    [Fact]
    public void Generates_typed_compositor_accessor_with_no_runtime_dependency()
    {
        var src = Run("/proj/Templates/Card.tpl", "Hello {{ who }} and {{ place }}");
        Assert.Contains("namespace App.Templates;", src);
        Assert.Contains("public sealed class Card : global::Templar.Rendering.Compositor", src);
        Assert.Contains("public required object? Who { get; init; }", src);
        Assert.Contains("public required object? Place { get; init; }", src);
        Assert.Contains("[global::System.CodeDom.Compiler.GeneratedCode(\"Templar.Generators\", \"1.0.0\")]", src);
    }

    [Fact]
    public void Folder_segments_extend_the_namespace()
    {
        var src = Run("/proj/Templates/Widgets/Button.tpl", "{{ label }}");
        Assert.Contains("namespace App.Templates.Widgets;", src);
        Assert.Contains("public sealed class Button : global::Templar.Rendering.Compositor", src);
        Assert.Contains("public required object? Label { get; init; }", src);
    }

    [Fact]
    public void Template_outside_templates_folder_is_ignored()
    {
        Assert.Equal("", Run("/proj/Other/X.tpl", "{{ a }}"));
    }

    [Fact]
    public void Placeholderless_template_emits_empty_class()
    {
        var src = Run("/proj/Templates/Bare.tpl", "no placeholders here");
        Assert.Contains("public sealed class Bare : global::Templar.Rendering.Compositor", src);
        Assert.DoesNotContain("{ get; init; }", src);
    }
}
