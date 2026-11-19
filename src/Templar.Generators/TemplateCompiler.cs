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
    }

    public readonly struct Node
    {
        public Op Op { get; }
        public string Text { get; }
        public int Line { get; }

        public Node(Op op, string text, int line)
        {
            Op = op;
            Text = text;
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
                nodes.Add(new Node(Op.Lit, lit.ToString(), 0));
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
                nodes.Add(new Node(Op.Val, body, openLine));
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
