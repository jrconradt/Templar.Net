namespace Templar.Rendering;

internal static class Renderer
{
    internal sealed class ScanFrame
    {
        public required string Source;
        public int Pos;
        public required IDictionary<string, object?> Values;
        public required RenderOptions Options;
    }

    internal sealed class SequenceFrame
    {
        public required IEnumerator<IEnumerator<object?>> Items;
        public required string Separator;
        public bool First = true;
    }

    internal sealed class CompiledFrame
    {
        public required IEnumerator<object?> Steps;
    }

    private sealed record IndentPop(string Extra);

    internal static string Render(string source,
                                  IDictionary<string, object?> values,
                                  RenderOptions options)
    {
        var writer = new TemplarWriter(options);
        writer.Frames.Push(new ScanFrame
        {
            Source = source,
            Pos = 0,
            Values = values,
            Options = options,
        });
        Drive(writer);
        return writer.Result;
    }

    internal static void Drive(TemplarWriter writer)
    {
        if (writer.Frames.Count == 0)
        {
            return;
        }

        object current = writer.Frames.Pop();
        while (true)
        {
            if (current is IndentPop pop)
            {
                writer.PopIndent(pop.Extra);
                if (writer.Frames.Count == 0)
                {
                    return;
                }
                current = writer.Frames.Pop();
                continue;
            }

            if (current is ScanFrame scan)
            {
                if (scan.Pos >= scan.Source.Length)
                {
                    if (writer.Frames.Count == 0)
                    {
                        return;
                    }
                    current = writer.Frames.Pop();
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
                        writer.WriteLiteralChar('\\', nl);
                        scan.Pos += 2;
                        continue;
                    }
                    if (n == '{' && p + 2 < s.Length
                        && s[p + 2] == '{')
                    {
                        writer.WriteLiteralChar('{', nl);
                        writer.WriteLiteralChar('{', nl);
                        scan.Pos += 3;
                        continue;
                    }
                    if (n == '}' && p + 2 < s.Length
                        && s[p + 2] == '}')
                    {
                        writer.WriteLiteralChar('}', nl);
                        writer.WriteLiteralChar('}', nl);
                        scan.Pos += 3;
                        continue;
                    }
                }

                if (p + 1 < s.Length && c == '{'
                    && s[p + 1] == '{')
                {
                    char marker = (p + 2 < s.Length) ? s[p + 2] : '\0';
                    bool markerConsumed = marker == '#';
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

                    scan.Values.TryGetValue(body, out var value);

                    writer.MarkExpression();

                    if (value is null)
                    {
                        continue;
                    }
                    if (value is string sval)
                    {
                        writer.WriteValueString(sval, nl);
                        continue;
                    }
                    if (value is IEnumerable<string> strs)
                    {
                        string joined = string.Join(nl, strs);
                        writer.WriteValueString(joined, nl);
                        continue;
                    }

                    string fallback = value.ToString() ?? "";
                    writer.WriteValueString(fallback, nl);
                    continue;
                }

                writer.WriteLiteralChar(c, nl);
                scan.Pos++;
                continue;
            }

            if (current is SequenceFrame seq)
            {
                if (!seq.Items.MoveNext())
                {
                    seq.Items.Dispose();
                    if (writer.Frames.Count == 0)
                    {
                        return;
                    }
                    current = writer.Frames.Pop();
                    continue;
                }
                if (!seq.First)
                {
                    writer.WriteSeparator(seq.Separator);
                }
                seq.First = false;
                writer.Frames.Push(seq);
                current = new CompiledFrame
                {
                    Steps = seq.Items.Current,
                };
                continue;
            }

            if (current is CompiledFrame compiled)
            {
                if (compiled.Steps.MoveNext())
                {
                    var step = compiled.Steps.Current;
                    if (step is null)
                    {
                        continue;
                    }
                    string extra = writer.PushColumnIndent();
                    writer.Frames.Push(compiled);
                    writer.Frames.Push(new IndentPop(extra));
                    current = step;
                    continue;
                }
                compiled.Steps.Dispose();
                if (writer.Frames.Count == 0)
                {
                    return;
                }
                current = writer.Frames.Pop();
                continue;
            }

            if (writer.Frames.Count == 0)
            {
                return;
            }
            current = writer.Frames.Pop();
        }
    }
}
