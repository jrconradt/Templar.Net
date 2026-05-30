using Templar.UI;
using Templar.UI.Tests.Components;
using Xunit;

namespace Templar.UI.Tests;

public class GeneratedComponentTests
{
    [Fact]
    public void Generated_component_types_each_placeholder()
    {
        var card = new Card
        {
            Title = "Hi <there>",
            BodyHtml = Markup.Raw("<p>raw &amp; ready</p>"),
            Footer = Markup.Span("footer"),
        };

        var html = card.Render();

        Assert.Contains("<h2>Hi &lt;there&gt;</h2>", html);
        Assert.Contains("<p>raw &amp; ready</p>", html);
        Assert.Contains("<span>footer</span>", html);
        Assert.StartsWith("<article class=\"card\">", html);
    }
}
