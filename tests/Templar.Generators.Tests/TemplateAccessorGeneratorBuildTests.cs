using System.Linq;
using Xunit;

namespace Templar.Generators.Tests;

public class TemplateAccessorGeneratorBuildTests
{
    private static IReadOnlyDictionary<string, string> RunVerified(string path, string content)
    {
        var options = new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "App",
            ["build_property.ProjectDir"] = "/proj/",
        };
        return GeneratorHarness.RunVerified(new Templar.Generators.TemplateAccessorGenerator(),
                                            new[] { (path, content) },
                                            options);
    }

    [Fact]
    public void AdditionalText_tpl_generates_compiling_renderinto_accessor()
    {
        var sources = RunVerified("/proj/Templates/Welcome.tpl",
                                  "Hello {{ who }}, welcome to {{ place }}!");

        var hint = sources.Keys.Single(k => k.EndsWith("Welcome.g.cs"));
        var source = sources[hint];

        Assert.Contains("namespace App.Templates", source);
        Assert.Contains("class Welcome", source);
        Assert.Contains("public override void RenderInto", source);
        Assert.Contains("w.Literal(", source);
        Assert.Contains("w.Value(", source);
        Assert.Contains("Who", source);
        Assert.Contains("Place", source);
    }

    [Fact]
    public void AdditionalText_tpl_in_nested_folder_emits_namespace_segment()
    {
        var sources = RunVerified("/proj/Templates/Email/Reset.tpl",
                                  "Reset link: {{ link }}");

        var hint = sources.Keys.Single(k => k.EndsWith("Reset.g.cs"));
        var source = sources[hint];

        Assert.Contains("namespace App.Templates.Email", source);
        Assert.Contains("class Reset", source);
        Assert.Contains("public override void RenderInto", source);
        Assert.Contains("Link", source);
    }
}
