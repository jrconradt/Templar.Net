using System.Linq;
using Microsoft.CodeAnalysis.CSharp;

namespace Templar.Generators;

internal static class Identifier
{
    public static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return "_";
        }
        var safe = new string(s.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        return char.IsDigit(safe[0]) ? "_" + safe : safe;
    }

    public static string Escape(string name)
    {
        return SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None
            ? name
            : "@" + name;
    }
}
