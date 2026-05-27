namespace Templar.Rendering;

public sealed class TemplateParseException(string message, int line, int column)
    : Exception(message)
{
    public int Line { get; } = line;
    public int Column { get; } = column;
}
