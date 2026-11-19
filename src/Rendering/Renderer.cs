namespace Templar.Rendering;

internal static class Renderer
{
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
