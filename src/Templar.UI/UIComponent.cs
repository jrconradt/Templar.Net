using Templar.Rendering;

namespace Templar.UI;

public abstract class UIComponent : Compositor
{
    public object? Children { get; init; }

    protected virtual void Validate()
    {
    }

    internal override void Compile(out string source,
                                   out IDictionary<string, object?> values,
                                   out FilterRegistry filters,
                                   out RenderOptions options)
    {
        Validate();
        base.Compile(out source,
                     out values,
                     out filters,
                     out options);

        if (options.Escape is null)
        {
            options = new RenderOptions
            {
                IndentString = options.IndentString,
                Newline = options.Newline,
                StrictUndefined = options.StrictUndefined,
                Escape = Html.Escape,
            };
        }
    }
}
