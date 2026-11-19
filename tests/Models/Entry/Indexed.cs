namespace Templar.Tests.Models.Entry;

public sealed class Indexed
{
    public string this[int i] => "INDEXED";

    public string Real { get; init; } = "value";
}
