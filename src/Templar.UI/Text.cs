using Templar.Rendering;

namespace Templar.UI;

public sealed class Text : UIComponent
{
    public object? Value { get; init; }

    protected override string Structure => "{{ value }}";
}
