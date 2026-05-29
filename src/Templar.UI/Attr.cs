using Templar.Rendering;

namespace Templar.UI;

public sealed class Attr : UIComponent
{
    public required string Name { get; init; }

    [TemplateIgnore]
    public string? Value { get; init; }

    [TemplateIgnore]
    public bool Boolean { get; init; }

    protected override void Validate()
    {
        Safety.AttributeName(Name);
    }

    [TemplateBind("value")]
    public string? SafeValue
    {
        get
        {
            if (Value is null)
            {
                return null;
            }
            if (Safety.IsUrlAttribute(Name))
            {
                return Safety.SanitizeUrl(Value);
            }
            return Value;
        }
    }

    protected override string Structure
    {
        get
        {
            if (Boolean)
            {
                return " {{ name }}";
            }
            if (Value is null)
            {
                return "";
            }
            return " {{ name }}=\"{{ value }}\"";
        }
    }
}
