using Templar.Rendering;

namespace Templar.Tests.DefaultNaming.Orders;

public sealed class Requisite : Compositor
{
    public required string Name { get; init; }
}
