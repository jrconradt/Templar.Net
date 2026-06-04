# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test

```bash
dotnet build Templar.slnx          # build everything
dotnet test Templar.slnx           # run the xUnit test suite
```

Target framework is **net10.0**. Tests use **xUnit** (project `tests/Templar.Tests.csproj` — the test files are the authoritative behavior spec; consult `tests/` directly rather than relying on any inventory written here). The main library has zero external dependencies.

## Architecture

Templar is a template engine for C# code generation. The pipeline:

```
Template text → Template.Validate (single-pass scan) → Renderer.Drive (iterative frame loop over a TemplarWriter sink) → Output
```

Parsing and rendering are hand-written single-pass scanners over the source string. There is no separate token stream and no AST — the scanners walk the source directly. Rendering is inverted onto a sink: `Renderer.Drive` runs an iterative loop over an explicit `Stack<object>` of `ScanFrame` / `SequenceFrame` / `CompiledFrame`, writing into a `TemplarWriter` that owns the output, indentation, and line-swallow state — so deeply nested compositors and conditionals do not consume the call stack.

### Template syntax

- `{{ variable }}` — expression placeholder
- `{{ variable | filter }}` — expression with filter (pipe syntax)
- `{{# comment }}` — comment (stripped from output)
- `{{? cond }} … {{?else}} … {{?}}` — conditional block; `{{?!cond}}` for negation
- `\{{`, `\}}`, `\\` — escape sequences (literal delimiters / backslash)

Variable and filter names are **case-insensitive**. Truthiness for conditionals: `null`, `""`, `false`, and empty `IEnumerable` are falsy; everything else truthy. Whitespace-only lines whose placeholders all rendered empty are swallowed entire (including the trailing newline).

### Core types

| Type | Location | Role |
|------|----------|------|
| `Template` | `src/Rendering/Template.cs` | Main API — parse, set variables, add filters, render. Hosts `Validate` (the parse-time scanner). |
| `TemplateSet` | `src/Rendering/TemplateSet.cs` | Load templates by name from strings, directories, or embedded resources |
| `IComposable` | `src/Rendering/IComposable.cs` | Composition primitive — `void RenderInto(TemplarWriter)` (+ default `Render()`). The engine dispatches injected values on this interface, not on concrete base classes. |
| `TemplarWriter` | `src/Rendering/TemplarWriter.cs` | The render sink — owns output, indentation, line-swallow state, the frame stack, and the public `Literal`/`Value`/`Truthy`/`Compiled` surface that compiled accessors emit against. |
| `Compositor` | `src/Rendering/Compositor.cs` | Base for structured generators; implements `IComposable`. `Structure` (protected virtual) + `Populate` (protected virtual, default reflects bindable properties). Hand-written subclasses interpret `Structure`; generated `.tpl` accessors override `RenderInto` with compiled code. |
| `Sequence` (+ `Lines`/`BlankLines`/`CommaList` factories) | `src/Rendering/Sequence.cs` | Sealed `IComposable` — `new Sequence(items, separator)` composes other `IComposable` values; the three names are static factory methods, not subclasses. |
| `CSharpFile`, `Using` | `src/Presets/` | Preset compositors for C# files and using-directives |

### Parsing & rendering

- **`Template.Validate`** (in `src/Rendering/Template.cs`) — single-pass character scanner that catches unclosed tags, malformed conditionals, and balance errors at parse time. Tracks line/column for diagnostics.
- **`Renderer.Drive`** (in `src/Rendering/Renderer.cs`) — internal static, the render loop. Iterates an explicit `Stack<object>` of frames, writing into a `TemplarWriter`; handles literal text, value/filter expressions, comments, conditionals (with `else` and `!` negation), escapes, and line-swallowing. Injected values dispatch by interface — `IPreformattedContent`, `IIndentedContent`, `string`, `IComposable` (via `RenderInto`), `IEnumerable<IComposable>`, `IEnumerable<string>`, else `ToString()`. `Renderer.Render` and `Compositor.Render` seed a `TemplarWriter` and call `Drive`.
- **`TemplarWriter`** (in `src/Rendering/TemplarWriter.cs`) — the output sink and write primitives, plus the public `Literal` / `Value` / `Truthy` / `Compiled` API compiled accessors emit against.
- **`FilterRegistry`** (in `src/Rendering/FilterRegistry.cs`) — built-in filters: `upper`, `lower`, `pascal`, `camel`. Extensible via `AddFilter` on the interpreted path; compiled accessors use the built-ins only.

Indentation behavior — multi-line values inherit the column position of the placeholder — lives in `TemplarWriter` via the `WriteValueString` / `EmitNewline` / `currentIndent` triad.

### Composition model

Templates compose by rendering an inner template and injecting its output as a variable in an outer template. Indentation is preserved across these boundaries. The `Compositor` base class formalizes this as `Structure` (the template text — `protected virtual`, default loads an embedded `.tpl` resource matching the type's `FullName`) plus `Populate(Template)` (default: reflect bindable instance properties and write each as a variable, honoring `[TemplateBind]` and `[TemplateIgnore]`). This is the **interpreted** path used by hand-written subclasses. The `TemplateAccessorGenerator` instead **compiles** a `.tpl` into a `Compositor` that overrides `RenderInto` directly — no `Structure`, no runtime parse, no embedded resource.

### Presets

`CSharpFile` (in `src/Presets/`) is the only built-in preset. It extends `Compositor` with a five-section structure: `Header` (virtual, defaults to `// <auto-generated />\n#nullable enable`), `Pragmas` (virtual, defaults to empty), `Usings` (an `IEnumerable<string>` rendered into `UsingsBlock`), `Namespace` (virtual init, file-scoped — required for valid output), and `Body` (virtual). Use the object-initializer pattern or subclass it and override the properties to generate complete `.cs` files.

### Custom Filters

Register filters via `template.AddFilter("name", Func<object?, string>)` or on a `TemplateSet`. Filter names are case-insensitive. The four built-in filters (`upper`, `lower`, `pascal`, `camel`) can be overridden.

## Conventions

- Fluent API — `Template.Set()`, `AddFilter()`, `WithOptions()` all return `this`
- `RenderOptions` controls indent string (default 4 spaces) and newline character
- Parse/render errors throw `TemplateParseException` (with line/column) or `TemplateRenderException`
