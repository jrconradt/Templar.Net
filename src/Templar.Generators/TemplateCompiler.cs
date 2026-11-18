namespace Templar.Generators;

internal static class TemplateCompiler
{
    private enum Op
    {
        Lit,
        Val,
    }

    private readonly struct Node
    {
        public Op Op { get; }
        public string Text { get; }

        public Node(Op op, string text)
        {
            Op = op;
            Text = text;
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

    public static string EmitClass(string ns, string className, string templateText)
    {
        var nodes = Parse(templateText);
        var properties = CollectProperties(nodes);

        var propertyLines = string.Join("\n",
            properties.Select(p => $"    public required object? {p} {{ get; init; }}"));
        var propertyBlock = properties.Count > 0 ? $"\n{propertyLines}\n" : "\n";

        var steps = EmitSteps(nodes);

        return "#nullable enable\n\n"
            + $"namespace {ns};\n\n"
            + "[global::System.CodeDom.Compiler.GeneratedCode(\"Templar.Generators\", \"1.0.0\")]\n"
            + $"public sealed class {className} : global::Templar.Rendering.Compositor\n"
            + "{\n"
            + propertyBlock
            + "\n"
            + "    public override void RenderInto(global::Templar.Rendering.TemplarWriter w)\n"
            + "    {\n"
            + "        w.Compiled(Steps(w));\n"
            + "    }\n\n"
            + "    private global::System.Collections.Generic.IEnumerator<global::Templar.Rendering.IComposable> Steps(global::Templar.Rendering.TemplarWriter w)\n"
            + "    {\n"
            + steps
            + "        yield break;\n"
            + "    }\n"
            + "}\n";
    }

    private static List<Node> Parse(string text)
    {
        var nodes = new List<Node>();
        var lit = new List<char>();

        void FlushLit()
        {
            if (lit.Count > 0)
            {
                nodes.Add(new Node(Op.Lit, new string(lit.ToArray())));
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
                    lit.Add('\\');
                    p += 2;
                    continue;
                }
                if (n == '{' && p + 2 < text.Length
                    && text[p + 2] == '{')
                {
                    lit.Add('{');
                    lit.Add('{');
                    p += 3;
                    continue;
                }
                if (n == '}' && p + 2 < text.Length
                    && text[p + 2] == '}')
                {
                    lit.Add('}');
                    lit.Add('}');
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
                    lit.Add(c);
                    p++;
                    continue;
                }

                string body = text.Substring(contentStart, close - contentStart).Trim();
                p = close + 2;

                if (marker == '#')
                {
                    continue;
                }

                FlushLit();
                nodes.Add(new Node(Op.Val, body));
                continue;
            }

            lit.Add(c);
            p++;
        }

        FlushLit();
        return nodes;
    }

    private static List<string> CollectProperties(List<Node> nodes)
    {
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (node.Op != Op.Val)
            {
                continue;
            }
            if (!IsIdentifier(node.Text))
            {
                continue;
            }
            string prop = Identifier.UpperFirst(node.Text);
            if (seen.Add(prop))
            {
                ordered.Add(prop);
            }
        }
        return ordered;
    }

    private static string EmitSteps(List<Node> nodes)
    {
        var lines = new List<string>();
        int depth = 2;

        string Pad(int d)
        {
            return new string(' ', d * 4);
        }

        foreach (var node in nodes)
        {
            switch (node.Op)
            {
                case Op.Lit:
                {
                    lines.Add($"{Pad(depth)}w.Literal(\"{EscapeLiteral(node.Text)}\");");
                    break;
                }
                case Op.Val:
                {
                    if (!IsIdentifier(node.Text))
                    {
                        break;
                    }
                    string prop = Identifier.UpperFirst(node.Text);
                    lines.Add($"{Pad(depth)}{{");
                    lines.Add($"{Pad(depth + 1)}var __c = w.Value({prop});");
                    lines.Add($"{Pad(depth + 1)}if (__c is not null)");
                    lines.Add($"{Pad(depth + 1)}{{");
                    lines.Add($"{Pad(depth + 2)}yield return __c;");
                    lines.Add($"{Pad(depth + 1)}}}");
                    lines.Add($"{Pad(depth)}}}");
                    break;
                }
            }
        }

        return lines.Count > 0 ? string.Join("\n", lines) + "\n" : "";
    }

    private static bool IsIdentifier(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return false;
        }
        if (!char.IsLetter(s[0]) && s[0] != '_')
        {
            return false;
        }
        for (int i = 1; i < s.Length; i++)
        {
            if (!char.IsLetterOrDigit(s[i]) && s[i] != '_')
            {
                return false;
            }
        }
        return true;
    }

    private static string EscapeLiteral(string s)
    {
        return string.Concat(s.Select(EscapeChar));
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
            _ => $"{c}",
        };
    }
}
