using System.IO;
using Templar.Rendering;
using Xunit;

namespace Templar.Tests;

public class TemplateSetTests
{
    [Fact]
    public void AddAndGetByName()
    {
        var set = new TemplateSet().Add("greeting", "Hello, {{ who }}!");
        var t = set.Get("greeting");
        t["who"] = "team";
        Assert.Equal("Hello, team!", t.Render());
    }

    [Fact]
    public void ContainsReportsMembership()
    {
        var set = new TemplateSet().Add("a", "x");
        Assert.True(set.Contains("a"));
        Assert.True(set.Contains("A")); // case-insensitive
        Assert.False(set.Contains("b"));
    }

    [Fact]
    public void GetMissingThrows()
    {
        var set = new TemplateSet();
        Assert.Throws<KeyNotFoundException>(() => set.Get("nope"));
    }

    [Fact]
    public void AddDirectoryLoadsTplFiles()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"templar-set-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "first.tpl"),  "{{ x }}");
            File.WriteAllText(Path.Combine(dir, "second.tpl"), "[{{ x }}]");
            File.WriteAllText(Path.Combine(dir, "ignored.txt"), "not loaded");

            var set = new TemplateSet().AddDirectory(dir);
            Assert.True(set.Contains("first"));
            Assert.True(set.Contains("second"));
            Assert.False(set.Contains("ignored"));

            var t = set.Get("second");
            t["x"] = "Y";
            Assert.Equal("[Y]", t.Render());
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void FiltersPropagateFromSetToTemplate()
    {
        var set = new TemplateSet()
            .Add("t", "{{ x | yell }}")
            .AddFilter("yell", v => (v?.ToString() ?? "") + "!");

        var t = set.Get("t");
        t["x"] = "hey";
        Assert.Equal("hey!", t.Render());
    }

    [Fact]
    public void OptionsPropagateFromSetToTemplate()
    {
        var set = new TemplateSet()
            .Add("t", "a\n{{ x }}\nb")
            .WithOptions(new RenderOptions { Newline = "\r\n" });

        var t = set.Get("t");
        t["x"] = "MID";
        Assert.Equal("a\r\nMID\r\nb", t.Render());
    }
}
