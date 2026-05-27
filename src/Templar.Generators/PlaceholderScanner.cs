using System.Collections.Generic;

namespace Templar.Generators;

internal static class PlaceholderScanner
{
    public static IReadOnlyList<string> Scan(string templateText)
    {
        var found = new List<string>();
        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var pos = 0;
        while (pos < templateText.Length - 1)
        {
            char c = templateText[pos];

            if (c == '\\' && pos + 1 < templateText.Length)
            {
                char n = templateText[pos + 1];
                if (n == '\\')
                {
                    pos += 2;
                    continue;
                }
                if (n == '{' && pos + 2 < templateText.Length
                    && templateText[pos + 2] == '{')
                {
                    pos += 3;
                    continue;
                }
                if (n == '}' && pos + 2 < templateText.Length
                    && templateText[pos + 2] == '}')
                {
                    pos += 3;
                    continue;
                }
            }

            if (c != '{' || templateText[pos + 1] != '{')
            {
                pos++;
                continue;
            }

            var contentStart = pos + 2;

            if (contentStart < templateText.Length && templateText[contentStart] == '#')
            {
                var commentClose = templateText.IndexOf("}}",
                                                        contentStart,
                                                        System.StringComparison.Ordinal);
                pos = commentClose < 0 ? templateText.Length : commentClose + 2;
                continue;
            }

            if (contentStart < templateText.Length && templateText[contentStart] == '?')
            {
                var controlClose = templateText.IndexOf("}}",
                                                        contentStart,
                                                        System.StringComparison.Ordinal);
                pos = controlClose < 0 ? templateText.Length : controlClose + 2;
                continue;
            }

            var close = templateText.IndexOf("}}",
                                             contentStart,
                                             System.StringComparison.Ordinal);
            if (close < 0)
            {
                break;
            }

            var inner = templateText.Substring(contentStart, close - contentStart).Trim();
            var pipe = inner.IndexOf('|');
            var name = (pipe >= 0 ? inner.Substring(0, pipe) : inner).Trim();
            if (IsValidIdentifier(name) && seen.Add(name))
            {
                found.Add(name);
            }
            pos = close + 2;
        }
        return found;
    }

    private static bool IsValidIdentifier(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return false;
        }
        if (!char.IsLetter(s[0]) && s[0] != '_')
        {
            return false;
        }
        for (var i = 1; i < s.Length; i++)
        {
            if (!char.IsLetterOrDigit(s[i]) && s[i] != '_')
            {
                return false;
            }
        }
        return true;
    }
}
