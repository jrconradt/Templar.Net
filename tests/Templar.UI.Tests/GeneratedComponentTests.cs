using Templar.UI;
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
            BodyHtml = Html.Raw("<p>raw &amp; ready</p>"),
            Footer = H.Span("footer"),
        };

        var html = card.Render();

        Assert.Contains("<h2>Hi &lt;there&gt;</h2>", html);
        Assert.Contains("<p>raw &amp; ready</p>", html);
        Assert.Contains("<span>footer</span>", html);
        Assert.StartsWith("<article class=\"card\">", html);
    }
}
