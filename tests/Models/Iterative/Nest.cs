namespace Templar.Tests.Models.Iterative;

public sealed class Nest
{
    public Nest? Inner { get; init; }

    public string Leaf { get; init; } = "";
}
