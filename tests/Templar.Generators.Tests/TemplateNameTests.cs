using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Templar.Generators.Tests;

// A template's name is its path under <ProjectDir>/Templates/ without .tpl. Its compiled renderers live in
// <RootNamespace>.Templates.<folders>.<Leaf>Template; the types it renders live wherever their code lives.
public class TemplateNameTests
{
    [Fact]
    public void Template_name_is_its_path_under_Templates_without_the_extension()
    {
        var run = GeneratorHarness.Generate(new[] { "namespace App; [Templar.Rendering.Template(\"Widgets/Forms/Button\")] public sealed record Button(string Label);" },
                                            new[] { ("/proj/Templates/Widgets/Forms/Button.tpl", "{{ label }}") });

        Assert.Empty(run.Diagnostics);
        Assert.Equal(new[] { "Widgets.Forms.Button.g.cs" }, run.Sources.Keys.ToArray());
        var source = run.Sources["Widgets.Forms.Button.g.cs"];
        Assert.Contains("namespace App.Templates.Widgets.Forms;", source);
        Assert.Contains("internal static class ButtonTemplate", source);
        Assert.Contains("RenderRegistry.Register<global::App.Button>(\"Widgets/Forms/Button\", Steps);", source);
    }

    [Fact]
    public void A_template_renders_a_type_wherever_its_code_lives()
    {
        var code = CSharpSyntaxTree.ParseText("namespace Elsewhere.Deep; [Templar.Rendering.Template(\"Injection/Constructor\")] public sealed record Ctor(string Name);",
                                              GeneratorHarness.ParseOptions,
                                              path: "/proj/Wiring/Emit/Ctor.cs");
        var compilation = GeneratorHarness.Compile(Array.Empty<string>()).AddSyntaxTrees(code);
        var driver = GeneratorHarness.CreateDriver(new[] { ("/proj/Templates/Injection/Constructor.tpl", "ctor {{ name }}") });

        var run = GeneratorHarness.Advance(driver, compilation);

        Assert.Empty(run.Diagnostics);
        var source = run.Sources["Injection.Constructor.g.cs"];
        Assert.Contains("namespace App.Templates.Injection;", source);
        Assert.Contains("Steps(global::Templar.Rendering.TemplarWriter w, global::Elsewhere.Deep.Ctor model)", source);
        Assert.Contains("w.Text(model.Name);", source);
    }

    [Fact]
    public void Generated_namespace_follows_the_root_namespace()
    {
        var run = GeneratorHarness.Generate(new[] { "namespace App; [Templar.Rendering.Template(\"Card\")] public sealed record Card;" },
                                            new[] { ("/proj/Templates/Card.tpl", "card") },
                                            rootNamespace: "Shop.Front");

        Assert.Empty(run.Diagnostics);
        Assert.Contains("namespace Shop.Front.Templates;", run.Sources["Card.g.cs"]);
    }

    [Fact]
    public void Folder_names_are_sanitized_into_the_generated_namespace_and_kept_in_the_name()
    {
        var run = GeneratorHarness.Generate(new[] { "namespace App; [Templar.Rendering.Template(\"my-widgets/Button\")] public sealed record Button;" },
                                            new[] { ("/proj/Templates/my-widgets/Button.tpl", "button") });

        Assert.Empty(run.Diagnostics);
        var source = run.Sources["my_widgets.Button.g.cs"];
        Assert.Contains("namespace App.Templates.my_widgets;", source);
        Assert.Contains("(\"my-widgets/Button\", Steps);", source);
    }

    [Fact]
    public void Renderers_are_internal_whatever_the_accessibility_of_the_type()
    {
        var run = GeneratorHarness.Generate(new[] { "namespace App; [Templar.Rendering.Template(\"Card\")] public sealed record Card; [Templar.Rendering.Template(\"Note\")] internal sealed record Note;" },
                                            new[] { ("/proj/Templates/Card.tpl", "card"), ("/proj/Templates/Note.tpl", "note") });

        Assert.Empty(run.Diagnostics);
        Assert.Contains("internal static class CardTemplate", run.Sources["Card.g.cs"]);
        Assert.Contains("internal static class NoteTemplate", run.Sources["Note.g.cs"]);
        Assert.DoesNotContain(run.Sources.Values, s => s.Contains("public "));
    }

    [Fact]
    public void Sibling_project_directory_sharing_a_name_prefix_is_outside_the_project()
    {
        var run = GeneratorHarness.Generate(new[] { "namespace App;" },
                                            new[] { ("/work/proj-extra/Card.tpl", "card") },
                                            projectDir: "/work/proj/");

        Assert.Equal("TMPLR003", Assert.Single(run.Diagnostics).Id);
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Template_inside_the_project_Templates_folder_is_named()
    {
        var run = GeneratorHarness.Generate(new[] { "namespace App; [Templar.Rendering.Template(\"Card\")] public sealed record Card;" },
                                            new[] { ("/work/proj/Templates/Card.tpl", "card") },
                                            projectDir: "/work/proj/");

        Assert.Empty(run.Diagnostics);
        Assert.Contains("Card.g.cs", run.Sources.Keys);
    }
}
