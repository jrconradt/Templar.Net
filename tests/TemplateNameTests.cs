using Templar.Rendering;
using Templar.Tests.Models.Names;
using Xunit;

namespace Templar.Tests;

// A template's name is its path under Templates/ without .tpl (tests/Templates/Names/Orders/Requisite.tpl is
// "Names/Orders/Requisite"); the types it renders live wherever their code lives, in any namespace.
public sealed class TemplateNameTests
{
    [Fact]
    public void Template_is_named_by_its_path_under_Templates()
    {
        Assert.Equal("requisite R-1", Compositor.ResolveTemplate(new Requisite("R-1"), "Names/Orders/Requisite"));
    }

    [Fact]
    public void Two_unrelated_types_render_through_one_template()
    {
        Assert.Equal("requisite Acme", Compositor.ResolveTemplate(new Vendor("Acme", "Oslo"), "Names/Orders/Requisite"));
        Assert.Equal("requisite R-2", Compositor.ResolveTemplate(new Requisite("R-2"), "Names/Orders/Requisite"));
    }

    [Fact]
    public void Type_attribute_names_its_default_template_in_a_nested_folder()
    {
        Assert.Equal("Templar.Tests.Models.Names.Backordered", typeof(Backordered).FullName);
        Assert.Equal("backordered B-200 eta May", Compositor.ResolveTemplate(new Backordered("B-200", "May")));
    }

    [Fact]
    public void Template_no_pair_reaches_is_not_compiled_in()
    {
        var assembly = typeof(TemplateNameTests).Assembly;

        Assert.NotNull(assembly.GetType("Templar.Tests.Templates.Names.Orders.RequisiteTemplate"));
        Assert.Null(assembly.GetType("Templar.Tests.Templates.Names.UnreachedTemplate"));
    }
}
