namespace Templar.Rendering;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class TemplateTextAttribute(string name, string text) : Attribute
{
    public string Name { get; } = name;
    public string Text { get; } = text;
}
