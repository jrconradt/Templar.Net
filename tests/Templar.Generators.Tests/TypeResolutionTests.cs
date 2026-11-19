using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Templar.Generators.Tests;

public class TypeResolutionTests
{
    [Fact]
    public void Template_at_the_Templates_root_resolves_the_root_namespace_type()
    {
        var run = GeneratorHarness.Generate(new[] { "namespace App; public sealed class Card { public required string Who { get; init; } }" },
                                            new[] { ("/proj/Templates/Card.tpl", "Hello {{ who }}") });

        Assert.Empty(run.Diagnostics);
        Assert.Equal(new[] { "App.Card.g.cs" }, run.Sources.Keys.ToArray());
        Assert.Contains("namespace App;", run.Sources["App.Card.g.cs"]);
        Assert.Contains("public static class CardRenderer", run.Sources["App.Card.g.cs"]);
    }

    [Fact]
    public void Folder_segments_extend_the_namespace()
    {
        var run = GeneratorHarness.Generate(new[] { "namespace App.Widgets.Forms; public sealed class Button { public required string Label { get; init; } }" },
                                            new[] { ("/proj/Templates/Widgets/Forms/Button.tpl", "{{ label }}") });

        Assert.Empty(run.Diagnostics);
        Assert.Contains("namespace App.Widgets.Forms;", run.Sources["App.Widgets.Forms.Button.g.cs"]);
    }

    [Fact]
    public void Template_under_Templates_resolves_the_namespace_path_type_wherever_its_code_lives()
    {
        var code = CSharpSyntaxTree.ParseText("namespace App.Injection; public sealed class Constructor { public required string Name { get; init; } }",
                                              GeneratorHarness.ParseOptions,
                                              path: "/proj/Wiring/Emit/Constructor.cs");
        var compilation = GeneratorHarness.Compile(Array.Empty<string>()).AddSyntaxTrees(code);
        var driver = GeneratorHarness.CreateDriver(new[] { ("/proj/Templates/Injection/Constructor.tpl", "ctor {{ name }}") });

        var run = GeneratorHarness.Advance(driver, compilation);

        Assert.Empty(run.Diagnostics);
        Assert.Equal(new[] { "App.Injection.Constructor.g.cs" }, run.Sources.Keys.ToArray());
        Assert.Contains("namespace App.Injection;", run.Sources["App.Injection.Constructor.g.cs"]);
        Assert.Contains("w.Text(model.Name);", run.Sources["App.Injection.Constructor.g.cs"]);
    }

    [Fact]
    public void Folder_names_are_sanitized_to_identifiers()
    {
        var run = GeneratorHarness.Generate(new[] { "namespace App.my_widgets; public sealed class Button { }" },
                                            new[] { ("/proj/Templates/my-widgets/Button.tpl", "button") });

        Assert.Empty(run.Diagnostics);
        Assert.Contains("App.my_widgets.Button.g.cs", run.Sources.Keys);
    }

    [Fact]
    public void Sealed_record_resolves()
    {
        var run = GeneratorHarness.Generate(new[] { "namespace App; public sealed record Tag(string Name);" },
                                            new[] { ("/proj/Templates/Tag.tpl", "#{{ name }}") });

        Assert.Empty(run.Diagnostics);
        Assert.Contains("w.Text(model.Name);", run.Sources["App.Tag.g.cs"]);
    }

    [Fact]
    public void Internal_type_gets_an_internal_renderer()
    {
        var run = GeneratorHarness.Generate(new[] { "namespace App; internal sealed class Card { }" },
                                            new[] { ("/proj/Templates/Card.tpl", "card") });

        Assert.Empty(run.Diagnostics);
        Assert.Contains("internal static class CardRenderer", run.Sources["App.Card.g.cs"]);
    }

    [Fact]
    public void Type_from_a_referenced_assembly_is_not_resolved()
    {
        var run = GeneratorHarness.Generate(new[] { "namespace App; public sealed class Other { }" },
                                            new[] { ("/proj/Templates/Template.tpl", "x") },
                                            rootNamespace: "Templar.Rendering");

        Assert.Equal("TMPLR004", Assert.Single(run.Diagnostics).Id);
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Sibling_project_directory_sharing_a_name_prefix_is_outside_the_project()
    {
        var run = GeneratorHarness.Generate(new[] { "namespace App; public sealed class Card { }" },
                                            new[] { ("/work/proj-extra/Card.tpl", "card") },
                                            projectDir: "/work/proj/");

        Assert.Equal("TMPLR003", Assert.Single(run.Diagnostics).Id);
        Assert.Empty(run.Sources);
    }

    [Fact]
    public void Template_inside_the_project_Templates_folder_is_matched()
    {
        var run = GeneratorHarness.Generate(new[] { "namespace App; public sealed class Card { }" },
                                            new[] { ("/work/proj/Templates/Card.tpl", "card") },
                                            projectDir: "/work/proj/");

        Assert.Empty(run.Diagnostics);
        Assert.Contains("App.Card.g.cs", run.Sources.Keys);
    }
}
