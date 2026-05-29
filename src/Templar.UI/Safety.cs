namespace Templar.UI;

public static class Safety
{
    public const string BlockedUrl = "about:invalid#blocked";

    private static readonly HashSet<string> UrlAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "href",
        "src",
        "srcset",
        "action",
        "formaction",
        "cite",
        "poster",
        "data",
        "background",
        "ping",
        "longdesc",
        "manifest",
        "xlink:href",
    };

    public static string TagName(string tag)
    {
        if (!IsTagName(tag))
        {
            throw new MarkupSecurityException(
                $"Unsafe tag name '{tag}'. Tag names must start with a letter and contain only letters, digits, or '-'.");
        }
        return tag;
    }

    public static string AttributeName(string name)
    {
        if (!IsAttributeName(name))
        {
            throw new MarkupSecurityException(
                $"Unsafe attribute name '{name}'. It contains characters that could break out of the attribute list.");
        }
        if (name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
        {
            throw new MarkupSecurityException(
                $"Event-handler attribute '{name}' is a script context and cannot carry an escaped value. Emit it through a raw attribute (Html.Raw) only if the value is trusted.");
        }
        return name;
    }

    public static bool IsUrlAttribute(string name)
    {
        return UrlAttributes.Contains(name);
    }

    public static string SanitizeUrl(string url)
    {
        string? scheme = SchemeOf(url);
        if (scheme is "javascript" or "vbscript" or "data")
        {
            return BlockedUrl;
        }
        return url;
    }

    private static string? SchemeOf(string url)
    {
        int colon = -1;
        for (int i = 0; i < url.Length; i++)
        {
            char c = url[i];
            if (c == ':')
            {
                colon = i;
                break;
            }
            if (c == '/' || c == '?'
                || c == '#')
            {
                return null;
            }
        }
        if (colon < 0)
        {
            return null;
        }

        string scheme = "";
        for (int i = 0; i < colon; i++)
        {
            char c = url[i];
            if (c > ' ')
            {
                scheme += char.ToLowerInvariant(c);
            }
        }
        return scheme;
    }

    private static bool IsTagName(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return false;
        }
        if (!IsLetter(s[0]))
        {
            return false;
        }
        for (int i = 1; i < s.Length; i++)
        {
            char c = s[i];
            if (!IsLetter(c) && !IsDigit(c)
                && c != '-')
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsAttributeName(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return false;
        }
        char first = s[0];
        if (!IsLetter(first) && first != '_'
            && first != ':')
        {
            return false;
        }
        for (int i = 1; i < s.Length; i++)
        {
            char c = s[i];
            if (!IsLetter(c) && !IsDigit(c)
                && c != '-'
                && c != '_'
                && c != ':'
                && c != '.')
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsLetter(char c)
    {
        return (c >= 'a' && c <= 'z')
            || (c >= 'A' && c <= 'Z');
    }

    private static bool IsDigit(char c)
    {
        return c >= '0' && c <= '9';
    }
}
