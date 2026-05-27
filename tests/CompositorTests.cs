using Templar.Rendering;
using Xunit;

namespace Templar.Tests;

public class CompositorTests
{
    private class SimpleComposite : Compositor
    {
        public string Name { get; init; } = "";
        public string Verb { get; init; } = "";
        protected override string Structure => "{{ verb }}, {{ name }}!";
    }

    [Fact]
    public void AutoBindsPropertiesByName()
    {
        var result = new SimpleComposite { Name = "World", Verb = "Hello" }.Render();
        Assert.Equal("Hello, World!", result);
    }

    private sealed class RenamedComposite : Compositor
    {
        [TemplateBind("greeting")]
        public string MyMessage { get; init; } = "";
        protected override string Structure => "{{ greeting }}";
    }

    [Fact]
    public void TemplateBindRenamesVariable()
    {
        var result = new RenamedComposite { MyMessage = "Salaam" }.Render();
        Assert.Equal("Salaam", result);
    }

    private sealed class IgnoredComposite : Compositor
    {
        public string Visible { get; init; } = "VISIBLE";
        [TemplateIgnore]
        public string Hidden { get; init; } = "SHOULD_NOT_APPEAR";
        protected override string Structure => "{{ visible }} / {{ hidden }}";
    }

    [Fact]
    public void TemplateIgnoreExcludesProperty()
    {
        var result = new IgnoredComposite().Render();
        // hidden is unset → empty
        Assert.Equal("VISIBLE / ", result);
    }

    private sealed class IndexedComposite : Compositor
    {
        public string this[int i] => "INDEXED";
        public string Real { get; init; } = "value";
        protected override string Structure => "{{ real }}";
    }

    [Fact]
    public void IndexerPropertiesSkipped()
    {
        // The indexer used to throw "Parameter count mismatch" on
        // PropertyInfo.GetValue(this) before we filtered indexers.
        var result = new IndexedComposite().Render();
        Assert.Equal("value", result);
    }

    private sealed class InheritingComposite : SimpleComposite
    {
        public string Suffix { get; init; } = "";
    }

    [Fact]
    public void InheritedPropertiesAutoBind()
    {
        // InheritingComposite reuses SimpleComposite's Structure but adds a
        // Suffix property (auto-bound by name). Template doesn't reference
        // {{ suffix }}, so it's just a smoke test that inheritance doesn't
        // break the auto-bind reflection walk.
        var result = new InheritingComposite { Verb = "Hi", Name = "team", Suffix = "!!" }.Render();
        Assert.Equal("Hi, team!", result);
    }

    private sealed class CachedComposite : Compositor
    {
        public string Value { get; init; } = "";
        protected override string Structure => "{{ value }}";
    }

    [Fact]
    public void RenderingSameTypeRepeatedlyReusesParseCache()
    {
        // Sanity smoke: 100 instances render correctly with the per-type cache.
        for (int i = 0; i < 100; i++)
        {
            var r = new CachedComposite { Value = i.ToString() }.Render();
            Assert.Equal(i.ToString(), r);
        }
    }

    private sealed class OverridingComposite : Compositor
    {
        public string A { get; init; } = "auto-A";
        public string B { get; init; } = "auto-B";
        protected override string Structure => "{{ a }}-{{ b }}-{{ c }}";
        protected override void Populate(Template template)
        {
            base.Populate(template);          // auto-bind A and B
            template["c"] = "manual-C";       // then add C by hand
        }
    }

    [Fact]
    public void PopulateOverrideExtendsAutoBinding()
    {
        var result = new OverridingComposite().Render();
        Assert.Equal("auto-A-auto-B-manual-C", result);
    }

    private sealed class CrlfComposite : Compositor
    {
        public string Body { get; init; } = "";
        protected override string Structure => "line1\n{{ body }}\nline3";
    }

    [Fact]
    public void WithOptionsAppliesNewline()
    {
        var result = new CrlfComposite { Body = "line2" }
            .WithOptions(new RenderOptions { Newline = "\r\n" })
            .Render();
        Assert.Equal("line1\r\nline2\r\nline3", result);
    }
}
