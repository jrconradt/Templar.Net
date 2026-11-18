using Templar.Tests.DefaultNaming.Orders;
using Templar.Tests.DefaultNaming.Orders.Lines;
using Xunit;

namespace Templar.Tests;

// The documented default-naming path (_docs/integration.md "Finding a template for a type",
// _docs/composition.md): each type sits beside its .tpl under DefaultNaming/, its namespace
// follows its folder, and the .tpl files are embedded by a glob with no LogicalName, so each
// resource carries the SDK's default name <RootNamespace>.<folder path>.<file name>.
public sealed class DefaultNamingTests
{
    [Fact]
    public void Type_whose_namespace_follows_its_folder_finds_the_tpl_beside_it_by_default_resource_name()
    {
        var resources = typeof(Requisite).Assembly.GetManifestResourceNames();

        Assert.Equal("Templar.Tests.DefaultNaming.Orders.Requisite", typeof(Requisite).FullName);
        Assert.Contains("Templar.Tests.DefaultNaming.Orders.Requisite.tpl", resources);
        Assert.Equal("requisite R-1", new Requisite { Name = "R-1" }.Render());
    }

    [Fact]
    public void Subtype_with_its_own_tpl_renders_by_its_own_template()
    {
        var line = new Backordered { Sku = "B-200", Eta = "May" };

        Assert.Equal("backordered B-200 eta May", line.Render());
    }

    [Fact]
    public void Subtype_without_a_tpl_renders_by_its_nearest_base_template_with_its_own_properties_bound()
    {
        var line = new Counted { Sku = "A-100", Quantity = 3 };

        Assert.Equal("line A-100 x3", line.Render());
    }

    [Fact]
    public void Type_with_no_template_found_throws_InvalidOperationException_naming_the_type()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new Untemplated().Render());

        Assert.Contains("Templar.Tests.DefaultNaming.Orders.Untemplated", ex.Message);
    }
}
