using Templar.Rendering;
using Templar.UI;
using Xunit;

namespace Templar.UI.Tests;

public class UIComponentTests
{
    private sealed class Greeting : UIComponent
    {
        public string Name { get; init; } = "";

        protected override string Structure => "Hi {{ name }}";
    }

    private sealed class RawGreeting : UIComponent
    {
        public string Name { get; init; } = "";

        protected override string Structure => "Hi {{& name }}";
    }

    [Fact]
    public void Component_escapes_text_by_default()
    {
        Assert.Equal("Hi &lt;x&gt;", new Greeting { Name = "<x>" }.Render());
    }

    [Fact]
    public void Raw_marker_opts_out_of_escaping()
    {
        Assert.Equal("Hi <x>", new RawGreeting { Name = "<x>" }.Render());
    }

    [Fact]
    public void Custom_options_without_escape_still_escape()
    {
        var rendered = new Greeting { Name = "<x>" }
            .WithOptions(new RenderOptions { Newline = "\n" })
            .Render();
        Assert.Equal("Hi &lt;x&gt;", rendered);
    }

    [Fact]
    public void Document_renders_well_formed_page()
    {
        var html = new Document
        {
            Title = "Home",
            Body = Markup.P("hi"),
        }.Render();

        Assert.Contains("<!DOCTYPE html>", html);
        Assert.Contains("<html lang=\"en\">", html);
        Assert.Contains("<title>Home</title>", html);
        Assert.Contains("<p>\n        hi\n    </p>", html);
    }

    [Fact]
    public void Document_escapes_title()
    {
        var html = new Document { Title = "<b>" }.Render();
        Assert.Contains("<title>&lt;b&gt;</title>", html);
    }
}
