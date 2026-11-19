namespace Templar.Tests.Models.Engine;

public sealed class WriteOnlyProp
{
    private string _w = "";

    public string Sink
    {
        set => _w = value;
    }

    public string Used => _w.Length >= 0 ? "v" : "";
}
