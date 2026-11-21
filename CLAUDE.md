# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Test

```bash
dotnet build Templar.slnx          # build everything (library, generator, fixtures, tests)
dotnet test Templar.slnx           # run the xUnit test suites
```

Target framework is **net10.0**. Tests use **xUnit** in two projects — `tests/Templar.Tests.csproj` (runtime; its view models live under `tests/Models/`, its templates under `tests/Templates/`, compiled by the generator) and `tests/Templar.Generators.Tests/` (generator, diagnostics, emitted code, and the fixture consumer). The test files are the authoritative behavior spec; consult `tests/` directly rather than relying on any inventory written here. The main library has zero external dependencies.

## Architecture

Templar is a template engine for code generation. Templates are views; objects are their view models. A template is a `.tpl` `AdditionalFile` under `<ProjectDir>/Templates/` — any text — and its name is its path there without `.tpl` (`Templates/Injection/Constructor.tpl` → `"Injection/Constructor"`). Its spots name properties. The template is the contract: any type carrying those properties can be rendered by it, and it does not change for the type. A class or interface names its template with `[Template("Name")]`. The pipeline:

```
.tpl files + [Template] types + Compositor.ResolveTemplate call sites
  → RendererGenerator (compile time: reachable (type, template) pairs, each spot bound and its write chosen)
  → <RootNamespace>.Templates.<folders>.<Leaf>Template.Steps — one flat iterator per pair
  → Templar.Interceptors.g.cs — one interceptor per pair a call site resolves to, [InterceptsLocation] on each such call
Compositor.ResolveTemplate(obj[, "Name"]) → intercepted: TemplarWriter.Render(model, <Pair>Template.Steps, options)
                                            (not resolved at compile time: the body throws InvalidOperationException)
  → Renderer.Drive (iterative frame loop over a TemplarWriter sink) → string
```

No `.tpl` is read or parsed at run time; template text is baked into the generated renderers as string literals, and a template no pair reaches is syntax-checked but not compiled in. Rendering is inverted onto a sink: `Renderer.Drive` runs an iterative loop over an explicit `Stack<object>` of `SequenceFrame` / `CompiledFrame`, writing into a `TemplarWriter` that owns the output, indentation, and line-swallow state — so deeply nested object graphs do not consume the call stack.

### Template syntax

- `{{ property }}` — spot, written by the property's declared type
- `{{# comment }}` — comment (stripped from output)
- `\{{`, `\}}`, `\\` — escape sequences (literal delimiters / backslash)

A tag that opens with `{{?`, `{{&`, or `{{>`, contains a `|`, or is not a property name is rejected at compile time (`TMPLR002`).

Property names are **case-insensitive**. Whitespace-only lines whose spots all rendered empty are swallowed entire (including the trailing newline).

### Core types

| Type | Location | Role |
|------|----------|------|
| `RendererGenerator` | `src/Templar.Generators/RendererGenerator.cs` | Incremental generator: `.tpl` AdditionalFiles + settings + compilation → `TemplateResolver.Resolve` → one source per reached template and one `Templar.Interceptors.g.cs`, plus diagnostics. Pipeline outputs are equatable records (`PipelineRecords.cs`: `TemplateFile`, `ProjectSettings`, `Site`, `Finding`, `TemplateOutput`; `EquatableArray<T>`), never symbols, `Compilation` or `Location`. |
| `TemplateResolver` | `src/Templar.Generators/TemplateResolver.cs` | Declares templates by name; seeds pairs from `[Template]` types and `Compositor.ResolveTemplate` call sites (static type + constant name); binds each pair's spots to properties and chooses each write from the declared type (string → `w.Text`, primitive/enum → `w.Format`, `[Template]` class or interface of this project → `w.Compose(v, …Template.Steps)`, `Sequence<T>`/`IEnumerable<T>` → `w.List`, anything else — including a `[Template]` type from another assembly — `TMPLR008`), following new pairs; emits one class per template with a `Steps` overload per type; and, for each `ResolveTemplate(obj, "Name")` call and each `ResolveTemplate(obj)` call on a project `[Template]` type, an interceptor in `Templar.Generated` (`[InterceptsLocation]` from `SemanticModel.GetInterceptableLocation`) whose body is `TemplarWriter.Render(model, <Pair>Template.Steps, options)` — calls to the same pair through the same overload share one. Diagnostics `TMPLR002`–`TMPLR008` (`Descriptors.cs`, tracked in `AnalyzerReleases.Unshipped.md`). |
| `TemplateCompiler`, `TemplateLocation` | `src/Templar.Generators/` | Template validation (including the spot grammar) and parsing; template path → name under `<ProjectDir>/Templates/` (outside it: `TMPLR003`). |
| `Compositor` | `src/Rendering/Compositor.cs` | Static entry: `ResolveTemplate<T>(T model, string template, RenderOptions? options = null)` and `ResolveTemplate<T>(T model, RenderOptions? options = null)` (by `T`'s `[Template]`). Every call is intercepted at compile time; the bodies only throw `InvalidOperationException` saying the call was not resolved at compile time (possible only when the generator does not run on the calling project). |
| `TemplateAttribute` | `src/Rendering/TemplateAttribute.cs` | `[Template("Name")]` on a class or interface: its template. |
| `TemplarWriter` | `src/Rendering/TemplarWriter.cs` | The render sink — owns output, indentation, line-swallow state, the frame stack, and the public writes generated renderers call (`Literal`, `Text`, `Format`, `Compose`, `List`, the `*Item` steps, static `Render`). `null` and empty lists are handled here, so generated code holds no conditional. |
| `Sequence<T>` (+ static `Sequence.Lines`/`BlankLines`/`CommaList`) | `src/Rendering/Sequence.cs` | A list and its separator. Separators go through `WriteSeparator`, with `\n` translated to `RenderOptions.Newline`. |

### Rendering

- **`Renderer.Drive`** (in `src/Rendering/Renderer.cs`) — internal static, the one render loop. `CompiledFrame` steps a generated renderer, and each non-null step it yields is a frame (nested template or list) pushed at the spot's column; `SequenceFrame` steps a list's items, writing the separator between them.
- **`TemplarWriter`** — the output sink and write primitives.

Indentation behavior — multi-line values inherit the column position of the spot — lives in `TemplarWriter` via the `WriteValueString` / `EmitNewline` / `currentIndent` triad and `PushColumnIndent` / `PopIndent` for nested frames.

### Composition model

A template is a view: it names properties and knows nothing of the data model. An object is a view model: any type, in its own shape, checked at compile time to carry what each template it is rendered by uses. Each spot's write is chosen from the property's declared type; the generated renderer is flat — `w.Literal(...)` and one write per spot, no `if`, no loop. A property whose declared type carries `[Template]` renders by that template, bound at compile time; an interface with `[Template]` binds the interface's own properties, whatever object the property holds. Nothing is chosen at run time. Lists are `Sequence<T>` (separator chosen when the object is built) or `IEnumerable<T>` (newline). No base class, no reflection binding, no run-time template parsing. Templar knows nothing of its consumers. See `_docs/composition.md` and `_docs/integration.md`.

### Build wiring

`src/Templar.csproj` references `Templar.Generators` as an analyzer (`OutputItemType="Analyzer" ReferenceOutputAssembly="false" PrivateAssets="all"`), lists `Templates/**/*.tpl` as `AdditionalFiles`, and makes `RootNamespace`/`ProjectDir` compiler-visible; `tests/Templar.Tests.csproj` and `fixtures/Fixture.Generator` do the same for their `Templates/` folders and add `Templar.Generated` to `InterceptorsNamespaces`. The generator has no reference to Templar.dll. The package ships the generator under `analyzers/dotnet/cs` and build targets exposing `RootNamespace`/`ProjectDir` and adding `Templar.Generated` to `InterceptorsNamespaces`. `fixtures/Fixture.Generator` (a source generator that renders with `Compositor.ResolveTemplate`, shipping Templar.dll via `GetDependencyTargetPaths`) and `fixtures/Fixture.Consumer` prove the Roslyn analyzer-loader path.

## Conventions

- Sealed by default; braces on every `if`; Allman; hand-written scanners, no regex; no XML docs; xUnit `Assert.Equal(expected, actual)`
- `RenderOptions` controls indent string (default 4 spaces) and newline character
- Template errors (malformed, outside `Templates/`, unknown names, non-constant names, missing properties, types that cannot render) are compile-time diagnostics; run time only throws `InvalidOperationException` for a `ResolveTemplate` call that was not resolved at compile time
