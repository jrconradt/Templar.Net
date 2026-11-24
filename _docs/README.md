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
| [composition.md](composition.md) | You're rendering your objects through templates — `[Template]` types, lists. |
| [integration.md](integration.md) | You're wiring the generator, reading what it emits or reports, or using Templar inside a source generator. |
| [syntax.md](syntax.md)           | You need the spot, comment, escape, and indentation rules.                      |

## What lives where

| Concept                | Page                                              |
|------------------------|---------------------------------------------------|
| `{{ spot }}` / `{{# comment }}`                  | [syntax.md](syntax.md)               |
| Multi-line indentation behavior                  | [syntax.md](syntax.md#indentation-behavior) |
| The model — templates are views, objects their view models | [composition.md](composition.md#the-model) |
| `[Template]` on a type                           | [composition.md](composition.md#a-types-template) |
| How each spot is written, by declared type       | [composition.md](composition.md#how-a-spot-is-written) |
| `Sequence<T>` and lists                          | [composition.md](composition.md#lists)          |
| The iterative engine                             | [composition.md](composition.md#the-engine)     |
| Template names                                   | [integration.md](integration.md#template-names) |
| Pairs, generated code, diagnostics `TMPLR002`–`TMPLR008` | [integration.md](integration.md#pairs)  |
| `Compositor.ResolveTemplate` at run time, `RenderOptions` | [integration.md](integration.md#run-time) |
| Templar inside a source generator                | [integration.md](integration.md#inside-a-source-generator) |

## Source pointers

| Type / file                          | Location                                  |
|--------------------------------------|-------------------------------------------|
| `Compositor` (`ResolveTemplate`)     | `src/Rendering/Compositor.cs`             |
| `TemplateAttribute`                  | `src/Rendering/TemplateAttribute.cs`      |
| `TemplateTextAttribute` (a template's text, written into the assembly) | `src/Rendering/TemplateTextAttribute.cs` |
| `TemplarWriter` (the render sink and the writes generated renderers call) | `src/Rendering/TemplarWriter.cs` |
| `Sequence<T>` (+ `Sequence.Lines` / `BlankLines` / `CommaList`) | `src/Rendering/Sequence.cs` |
| `RenderOptions`                      | `src/Rendering/RenderOptions.cs`          |
| Engine (`Renderer.Drive`)            | `src/Rendering/Renderer.cs`               |
| `RendererGenerator`, `TemplateResolver` (pairs and writes), `TemplateCompiler` (template parsing), diagnostics | `src/Templar.Generators/` |
| Analyzer-loader fixtures             | `fixtures/`                               |
| Test corpus (behavior contract)      | `tests/`                                  |
