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
                $"Event-handler attribute '{name}' is a script context and cannot carry an escaped value. Emit it through a raw attribute (Markup.Raw) only if the value is trusted.");
        }
        return name;
    }

    public static bool IsUrlAttribute(string name)
    {
        return UrlAttributes.Contains(name);
    }

    private static readonly HashSet<string> SafeDataMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/gif",
        "image/webp",
    };

    public static string SanitizeUrl(string name, string url)
    {
        if (string.Equals(name, "srcset", StringComparison.OrdinalIgnoreCase))
        {
            return SanitizeSrcset(url);
        }
        return SanitizeUrl(url);
    }

    public static string SanitizeUrl(string url)
    {
        string? scheme = SchemeOf(url);
        if (scheme is "javascript" or "vbscript")
        {
            return BlockedUrl;
        }
        if (scheme is "data")
        {
            return SanitizeDataUrl(url);
        }
        return url;
    }

    public static string SanitizeSrcset(string srcset)
    {
        string[] candidates = srcset.Split(',');
        string[] sanitized = new string[candidates.Length];
        for (int i = 0; i < candidates.Length; i++)
        {
            sanitized[i] = SanitizeCandidate(candidates[i]);
        }
        return string.Join(",", sanitized);
    }

    private static string SanitizeCandidate(string candidate)
    {
        int start = 0;
        while (start < candidate.Length
            && candidate[start] <= ' ')
        {
            start++;
        }
        int end = candidate.Length;
        while (end > start
            && candidate[end - 1] <= ' ')
        {
            end--;
        }
        string trimmed = candidate.Substring(start, end - start);
        if (trimmed.Length == 0)
        {
            return candidate;
        }
        int urlEnd = 0;
        while (urlEnd < trimmed.Length
            && trimmed[urlEnd] > ' ')
        {
            urlEnd++;
        }
        string url = trimmed.Substring(0, urlEnd);
        string descriptor = trimmed.Substring(urlEnd);
        string leading = candidate.Substring(0, start);
        string trailing = candidate.Substring(end);
        return $"{leading}{SanitizeUrl(url)}{descriptor}{trailing}";
    }

    private static string SanitizeDataUrl(string url)
    {
        int colon = url.IndexOf(':');
        int comma = url.IndexOf(',');
        if (colon < 0
            || comma < 0
            || comma < colon)
        {
            return BlockedUrl;
        }
        string parameters = url.Substring(colon + 1, comma - colon - 1);
        int semicolon = parameters.IndexOf(';');
        string mediaType = semicolon < 0 ? parameters : parameters.Substring(0, semicolon);
        if (SafeDataMediaTypes.Contains(mediaType.Trim()))
        {
            return url;
        }
        return BlockedUrl;
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
