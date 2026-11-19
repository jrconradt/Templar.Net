# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test

```bash
dotnet build Templar.slnx          # build everything (library, generator, fixtures, tests)
dotnet test Templar.slnx           # run the xUnit test suites
```

Target framework is **net10.0**. Tests use **xUnit** in two projects — `tests/Templar.Tests.csproj` (runtime; its model types live under `tests/Models/` and `tests/DefaultNaming/`, their `.tpl` files under `tests/Templates/` at the types' namespace paths, compiled by the generator) and `tests/Templar.Generators.Tests/` (generator, diagnostics, emitted code, and the fixture consumer). The test files are the authoritative behavior spec; consult `tests/` directly rather than relying on any inventory written here. The main library has zero external dependencies.

## Architecture

Templar is a template engine for C# code generation. Templates live under `Templates/`; types live where the code lives. A plain, sealed, non-generic, top-level class or record `X` has a template when `X.tpl` is an `AdditionalFile` under `<ProjectDir>/Templates/` whose path after `Templates/` is `X`'s namespace path below `RootNamespace` (`Templates/Injection/Constructor.tpl` → `<RootNamespace>.Injection.Constructor`, wherever its `.cs` sits). The pipeline:

```
X.tpl + X → RendererGenerator (compile time: resolve type, validate, choose each hole's write) → XRenderer.Steps (flat iterator)
model.Render() → Renderer.Drive (iterative frame loop over a TemplarWriter sink) → Output
```

Parsing and rendering are hand-written single-pass scanners over the source string. There is no separate token stream and no AST. Rendering is inverted onto a sink: `Renderer.Drive` runs an iterative loop over an explicit `Stack<object>` of `ScanFrame` / `SequenceFrame` / `CompiledFrame`, writing into a `TemplarWriter` that owns the output, indentation, and line-swallow state — so deeply nested models do not consume the call stack.

### Template syntax

- `{{ variable }}` — expression placeholder
- `{{# comment }}` — comment (stripped from output)
- `\{{`, `\}}`, `\\` — escape sequences (literal delimiters / backslash)

A tag that opens with `{{?`, `{{&`, or `{{>`, or that contains a `|`, is rejected at parse time (`TemplateParseException`) and at compile time (`TMPLR002`).

Variable names are **case-insensitive**. Whitespace-only lines whose placeholders all rendered empty are swallowed entire (including the trailing newline).

### Core types

| Type | Location | Role |
|------|----------|------|
| `RendererGenerator` | `src/Templar.Generators/RendererGenerator.cs` | Incremental generator. Each `.tpl` under `<ProjectDir>/Templates/` → `<RootNamespace>.<folders after Templates/>.<file>` (source types only, via `compilation.Assembly.GetTypeByMetadataName`); emits `XRenderer` with `Render(this X, RenderOptions?)`, the flat `Steps` iterator, and a module initializer registering `Steps`. Pipeline outputs are equatable records (`PipelineRecords.cs`, `EquatableArray<T>`). |
| `TemplateResolver` | `src/Templar.Generators/TemplateResolver.cs` | The compile-time predicates: type resolution and shape, hole → property, and the write per declared type (string → `w.Text`, primitive/enum → `w.Format`, mapped type → `w.Compose(v, XRenderer.Steps)`, `Sequence<T>`/`IEnumerable<T>` → `w.List`, interface → `w.Compose(v, RenderRegistry.Steps)`). Diagnostics `TMPLR002`–`TMPLR008` (`Descriptors.cs`, tracked in `AnalyzerReleases.Unshipped.md`). |
| `TemplateCompiler`, `TemplateLocation` | `src/Templar.Generators/` | Template validation/parsing for the generator; template path → folder segments + leaf under `<ProjectDir>/Templates/` (outside it: `TMPLR003`). |
| `InlineTemplateParseAnalyzer` | `src/Templar.Generators/` | `TMPLR001` — a string literal passed to `Template.Parse` belongs in a `.tpl` under `Templates/`. |
| `TemplarWriter` | `src/Rendering/TemplarWriter.cs` | The render sink — owns output, indentation, line-swallow state, the frame stack, and the public writes generated renderers call (`Literal`, `Text`, `Format`, `Compose`, `List`, the `*Item` steps, static `Render`). `null` and empty lists are handled here, so generated code holds no conditional. |
| `RenderRegistry` | `src/Rendering/RenderRegistry.cs` | `Type` → renderer, filled by generated module initializers (`Register` overwrites); `Steps` dispatches an interface-typed value by runtime type, throwing for an unregistered type. The only runtime decision. |
| `Sequence<T>` (+ static `Sequence.Lines`/`BlankLines`/`CommaList`) | `src/Rendering/Sequence.cs` | A list and its separator. Separators go through `WriteSeparator`, with `\n` translated to `RenderOptions.Newline`. |
| `Template` | `src/Rendering/Template.cs` | The string-keyed lower layer — parse, set variables, render. Hosts `Validate` (the parse-time scanner). Values are text (`string`, `IEnumerable<string>`, else `ToString()`). |
| `TemplateSet` | `src/Rendering/TemplateSet.cs` | Load string-keyed templates by name from strings, directories, or embedded resources |
| `CSharpFile`, `Using` | `src/Presets/` | Preset plain types, their `.tpl` in `src/Templates/Presets/`, compiled by Templar's own generator |

### Parsing & rendering

- **`Template.Validate`** (in `src/Rendering/Template.cs`) — single-pass character scanner that catches unclosed tags and unsupported tag syntax at parse time. Tracks line/column for diagnostics.
- **`Renderer.Drive`** (in `src/Rendering/Renderer.cs`) — internal static, the one render loop. `ScanFrame` interprets a `Template`; `CompiledFrame` steps a generated renderer, and each non-null step it yields is a frame (nested type or list) pushed at the hole's column; `SequenceFrame` steps a list's items, writing the separator between them.
- **`TemplarWriter`** — the output sink and write primitives.

Indentation behavior — multi-line values inherit the column position of the placeholder — lives in `TemplarWriter` via the `WriteValueString` / `EmitNewline` / `currentIndent` triad and `PushColumnIndent` / `PopIndent` for nested frames.

### Composition model

A type is its template. Holes bind to properties by name at compile time; each hole's write is chosen from the property's declared type, and the generated renderer is flat — `w.Literal(...)` and one write per hole, no `if`, no loop. Variants are sealed implementations of an interface, each with its own `.tpl`; an interface-typed hole renders by the value's runtime type through `RenderRegistry`. Lists are `Sequence<T>` (separator chosen when the model is built) or `IEnumerable<T>` (newline). There is no base class, no attribute, no reflection binding, and no runtime template lookup. Templar knows nothing of its consumers. See `_docs/composition.md` and `_docs/integration.md`.

### Build wiring

`src/Templar.csproj` references `Templar.Generators` as an analyzer (`OutputItemType="Analyzer" ReferenceOutputAssembly="false" PrivateAssets="all"`), lists `Templates/**/*.tpl` as `AdditionalFiles`, and makes `RootNamespace`/`ProjectDir` compiler-visible; `tests/Templar.Tests.csproj` and `fixtures/Fixture.Generator` do the same for their `Templates/` folders. The generator has no reference to Templar.dll. The package ships the generator under `analyzers/dotnet/cs` and build targets exposing `RootNamespace`/`ProjectDir`. `fixtures/Fixture.Generator` (a source generator whose model types are compiled by Templar, shipping Templar.dll via `GetDependencyTargetPaths`) and `fixtures/Fixture.Consumer` prove the Roslyn analyzer-loader path.

### Presets

`CSharpFile` (in `src/Presets/`) is the built-in preset: a sealed plain type with a five-section template — `Header` (defaults to `#nullable enable`), `Pragmas` (defaults to empty), `UsingsBlock` (a `Sequence<Using>` built from `Usings`, an `IEnumerable<string>`), `Namespace` (file-scoped — required for valid output), and `Body` (a `string`, placed as text — not rescanned, not composed by type). Its output is byte-identical to 0.2.0 under default options (pinned in `tests/CSharpFileTests.cs`).

## Conventions

- Sealed by default; braces on every `if`; Allman; hand-written scanners, no regex; no XML docs; xUnit `Assert.Equal(expected, actual)`
- Fluent API — `Template.Set()` and `WithOptions()` return `this`
- `RenderOptions` controls indent string (default 4 spaces) and newline character
- Parse errors throw `TemplateParseException` (with line/column); errors in a `.tpl` under `Templates/`, or a `.tpl` outside it, are compile-time diagnostics
