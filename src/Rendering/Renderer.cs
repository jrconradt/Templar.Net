using System.Collections;

namespace Templar.Rendering;

internal static class Renderer
{
    private sealed class ScanFrame
    {
        public required string Source;
        public int Pos;
        public required IDictionary<string, object?> Values;
        public required FilterRegistry Filters;
        public required RenderOptions Options;
        public string PushedIndent = "";
    }

    private sealed class SequenceFrame
    {
        public required IEnumerator<Compositor> Items;
        public required string Separator;
        public bool First = true;
        public string PushedIndent = "";
    }

    internal static string Render(string source,
                                  IDictionary<string, object?> values,
                                  FilterRegistry filters,
                                  RenderOptions options)
    {
        string output = "";
        int lineStart = 0;
        bool lineHadExpression = false;
        bool atLineStart = true;
        string currentIndent = "";

        var stack = new Stack<object>();
        object current = new ScanFrame
        {
            Source = source,
            Pos = 0,
            Values = values,
            Filters = filters,
            Options = options,
        };

        bool IsLineWsOnly()
        {
            for (int i = lineStart; i < output.Length; i++)
            {
                char ch = output[i];
                if (ch != ' ' && ch != '\t')
                {
                    return false;
                }
            }
            return true;
        }

        void EmitNewline(string nl)
        {
            if (lineHadExpression && IsLineWsOnly())
            {
                output = output[..lineStart];
            }
            else
            {
                output += nl;
            }
            atLineStart = true;
            lineHadExpression = false;
            lineStart = output.Length;
        }

        void EnsureIndent()
        {
            if (atLineStart)
            {
                output += currentIndent;
                atLineStart = false;
            }
        }

        string BuildIndent(int width)
        {
            if (width <= 0)
            {
                return "";
            }
            string unit = options.IndentString;
            if (unit.Length == 0)
            {
                return new string(' ', width);
            }
            string indent = "";
            while (indent.Length + unit.Length <= width)
            {
                indent += unit;
            }
            if (indent.Length < width)
            {
                indent += new string(' ', width - indent.Length);
            }
            return indent;
        }

        void WriteLiteralChar(char ch, string nl)
        {
            if (ch == '\n')
            {
                EmitNewline(nl);
                return;
            }
            if (ch == '\r')
            {
                return;
            }
            EnsureIndent();
            output += ch;
        }

        void WriteValueString(string val, string nl)
        {
            lineHadExpression = true;
            if (val.Length == 0)
            {
                return;
            }
            if (val.Contains('\r'))
            {
                val = val.Replace("\r\n", "\n").Replace('\r', '\n');
            }

            if (!val.Contains('\n'))
            {
                EnsureIndent();
                output += val;
                return;
            }

            EnsureIndent();
            int col = output.Length - lineStart;
            string extra = col > currentIndent.Length
                ? BuildIndent(col - currentIndent.Length)
                : "";
            currentIndent += extra;

            int start = 0;
            while (true)
            {
                int next = val.IndexOf('\n', start);
                int lineEnd = next < 0 ? val.Length : next;
                if (start > 0 && lineEnd > start)
                {
                    EnsureIndent();
                }
                if (lineEnd > start)
                {
                    output += val[start..lineEnd];
                }
                if (next < 0)
                {
                    break;
                }
                EmitNewline(nl);
                lineHadExpression = true;
                start = next + 1;
            }

            if (extra.Length > 0)
            {
                currentIndent = currentIndent[..^extra.Length];
            }
        }

        void WriteVerbatim(string val, string nl)
        {
            lineHadExpression = true;
            if (val.Length == 0)
            {
                return;
            }
            if (val.Contains('\r'))
            {
                val = val.Replace("\r\n", "\n").Replace('\r', '\n');
            }

            EnsureIndent();
            int start = 0;
            while (true)
            {
                int next = val.IndexOf('\n', start);
                int lineEnd = next < 0 ? val.Length : next;
                if (lineEnd > start)
                {
                    output += val[start..lineEnd];
                }
                if (next < 0)
                {
                    break;
                }
                output += nl;
                atLineStart = false;
                lineStart = output.Length;
                start = next + 1;
            }
        }

        int SkipPastEnd(string src, int from)
        {
            int depth = 0;
            int p = from;
            while (p < src.Length)
            {
                if (src[p] == '\\' && p + 1 < src.Length)
                {
                    char n = src[p + 1];
                    if (n == '\\')
                    {
                        p += 2;
                        continue;
                    }
                    if (n == '{' && p + 2 < src.Length
                        && src[p + 2] == '{')
                    {
                        p += 3;
                        continue;
                    }
                    if (n == '}' && p + 2 < src.Length
                        && src[p + 2] == '}')
                    {
                        p += 3;
                        continue;
                    }
                }
                if (p + 1 < src.Length && src[p] == '{'
                    && src[p + 1] == '{')
                {
                    if (p + 2 < src.Length && src[p + 2] == '?')
                    {
                        int close = src.IndexOf("}}",
                                                p + 3,
                                                StringComparison.Ordinal);
                        string body = src[(p + 3)..close].Trim();
                        if (body.Length == 0)
                        {
                            if (depth == 0)
                            {
                                return close + 2;
                            }
                            depth--;
                        }
                        else if (body != "else")
                        {
                            depth++;
                        }
                        p = close + 2;
                        continue;
                    }
                    else
                    {
                        int close = src.IndexOf("}}",
                                                p + 2,
                                                StringComparison.Ordinal);
                        p = close + 2;
                        continue;
                    }
                }
                p++;
            }
            return src.Length;
        }

        (int Pos, bool LandedOnElse) SkipPastEndOrElse(string src, int from)
        {
            int depth = 0;
            int p = from;
            while (p < src.Length)
            {
                if (src[p] == '\\' && p + 1 < src.Length)
                {
                    char n = src[p + 1];
                    if (n == '\\')
                    {
                        p += 2;
                        continue;
                    }
                    if (n == '{' && p + 2 < src.Length
                        && src[p + 2] == '{')
                    {
                        p += 3;
                        continue;
                    }
                    if (n == '}' && p + 2 < src.Length
                        && src[p + 2] == '}')
                    {
                        p += 3;
                        continue;
                    }
                }
                if (p + 1 < src.Length && src[p] == '{'
                    && src[p + 1] == '{')
                {
                    if (p + 2 < src.Length && src[p + 2] == '?')
                    {
                        int close = src.IndexOf("}}",
                                                p + 3,
                                                StringComparison.Ordinal);
                        string body = src[(p + 3)..close].Trim();
                        if (body.Length == 0)
                        {
                            if (depth == 0)
                            {
                                return (close + 2, false);
                            }
                            depth--;
                        }
                        else if (body == "else")
                        {
                            if (depth == 0)
                            {
                                return (close + 2, true);
                            }
                        }
                        else
                        {
                            depth++;
                        }
                        p = close + 2;
                        continue;
                    }
                    else
                    {
                        int close = src.IndexOf("}}",
                                                p + 2,
                                                StringComparison.Ordinal);
                        p = close + 2;
                        continue;
                    }
                }
                p++;
            }
            return (src.Length, false);
        }

        static bool IsTruthy(object? v) => v switch
        {
            null => false,
            bool b => b,
            string s => s.Length > 0,
            IEnumerable e => HasAny(e),
            _ => true,
        };

        static bool HasAny(IEnumerable e)
        {
            var en = e.GetEnumerator();
            try
            {
                return en.MoveNext();
            }
            finally
            {
                if (en is IDisposable d)
                {
                    d.Dispose();
                }
            }
        }

        while (true)
        {
            if (current is ScanFrame scan)
            {
                if (scan.Pos >= scan.Source.Length)
                {
                    if (scan.PushedIndent.Length > 0)
                    {
                        currentIndent = currentIndent[..^scan.PushedIndent.Length];
                    }
                    if (stack.Count == 0)
                    {
                        return output;
                    }
                    current = stack.Pop();
                    continue;
                }

                string s = scan.Source;
                int p = scan.Pos;
                char c = s[p];
                string nl = scan.Options.Newline;

                if (c == '\\' && p + 1 < s.Length)
                {
                    char n = s[p + 1];
                    if (n == '\\')
                    {
                        WriteLiteralChar('\\', nl);
                        scan.Pos += 2;
                        continue;
                    }
                    if (n == '{' && p + 2 < s.Length
                        && s[p + 2] == '{')
                    {
                        WriteLiteralChar('{', nl);
                        WriteLiteralChar('{', nl);
                        scan.Pos += 3;
                        continue;
                    }
                    if (n == '}' && p + 2 < s.Length
                        && s[p + 2] == '}')
                    {
                        WriteLiteralChar('}', nl);
                        WriteLiteralChar('}', nl);
                        scan.Pos += 3;
                        continue;
                    }
                }

                if (p + 1 < s.Length && c == '{'
                    && s[p + 1] == '{')
                {
                    char marker = (p + 2 < s.Length) ? s[p + 2] : '\0';
                    bool markerConsumed = marker == '#' || marker == '?'
                        || marker == '&'
                        || marker == '>';
                    int contentStart = markerConsumed ? p + 3 : p + 2;
                    int closePos = s.IndexOf("}}",
                                             contentStart,
                                             StringComparison.Ordinal);
                    string body = s[contentStart..closePos].Trim();
                    scan.Pos = closePos + 2;

                    if (marker == '#')
                    {
                        continue;
                    }

                    if (marker == '?')
                    {
                        if (body.Length == 0)
                        {
                            continue;
                        }
                        if (body == "else")
                        {
                            scan.Pos = SkipPastEnd(s, scan.Pos);
                            continue;
                        }
                        bool negated = body.StartsWith('!');
                        string name = (negated ? body[1..] : body).Trim();
                        scan.Values.TryGetValue(name, out var condV);
                        bool truthy = IsTruthy(condV);
                        if (negated)
                        {
                            truthy = !truthy;
                        }
                        if (!truthy)
                        {
                            var (jumpPos, _) = SkipPastEndOrElse(s, scan.Pos);
                            scan.Pos = jumpPos;
                        }
                        continue;
                    }

                    bool rawTag = marker == '&'
                        || marker == '>';
                    var esc = scan.Options.Escape;
                    string varName;
                    string? filterName = null;
                    int pipeIdx = body.IndexOf('|');
                    if (pipeIdx >= 0)
                    {
                        varName = body[..pipeIdx].Trim();
                        filterName = body[(pipeIdx + 1)..].Trim();
                    }
                    else
                    {
                        varName = body;
                    }

                    bool present = scan.Values.TryGetValue(varName, out var value);
                    if (scan.Options.StrictUndefined && !present)
                    {
                        throw new TemplateRenderException($"Undefined variable '{varName}' (strict mode)",
                                                         filterName: null,
                                                         variableName: varName);
                    }

                    lineHadExpression = true;

                    if (filterName is not null)
                    {
                        var filtered = scan.Filters.Apply(filterName, value, varName);
                        if (!rawTag && esc is not null)
                        {
                            filtered = esc(filtered);
                        }
                        EnsureIndent();
                        output += filtered;
                        continue;
                    }

                    if (value is null)
                    {
                        continue;
                    }
                    if (value is IPreformattedContent preformattedContent)
                    {
                        WriteVerbatim(preformattedContent.Value, nl);
                        continue;
                    }
                    if (value is IIndentedContent indentedContent)
                    {
                        WriteValueString(indentedContent.Value, nl);
                        continue;
                    }
                    if (value is string sval)
                    {
                        WriteValueString((!rawTag && esc is not null) ? esc(sval) : sval, nl);
                        continue;
                    }

                    if (value is Sequence seqVal)
                    {
                        int col1 = output.Length - lineStart;
                        if (atLineStart)
                        {
                            output += currentIndent;
                            atLineStart = false;
                            col1 = output.Length - lineStart;
                        }
                        string extra1 = col1 > currentIndent.Length
                            ? BuildIndent(col1 - currentIndent.Length)
                            : "";
                        currentIndent += extra1;
                        stack.Push(scan);
                        current = new SequenceFrame
                        {
                            Items = seqVal.Items.GetEnumerator(),
                            Separator = seqVal.SeparatorInternal,
                            PushedIndent = extra1,
                        };
                        continue;
                    }

                    if (value is Compositor co)
                    {
                        int col2 = output.Length - lineStart;
                        if (atLineStart)
                        {
                            output += currentIndent;
                            atLineStart = false;
                            col2 = output.Length - lineStart;
                        }
                        string extra2 = col2 > currentIndent.Length
                            ? BuildIndent(col2 - currentIndent.Length)
                            : "";
                        currentIndent += extra2;
                        co.Compile(out var cs,
                                   out var cv,
                                   out var cf,
                                   out var copts);
                        stack.Push(scan);
                        current = new ScanFrame
                        {
                            Source = cs,
                            Pos = 0,
                            Values = cv,
                            Filters = cf,
                            Options = copts,
                            PushedIndent = extra2,
                        };
                        continue;
                    }

                    if (value is IEnumerable<Compositor> comps)
                    {
                        int col3 = output.Length - lineStart;
                        if (atLineStart)
                        {
                            output += currentIndent;
                            atLineStart = false;
                            col3 = output.Length - lineStart;
                        }
                        string extra3 = col3 > currentIndent.Length
                            ? BuildIndent(col3 - currentIndent.Length)
                            : "";
                        currentIndent += extra3;
                        stack.Push(scan);
                        current = new SequenceFrame
                        {
                            Items = comps.GetEnumerator(),
                            Separator = nl,
                            PushedIndent = extra3,
                        };
                        continue;
                    }

                    if (value is IEnumerable<string> strs)
                    {
                        string joined = string.Join(nl, strs);
                        WriteValueString((!rawTag && esc is not null) ? esc(joined) : joined, nl);
                        continue;
                    }

                    string fallback = value.ToString() ?? "";
                    WriteValueString((!rawTag && esc is not null) ? esc(fallback) : fallback, nl);
                    continue;
                }

                WriteLiteralChar(c, nl);
                scan.Pos++;
                continue;
            }

            if (current is SequenceFrame seq)
            {
                if (!seq.Items.MoveNext())
                {
                    seq.Items.Dispose();
                    if (seq.PushedIndent.Length > 0)
                    {
                        currentIndent = currentIndent[..^seq.PushedIndent.Length];
                    }
                    current = stack.Pop();
                    continue;
                }
                if (!seq.First)
                {
                    output += seq.Separator;
                    if (seq.Separator.EndsWith('\n'))
                    {
                        atLineStart = true;
                        lineStart = output.Length;
                        lineHadExpression = false;
                    }
                }
                seq.First = false;
                var child = seq.Items.Current;
                child.Compile(out var cs2,
                              out var cv2,
                              out var cf2,
                              out var co2);
                stack.Push(seq);
                current = new ScanFrame
                {
                    Source = cs2,
                    Pos = 0,
                    Values = cv2,
                    Filters = cf2,
                    Options = co2,
                };
                continue;
            }
        }
    }
}
