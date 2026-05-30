using Xunit;

namespace Templar.UI.Generators.Tests;

public class TemplateLocationBoundaryTests
{
    private static string Run(string path, string projectDir)
    {
        var options = new Dictionary<string, string>
        {
            ["build_property.RootNamespace"] = "App",
            ["build_property.ProjectDir"] = projectDir,
        };
        var map = GeneratorHarness.Run(new Templar.Generators.TemplateAccessorGenerator(),
                                       new[] { (path, "{{ x }}") },
                                       options);
        return map.Count > 0 ? map.Values.First() : "";
    }

    [Fact]
    public void Sibling_project_directory_sharing_a_name_prefix_is_not_matched()
    {
        Assert.Equal("", Run("/work/proj-extra/Templates/Card.tpl", "/work/proj/"));
    }

    [Fact]
    public void Template_inside_the_project_directory_is_matched()
    {
        var src = Run("/work/proj/Templates/Card.tpl", "/work/proj/");
        Assert.Contains("public sealed class Card : global::Templar.Rendering.Compositor", src);
    }
}
