# Templar Documentation

Templar is a type-resolved template composition engine for C# code
generation: a type is its template, its properties are the template's holes,
and every value composes by its own type. This folder is the long-form
reference; the project [README](../README.md) is the elevator pitch and
quick-start.

## Reading order

Each doc stands on its own. Read the model first, then how a template is found
for a type; the syntax and the string-keyed `Template` API sit underneath:

| Doc                              | Read it when…                                                                   |
|----------------------------------|---------------------------------------------------------------------------------|
| [composition.md](composition.md) | You're modelling what you generate as types — variants as subtypes, lists of types. |
| [integration.md](integration.md) | You're embedding a type's template, weighing the accessor generator, or loading templates by name. |
| [syntax.md](syntax.md)           | You need the placeholder, comment, and indentation rules.                       |
| [templates.md](templates.md)     | You're rendering a template with no type behind it, filled by string key.      |

## What lives where

| Concept                | Page                                              |
|------------------------|---------------------------------------------------|
| `{{ var }}` / `{{# comment }}`                   | [syntax.md](syntax.md)                          |
| Multi-line indentation behavior                  | [syntax.md](syntax.md#indentation-behavior) |
| `Template.Parse`, `Set`, `Render`                | [templates.md](templates.md)                    |
| `RenderOptions` (newline + indent)               | [templates.md](templates.md#render-options)     |
| `TemplateParseException`                         | [templates.md](templates.md#exceptions), [integration.md](integration.md#exception-model) |
| The model — type is template, values compose by type | [composition.md](composition.md#the-model)  |
| `Compositor`, `Structure`, `Populate`            | [composition.md](composition.md)                |
| `[TemplateBind]` / `[TemplateIgnore]`            | [composition.md](composition.md#auto-binding-rules) |
| `CSharpFile` preset                              | [composition.md](composition.md#worked-preset--csharpfile) |
| `<FullName>.tpl` lookup, embedding a template beside its type | [integration.md](integration.md#finding-a-template-for-a-type) |
| `.tpl` accessor generator (template-first) and its limits | [integration.md](integration.md#the-tpl-accessor-generator) |
| `TemplateSet` — string-keyed loading, options propagation | [integration.md](integration.md#templateset--string-keyed-templates) |
| Render patterns                                  | [integration.md](integration.md#patterns)       |

## Source pointers

| Type / file                          | Location                                  |
|--------------------------------------|-------------------------------------------|
| `Template`                           | `src/Rendering/Template.cs`               |
| `TemplateSet`                        | `src/Rendering/TemplateSet.cs`            |
| `Compositor`, `[TemplateBind]`, `[TemplateIgnore]` | `src/Rendering/Compositor.cs` |
| `IComposable` (composition primitive) | `src/Rendering/IComposable.cs`           |
| `TemplarWriter` (the render sink)    | `src/Rendering/TemplarWriter.cs`          |
| `CSharpFile`                         | `src/Presets/CSharpFile.cs`               |
| `RenderOptions`                      | `src/Rendering/RenderOptions.cs`          |
| Parser (`Template.Validate`)         | `src/Rendering/Template.cs`               |
| Renderer (`Renderer.Drive`)          | `src/Rendering/Renderer.cs`               |
| `Sequence` (+ `Lines` / `BlankLines` / `CommaList` factories) | `src/Rendering/Sequence.cs` |
| `TemplateAccessorGenerator` (template-first `.tpl` → sealed accessor class) | `src/Templar.Generators/` |
| Test corpus (behavior contract)      | `tests/`                                  |
