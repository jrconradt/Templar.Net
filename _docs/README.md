# Templar Documentation

Templar is a template composition engine for code generation: templates are views,
objects are their view models. A template names the properties it uses; any object
carrying them fills it; the Templar generator checks every (type, template) pairing
your code reaches and compiles it at build time. This folder is the long-form
reference; the project [README](../README.md) is the elevator pitch and quick-start.

## Reading order

Each doc stands on its own. Read the model first, then names, wiring and diagnostics;
the syntax sits underneath:

| Doc                              | Read it when…                                                                   |
|----------------------------------|---------------------------------------------------------------------------------|
| [composition.md](composition.md) | You're rendering your objects through templates — defaults, `: Template` spots, variants, lists. |
| [integration.md](integration.md) | You're wiring the generator, reading what it emits or reports, or using Templar inside a source generator. |
| [syntax.md](syntax.md)           | You need the spot, comment, escape, and indentation rules.                      |

## What lives where

| Concept                | Page                                              |
|------------------------|---------------------------------------------------|
| `{{ spot }}` / `{{ spot : Template }}` / `{{ : Template }}` / `{{# comment }}` | [syntax.md](syntax.md) |
| Multi-line indentation behavior                  | [syntax.md](syntax.md#indentation-behavior) |
| The model — templates are views, objects their view models | [composition.md](composition.md#the-model) |
| `[Template]` defaults                            | [composition.md](composition.md#a-types-default-template) |
| How each spot is written, by declared type       | [composition.md](composition.md#how-a-spot-is-written) |
| Variants, the render registry                    | [composition.md](composition.md#variants-are-types) |
| `Sequence<T>` and lists                          | [composition.md](composition.md#lists)          |
| The iterative engine                             | [composition.md](composition.md#the-engine)     |
| `CSharpFile` preset                              | [composition.md](composition.md#worked-preset--csharpfile) |
| Template names                                   | [integration.md](integration.md#template-names) |
| Pairs, generated code, diagnostics `TMPLR002`–`TMPLR009` | [integration.md](integration.md#pairs)  |
| `Compositor.ResolveTemplate` at run time, `RenderOptions` | [integration.md](integration.md#run-time) |
| Templar inside a source generator                | [integration.md](integration.md#inside-a-source-generator) |

## Source pointers

| Type / file                          | Location                                  |
|--------------------------------------|-------------------------------------------|
| `Compositor` (`ResolveTemplate`)     | `src/Rendering/Compositor.cs`             |
| `TemplateAttribute`                  | `src/Rendering/TemplateAttribute.cs`      |
| `TemplarWriter` (the render sink and the writes generated renderers call) | `src/Rendering/TemplarWriter.cs` |
| `RenderRegistry` (renderers by type and template name, defaults by type) | `src/Rendering/RenderRegistry.cs` |
| `Sequence<T>` (+ `Sequence.Lines` / `BlankLines` / `CommaList`) | `src/Rendering/Sequence.cs` |
| `CSharpFile`, `Using`                | `src/Presets/` (templates in `src/Templates/Presets/`) |
| `RenderOptions`                      | `src/Rendering/RenderOptions.cs`          |
| Engine (`Renderer.Drive`)            | `src/Rendering/Renderer.cs`               |
| `RendererGenerator`, `TemplateResolver` (pairs and writes), `TemplateCompiler` (template parsing), diagnostics | `src/Templar.Generators/` |
| Analyzer-loader fixtures             | `fixtures/`                               |
| Test corpus (behavior contract)      | `tests/`                                  |
