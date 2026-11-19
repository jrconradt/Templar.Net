using Templar.Rendering;

namespace Templar.Tests.Models.Compiled;

[Template("Models/Compiled/Greeting")]
public sealed record Greeting(string Who, string Place);
