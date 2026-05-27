using System.Collections.Generic;
using System.Linq;

namespace Templar.Generators;

internal static class Identifier
{
    public static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "_";
        var safe = new string(s.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        return char.IsDigit(safe[0]) ? "_" + safe : safe;
    }

    public static string Pascal(string s)
    {
        var safe = Sanitize(s);
        return safe.Length > 0 && char.IsLower(safe[0])
            ? char.ToUpperInvariant(safe[0]) + safe.Substring(1)
            : safe;
    }
}
