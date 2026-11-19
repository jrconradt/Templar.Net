using Templar.Tests.DefaultNaming.Orders;
using Templar.Tests.DefaultNaming.Orders.Lines;
using Xunit;

namespace Templar.Tests;

// The documented default-naming path (_docs/integration.md "Finding the type of a template",
// _docs/composition.md): each type's .cs lives under DefaultNaming/ and its .tpl under Templates/ at the
// type's namespace path below the root namespace (Templates/DefaultNaming/...); the .tpl files are
// AdditionalFiles, so the generator resolves each to <RootNamespace>.<folder path after Templates/>.<file name>
// and emits <Type>Renderer in the type's namespace.
public sealed class DefaultNamingTests
{
    [Fact]
    public void Type_renders_by_the_tpl_at_its_namespace_path_under_Templates()
    {
        Assert.Equal("Templar.Tests.DefaultNaming.Orders.Requisite", typeof(Requisite).FullName);
        Assert.Equal(typeof(Requisite).Namespace, typeof(RequisiteRenderer).Namespace);
        Assert.Equal("requisite R-1", new Requisite { Name = "R-1" }.Render());
    }

    [Fact]
    public void Type_in_a_nested_namespace_renders_by_the_tpl_at_its_namespace_path_under_Templates()
    {
        var line = new Backordered { Sku = "B-200", Eta = "May" };

        Assert.Equal("Templar.Tests.DefaultNaming.Orders.Lines.Backordered", typeof(Backordered).FullName);
        Assert.Equal("backordered B-200 eta May", line.Render());
    }

    [Fact]
    public void Type_with_no_template_has_no_renderer()
    {
        var assembly = typeof(Untemplated).Assembly;

        Assert.NotNull(assembly.GetType("Templar.Tests.DefaultNaming.Orders.RequisiteRenderer"));
        Assert.Null(assembly.GetType("Templar.Tests.DefaultNaming.Orders.UntemplatedRenderer"));
    }
}
