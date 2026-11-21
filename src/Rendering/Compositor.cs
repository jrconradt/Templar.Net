namespace Templar.Rendering;

public static class Compositor
{
    private const string NotResolved = "Compositor.ResolveTemplate was not resolved at compile time: the Templar generator does not run on the calling project.";

    public static string ResolveTemplate<T>(T model, string template, RenderOptions? options = null) where T : class
    {
        throw new InvalidOperationException(NotResolved);
    }

    public static string ResolveTemplate<T>(T model, RenderOptions? options = null) where T : class
    {
        throw new InvalidOperationException(NotResolved);
    }
}
