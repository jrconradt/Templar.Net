namespace Templar.Rendering;

public static class Compositor
{
    public static string ResolveTemplate<T>(T model, string template, RenderOptions? options = null) where T : class
    {
        return TemplarWriter.Render(model, RenderRegistry.Find<T>(template), options);
    }

    public static string ResolveTemplate<T>(T model, RenderOptions? options = null) where T : class
    {
        return TemplarWriter.Render(model, RenderRegistry.Default<T>(), options);
    }
}
