using Templar.Rendering;

namespace Templar.UI;

public readonly struct RawHtml : IIndentedContent
{
    private readonly string? _value;

    public RawHtml(string value)
    {
        _value = value;
    }

    public string Value => _value ?? "";

    public override string ToString() => Value;

    public static RawHtml Of(string value)
    {
        return new RawHtml(value);
    }
}
