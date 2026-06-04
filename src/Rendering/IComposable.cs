namespace Templar.Rendering;

public interface IComposable
{
    void RenderInto(TemplarWriter writer);

    string Render()
    {
        var writer = new TemplarWriter(new RenderOptions());
        RenderInto(writer);
        Renderer.Drive(writer);
        return writer.Result;
    }
}
