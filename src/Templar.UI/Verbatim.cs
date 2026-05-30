using Templar.Rendering;

namespace Templar.UI;

public readonly struct Verbatim : IPreformattedContent
{
    private readonly string? _value;

    public Verbatim(string value)
    {
        _value = value;
    }

    public string Value => _value ?? "";

    public override string ToString() => Value;
}
