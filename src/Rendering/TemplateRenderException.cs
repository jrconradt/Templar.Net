namespace Templar.Rendering;

public sealed class TemplateRenderException : Exception
{
    public string? TemplateName { get; }
    public string? FilterName { get; }
    public string? VariableName { get; }

    public TemplateRenderException(string message, string? templateName = null) : base(message)
    {
        TemplateName = templateName;
    }

    public TemplateRenderException(
        string message,
        string? filterName,
        string? variableName,
        string? templateName = null) : base(message)
    {
        FilterName = filterName;
        VariableName = variableName;
        TemplateName = templateName;
    }
}
