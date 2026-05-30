using Templar.Rendering;

namespace Templar.UI;

public static partial class Markup
{
    public static Fragment Inline(params object?[] parts)
    {
        var items = new List<Compositor>();
        foreach (var part in parts)
        {
            if (part is Compositor fragment)
            {
                items.Add(fragment);
            }
            else if (part is not null)
            {
                items.Add(new Text { Value = part });
            }
        }
        return new Fragment { Items = items };
    }

    public static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        bool needs = false;
        foreach (char c in value)
        {
            if (c == '&' || c == '<'
                || c == '>'
                || c == '"'
                || c == '\'')
            {
                needs = true;
                break;
            }
        }

        if (!needs)
        {
            return value;
        }

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    public static RawHtml Raw(string value)
    {
        return new RawHtml(value);
    }

    public static RenderOptions Options(string newline = "\n", string indent = "    ")
    {
        return new RenderOptions
        {
            Newline = newline,
            IndentString = indent,
            Escape = Escape,
        };
    }
}
