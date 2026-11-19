using Templar.Rendering;

namespace Templar.Tests.Models.Swallow;

[Template("Models/Swallow/ListHost")]
public sealed record ListHost(Sequence<Empty> Inner);
