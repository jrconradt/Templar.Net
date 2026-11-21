using Microsoft.CodeAnalysis;

namespace Templar.Generators;

internal static class Descriptors
{
    public static readonly DiagnosticDescriptor MalformedTemplate = new(
        id: "TMPLR002",
        title: "Malformed template",
        messageFormat: "{0}",
        category: "Templar",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The .tpl file has an unclosed tag, an unsupported tag marker, or a spot that is not a property name.");

    public static readonly DiagnosticDescriptor OutsideTemplates = new(
        id: "TMPLR003",
        title: "Template outside the Templates folder",
        messageFormat: "Template '{0}' is outside the Templates folder '{1}', so it has no name",
        category: "Templar",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Templates live under <ProjectDir>/Templates/: a template's name is its path after Templates/ without .tpl. A .tpl outside Templates/ has no name.");

    public static readonly DiagnosticDescriptor NoTemplate = new(
        id: "TMPLR004",
        title: "No such template",
        messageFormat: "{0} names template '{1}', which is not declared under Templates/",
        category: "Templar",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A [Template] attribute or a ResolveTemplate call names a template, and that name is searched only among the .tpl files under this project's Templates/ folder.");

    public static readonly DiagnosticDescriptor NotConstant = new(
        id: "TMPLR005",
        title: "Template name is not a constant",
        messageFormat: "The template name passed to ResolveTemplate is not a constant",
        category: "Templar",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Templates are resolved at compile time, so the template a ResolveTemplate call names must be a constant string.");

    public static readonly DiagnosticDescriptor NoProperty = new(
        id: "TMPLR006",
        title: "Spot with no property",
        messageFormat: "Spot '{0}' at line {1} of template '{2}' names no readable property of '{3}'",
        category: "Templar",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Each spot names an accessible, readable instance property of the type rendered by the template, matched case-insensitively.");

    public static readonly DiagnosticDescriptor AmbiguousProperty = new(
        id: "TMPLR007",
        title: "Spot matches more than one property",
        messageFormat: "Spot '{0}' at line {1} of template '{2}' matches more than one property of '{3}': {4}",
        category: "Templar",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Spots match properties case-insensitively; a type with two properties differing only in case cannot fill a spot naming them.");

    public static readonly DiagnosticDescriptor CannotRender = new(
        id: "TMPLR008",
        title: "Type cannot render",
        messageFormat: "Type '{0}' cannot render {1}: {2}",
        category: "Templar",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A value renders as text (string, primitive, enum), as a list (Sequence<T>, IEnumerable<T>), or by [Template] through its declared type's hierarchy: every concrete type of this project a value of the declared type can be renders by the most-derived [Template] in its own chain, and each must have one.");

    public static DiagnosticDescriptor ById(string id)
    {
        return id switch
        {
            "TMPLR002" => MalformedTemplate,
            "TMPLR003" => OutsideTemplates,
            "TMPLR004" => NoTemplate,
            "TMPLR005" => NotConstant,
            "TMPLR006" => NoProperty,
            "TMPLR007" => AmbiguousProperty,
            "TMPLR008" => CannotRender,
            _ => throw new System.ArgumentOutOfRangeException(nameof(id), id, "Unknown Templar diagnostic id"),
        };
    }
}
