using Templar.Rendering;

namespace Templar.Tests.Models.Engine;

[Template("Models/Engine/WriteOnlyProp")]
public sealed record WriteOnlyProp
{
    private string _w = "";

    public string Sink
    {
        set => _w = value;
    }

    public string Used => _w.Length >= 0 ? "v" : "";
}
