# Templar Documentation

Templar is a type-resolved template composition engine for C# code
generation: a plain type is its template, its properties are the template's
holes, and every value composes by its own type. Its generator compiles each
template under `Templates/` into the renderer of the type at its namespace path
and checks both at compile time. This folder is the long-form reference; the project
[README](../README.md) is the elevator pitch and quick-start.

## Reading order

Each doc stands on its own. Read the model first, then how a template finds its
type and how the generator is wired; the syntax and the string-keyed `Template`
API sit underneath:

| Doc                              | Read it when…                                                                   |
|----------------------------------|---------------------------------------------------------------------------------|
| [composition.md](composition.md) | You're modelling what you generate as types — variants as interface implementations, lists. |
| [integration.md](integration.md) | You're wiring the generator, reading its diagnostics, using Templar inside a source generator, or loading templates by name. |
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
| How each hole is written, by declared type       | [composition.md](composition.md#how-a-hole-is-written) |
| Variants, the render registry                    | [composition.md](composition.md#variants-are-types) |
| `Sequence<T>` and lists                          | [composition.md](composition.md#lists)          |
| The iterative engine                             | [composition.md](composition.md#the-engine)     |
| `CSharpFile` preset                              | [composition.md](composition.md#worked-preset--csharpfile) |
| How a `.tpl` finds its type                      | [integration.md](integration.md#finding-the-type-of-a-template) |
| Wiring, generated code, diagnostics `TMPLR001`–`TMPLR008` | [integration.md](integration.md#wiring)  |
| Templar inside a source generator                | [integration.md](integration.md#inside-a-source-generator) |
| `TemplateSet` — string-keyed loading, options propagation | [integration.md](integration.md#templateset--string-keyed-templates) |
| Render patterns                                  | [integration.md](integration.md#patterns)       |

## Source pointers

| Type / file                          | Location                                  |
|--------------------------------------|-------------------------------------------|
| `Template`                           | `src/Rendering/Template.cs`               |
| `TemplateSet`                        | `src/Rendering/TemplateSet.cs`            |
| `TemplarWriter` (the render sink and the writes generated renderers call) | `src/Rendering/TemplarWriter.cs` |
| `RenderRegistry` (renderers by runtime type, for interface holes) | `src/Rendering/RenderRegistry.cs` |
| `Sequence<T>` (+ `Sequence.Lines` / `BlankLines` / `CommaList`) | `src/Rendering/Sequence.cs` |
| `CSharpFile`, `Using`                | `src/Presets/` (templates in `src/Templates/Presets/`) |
| `RenderOptions`                      | `src/Rendering/RenderOptions.cs`          |
| Parser (`Template.Validate`)         | `src/Rendering/Template.cs`               |
| Engine (`Renderer.Drive`)            | `src/Rendering/Renderer.cs`               |
| `RendererGenerator` (each `.tpl` → its type's renderer), `TemplateResolver`, diagnostics | `src/Templar.Generators/` |
| Analyzer-loader fixtures             | `fixtures/`                               |
| Test corpus (behavior contract)      | `tests/`                                  |
