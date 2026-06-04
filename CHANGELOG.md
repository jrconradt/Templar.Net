# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- `Templar.UI` — a server-rendered markup component toolkit on top of the
  Templar engine. `UIComponent` (a `Compositor` that HTML-escapes text by
  default), `Element` + the `Markup` element factory (generated from the
  `Elements.elements` data table), and a `Document` page preset. Styling is
  compositional, not imperative: a style is a micro-template that contributes
  class tokens, and an element's classes are a space-joined `Sequence` —
  composition is the merge. Elements carry a `DefaultClass` that
  composes with, rather than replaces, the caller's classes. `Attr` builds
  escaped, scheme-sanitized attributes; `Markup.Inline`/`Text` compose
  prose with inline markup; `<pre>`/`<textarea>` use a verbatim layout that
  escapes content without reindenting it. Zero dependencies, trim- and AOT-safe.
  See [_docs/ui.md](_docs/ui.md).
- Templar.UI is safe-by-default against XSS/injection: tag names and attribute
  names are validated, `on*` event-handler attributes are blocked, URL-context
  attribute values (`href`, `src`, …) with `javascript:`/`vbscript:`/`data:`
  schemes (including control-char-obfuscated) are neutralized to
  `about:invalid#blocked`, and the `attrs` slot rejects raw strings. Unsafe
  input throws `MarkupSecurityException` (fail closed). The only unescaped paths
  are the explicit `Markup.Raw`, `{{& }}`, and `RawContent` opt-outs.
- `Templar.UI.Generators` — `ElementFactoryGenerator` (emits the `Markup` factory
  from `.elements` tables) and `HtmlComponentGenerator` (turns `.html.tpl` files
  into strongly-typed `UIComponent` subclasses, typing each placeholder as
  `string`, `RawHtml`, or `Compositor?` from its marker).
- Engine: `RenderOptions.Escape` (an optional `Func<string,string>` applied to
  interpolated text), the `IPreformattedContent` marker interface (values written
  verbatim — literal newlines, no per-line reindentation), the `IIndentedContent`
  marker interface (multi-line values reindented to the placeholder's column), and
  two placeholder markers — `{{& x }}` (raw, unescaped) and `{{> x }}` (slot). All
  default-off, so existing code-generation output is unchanged.
- `IComposable` — the public composition primitive: `void RenderInto(TemplarWriter)`
  plus a default `Render()`. `Compositor` and `Sequence` implement it, and the
  renderer dispatches injected values on the interface rather than on concrete base
  classes, so any value that can write itself into the sink composes.
- `TemplarWriter` — the public render sink the engine writes through, exposing
  `Literal`/`Value`/`Truthy`/`Compiled` for compiled output.

### Changed
- The renderer is inverted onto the `TemplarWriter` sink: `Renderer.Drive` runs a
  single iterative frame loop (`ScanFrame`/`SequenceFrame`/`CompiledFrame`) writing
  into the sink, and `Template.Render` / `Compositor.Render` seed it. Rendered output
  is unchanged.
- `Sequence` is now a sealed `IComposable` constructed as
  `new Sequence(items, separator)`; `Lines`/`BlankLines`/`CommaList` are static
  factory methods (`Sequence.Lines(...)`), not subclasses, and items are
  `IEnumerable<IComposable>`. The previous `new Lines { Items = … }`
  object-initializer form is removed.
- `TemplateAccessorGenerator` now compiles each `.tpl` into a `Compositor` that
  overrides `RenderInto` with straight-line code instead of carrying a `Structure`
  string. Generated accessors no longer require the `.tpl` as an `EmbeddedResource`
  (only `AdditionalFiles`); conditional variables (`{{? c }}`) now become
  `required object?` properties; compiled accessors apply the four built-in filters
  only.

### Removed
- `Templar.UI`'s `ClassList` and `Fragment` types (thin `Sequence` subclasses); use
  `new Sequence(tokens, " ")` / `new Sequence(items, "")` or `Markup.Inline`.

### Fixed
- `Sequence` items after the first now inherit the placeholder's column when the
  separator ends in a newline, so multi-item sequences nested at an indented
  position stay aligned (previously only the first item was indented).

## [1.0.0]

Initial production release.

### Added
- `Template` — parse, set variables, add filters, render. Variable and filter
  names are case-insensitive.
- `TemplateSet` — load templates by name from strings, directories of `.tpl`
  files, or embedded resources; propagates filters and `RenderOptions` to
  templates produced by `Get`.
- `Compositor` — abstract base for declarative generators. Defines `Structure`
  (the template text) and auto-binds every readable instance property to a
  template variable matching the property name. Per-type parse + reflection
  caches so emitting hundreds of files doesn't reparse the structure each
  time. Rename a binding with `[TemplateBind("name")]`, skip a property with
  `[TemplateIgnore]`.
- `CSharpFile` preset — four-section structure for C# files: header (default
  `// <auto-generated />\n#nullable enable`), pragmas, usings, file-scoped
  namespace, body. Subclass and use the object-initializer pattern.
- Built-in filters: `upper`, `lower`, `pascal`, `camel`. Custom filters via
  `Template.AddFilter` or `TemplateSet.AddFilter`.
- Indentation-aware rendering — multi-line values inherit the column position
  of the placeholder, so substituted code keeps its relative indentation
  inside containing blocks at arbitrary nesting depth.
- `RenderOptions` for configurable newline (and strict-undefined mode). The
  renderer normalizes `\r\n` and lone `\r` in injected content and joins
  `IEnumerable<string>` values using the configured newline, so output line
  endings are consistent regardless of the input's.
- `TemplateParseException` (line + column) and `TemplateRenderException`
  (filter name, variable name, template name) for diagnostics.
- Trim- and AOT-friendly: `IsAotCompatible` and `IsTrimmable` are set, and
  `Compositor`'s reflection-based property binding is annotated with
  `[DynamicallyAccessedMembers]` so consumer trimmers preserve the bindable
  properties of `Compositor`-derived types.
