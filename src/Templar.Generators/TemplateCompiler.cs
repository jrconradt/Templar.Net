using System;
using System.Collections.Generic;
using System.Text;

namespace Templar.Generators;

internal static class TemplateCompiler
{
    public enum Op
    {
        Lit,
        Val,
        Self,
    }

    public readonly struct Node
    {
        public Op Op { get; }
        public string Text { get; }
        public string? Template { get; }
        public int Line { get; }

        public Node(Op op, string text, string? template, int line)
        {
            Op = op;
            Text = text;
            Template = template;
            Line = line;
        }
    }

    public readonly struct ValidationError
    {
        public int Line { get; }
        public string Message { get; }

        public ValidationError(int line, string message)
        {
            Line = line;
            Message = message;
        }
    }

    public static ValidationError? Validate(string text)
    {
        int p = 0;
        int line = 1;
        while (p < text.Length)
        {
            if (text[p] == '\\' && p + 1 < text.Length)
            {
                char n = text[p + 1];
                if (n == '\\')
                {
                    p += 2;
                    continue;
                }
                if (n == '{' && p + 2 < text.Length
                    && text[p + 2] == '{')
                {
                    p += 3;
                    continue;
                }
                if (n == '}' && p + 2 < text.Length
                    && text[p + 2] == '}')
                {
                    p += 3;
                    continue;
                }
            }

            if (p + 1 < text.Length && text[p] == '{'
                && text[p + 1] == '{')
            {
                int openLine = line;
                char marker = (p + 2 < text.Length) ? text[p + 2] : '\0';
                if (marker == '?' || marker == '&'
                    || marker == '>')
                {
                    return new ValidationError(openLine, $"Unsupported tag marker '{marker}' at line {openLine}");
                }
                bool markerConsumed = marker == '#';
                int contentStart = markerConsumed ? p + 3 : p + 2;
                int close = text.IndexOf("}}", contentStart, StringComparison.Ordinal);
                if (close < 0)
                {
                    return new ValidationError(openLine, $"Unclosed tag starting at line {openLine}");
                }
                if (!markerConsumed && text.IndexOf('|', contentStart, close - contentStart) >= 0)
                {
                    return new ValidationError(openLine, $"Unsupported '|' in tag at line {openLine}");
                }
                if (!markerConsumed && !IsSpot(text.Substring(contentStart, close - contentStart)))
                {
                    var spot = text.Substring(contentStart, close - contentStart).Trim();
                    return new ValidationError(openLine, $"Malformed spot '{spot}' at line {openLine}: a spot is a property name, optionally followed by ': TemplateName', or ': TemplateName' alone");
                }

                int afterClose = close + 2;
                for (int i = p; i < afterClose; i++)
                {
                    if (text[i] == '\n')
                    {
                        line++;
                    }
                }
                p = afterClose;
                continue;
            }

            if (text[p] == '\n')
            {
                line++;
            }
            p++;
        }

        return null;
    }

    public static List<Node> Parse(string text)
    {
        var nodes = new List<Node>();
        var lit = new StringBuilder();
        int line = 1;

        void FlushLit()
        {
            if (lit.Length > 0)
            {
                nodes.Add(new Node(Op.Lit, lit.ToString(), null, 0));
                lit.Clear();
            }
        }

        int p = 0;
        while (p < text.Length)
        {
            char c = text[p];

            if (c == '\\' && p + 1 < text.Length)
            {
                char n = text[p + 1];
                if (n == '\\')
                {
                    lit.Append('\\');
                    p += 2;
                    continue;
                }
                if (n == '{' && p + 2 < text.Length
                    && text[p + 2] == '{')
                {
                    lit.Append("{{");
                    p += 3;
                    continue;
                }
                if (n == '}' && p + 2 < text.Length
                    && text[p + 2] == '}')
                {
                    lit.Append("}}");
                    p += 3;
                    continue;
                }
            }

            if (c == '{' && p + 1 < text.Length
                && text[p + 1] == '{')
            {
                char marker = p + 2 < text.Length ? text[p + 2] : '\0';
                bool consumed = marker == '#';
                int contentStart = consumed ? p + 3 : p + 2;
                int close = text.IndexOf("}}", contentStart, StringComparison.Ordinal);
                if (close < 0)
                {
                    lit.Append(c);
                    p++;
                    continue;
                }

                int openLine = line;
                for (int i = p; i < close + 2; i++)
                {
                    if (text[i] == '\n')
                    {
                        line++;
                    }
                }

                string body = text.Substring(contentStart, close - contentStart).Trim();
                p = close + 2;

                if (marker == '#')
                {
                    continue;
                }

                FlushLit();
                int colon = body.IndexOf(':');
                if (colon < 0)
                {
                    nodes.Add(new Node(Op.Val, body, null, openLine));
                    continue;
                }
                string property = body.Substring(0, colon).Trim();
                nodes.Add(new Node(property.Length == 0 ? Op.Self : Op.Val, property, body.Substring(colon + 1).Trim(), openLine));
                continue;
            }

            if (c == '\n')
            {
                line++;
            }
            lit.Append(c);
            p++;
        }

        FlushLit();
        return nodes;
    }

    private static bool IsSpot(string content)
    {
        int colon = content.IndexOf(':');
        if (colon < 0)
        {
            return IsPropertyName(content.Trim());
        }
        string property = content.Substring(0, colon).Trim();
        return (property.Length == 0 || IsPropertyName(property))
            && IsTemplateName(content.Substring(colon + 1).Trim());
    }

    private static bool IsPropertyName(string name)
    {
        if (name.Length == 0 || !(char.IsLetter(name[0]) || name[0] == '_'))
        {
            return false;
        }
        foreach (char c in name)
        {
            if (!(char.IsLetterOrDigit(c) || c == '_'))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsTemplateName(string name)
    {
        if (name.Length == 0 || name[0] == '/' || name[name.Length - 1] == '/')
        {
            return false;
        }
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsWhiteSpace(c) || c == ':' || c == '\\')
            {
                return false;
            }
            if (c == '/' && name[i - 1] == '/')
            {
                return false;
            }
        }
        return true;
    }

    public static string EscapeLiteral(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            sb.Append(EscapeChar(c));
        }
        return sb.ToString();
    }

    private static string EscapeChar(char c)
    {
        return c switch
        {
            '\\' => "\\\\",
            '"' => "\\\"",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            _ when c < ' ' || c == (char)0x85 || c == (char)0x2028 || c == (char)0x2029 => $"\\u{(int)c:X4}",
            _ => $"{c}",
        };
    }
}
