using Templar.Rendering;

namespace Templar.UI;

public sealed class Cls : UIComponent
{
    public required string Tokens { get; init; }

    protected override string Structure => "{{ tokens }}";
}
