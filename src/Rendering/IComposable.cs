namespace Templar.Rendering;

public interface IComposable
{
    void RenderInto(TemplarWriter writer);
}
