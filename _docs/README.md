# Templar Documentation

Templar is a small, indentation-aware template engine for C# code generation.
This folder is the long-form reference; the project [README](../README.md) is
the elevator pitch and quick-start.

## Reading order

Each doc stands on its own. The most direct path from "I want to fill in a
string" to "I want to wire this into a build" is to read them in this order:

| Doc                              | Read it when…                                                                   |
|----------------------------------|---------------------------------------------------------------------------------|
| [syntax.md](syntax.md)           | You need the placeholder, filter, comment, and indentation rules.               |
| [templates.md](templates.md)     | You're constructing and rendering a single template at a time.                  |
| [composition.md](composition.md) | You want a class to **be** the template — properties auto-bind to variables.    |
| [integration.md](integration.md) | You're loading templates from disk or embedded resources, or wiring a generator.|

## What lives where

| Concept                | Page                                              |
|------------------------|---------------------------------------------------|
| `{{ var }}` / `{{ var \| filter }}` / `{{# comment }}` | [syntax.md](syntax.md)                  |
| Multi-line indentation behavior                  | [syntax.md](syntax.md#indentation-behavior) |
| Built-in filters (`upper`/`lower`/`pascal`/`camel`) | [syntax.md](syntax.md#built-in-filters), [templates.md](templates.md#filters) |
| `Template.Parse`, `Set`, `Render`, `AddFilter`   | [templates.md](templates.md)                    |
| `RenderOptions` (newline + indent)               | [templates.md](templates.md#render-options)     |
| `TemplateParseException` / `TemplateRenderException` | [templates.md](templates.md#exceptions), [integration.md](integration.md#exception-model) |
| `Compositor`, `Structure`, `Populate`            | [composition.md](composition.md)                |
| `[TemplateBind]` / `[TemplateIgnore]`            | [composition.md](composition.md#auto-binding-rules) |
| `CSharpFile` preset                              | [composition.md](composition.md#worked-preset--csharpfile) |
| `TemplateSet` — directories, embedded resources, filter/options propagation | [integration.md](integration.md) |
| Bulk render / generator patterns                 | [integration.md](integration.md#patterns)       |
| `.tpl` accessor generator (`Templates/*.tpl` → compiled `Compositor`) | [integration.md](integration.md#the-tpl-accessor-generator) |

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
| Renderer (`Renderer.Drive`) + filters | `src/Rendering/`                         |
| `Sequence` (+ `Lines` / `BlankLines` / `CommaList` factories) | `src/Rendering/Sequence.cs` |
| `TemplateAccessorGenerator` (`.tpl` → compiled `Compositor`) | `src/Templar.Generators/` |
| Test corpus (behavior contract)      | `tests/`                                  |
