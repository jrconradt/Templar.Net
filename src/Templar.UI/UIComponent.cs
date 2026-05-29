using Templar.Rendering;

namespace Templar.UI;

public abstract class UIComponent : Compositor
{
    public object? Children { get; init; }

    protected override RenderOptions ResolveOptions()
    {
        var options = base.ResolveOptions();
        if (options.Escape is not null)
        {
            return options;
        }
        return new RenderOptions
        {
            IndentString = options.IndentString,
            Newline = options.Newline,
            StrictUndefined = options.StrictUndefined,
            Escape = Html.Escape,
        };
    }
}
