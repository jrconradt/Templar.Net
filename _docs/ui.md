# Templar.UI

`Templar.UI` is a server-rendered markup component toolkit built on the Templar
engine. Components render to **correctly-indented HTML/XML/SVG** strings, with
HTML-escaping applied by default. It reuses Templar wholesale — `Compositor` is
the component, `Sequence` is list rendering, `{{? }}` is conditional rendering,
and the indentation engine keeps nested markup formatted at any depth. It adds
exactly one new semantic on top: **automatic escaping**.

It is zero-dependency, trim-safe, and AOT-safe, like Templar itself.

For the underlying engine see [syntax.md](syntax.md), [templates.md](templates.md),
[composition.md](composition.md), and [integration.md](integration.md).

## The component model

A component is a `UIComponent` — a `Compositor` whose text props are escaped by
default. Define `Structure` and add `{ get; init; }` properties; they auto-bind
to template variables by name (case-insensitive), exactly as in `Compositor`.

```csharp
using Templar.UI;
using Templar.Rendering;

public sealed class Greeting : UIComponent
{
    public string Name { get; init; } = "";
    protected override string Structure => "Hello, {{ name }}!";
}

new Greeting { Name = "<World>" }.Render();   // → "Hello, &lt;World&gt;!"
```

`UIComponent` also exposes a `Children` slot (`object?`) bound to `{{ children }}`,
so a component can wrap arbitrary content:

```csharp
public sealed class Panel : UIComponent
{
    protected override string Structure => """
        <section class="panel">
            {{ children }}
        </section>
        """;
}
```

## Auto-escaping

When a `UIComponent` renders, interpolated **text** is HTML-escaped
(`& < > " '`). This is the one behavioral difference from raw Templar, and it is
bulletproof: even if you replace the render options with
`WithOptions(...)`, escaping is re-applied unless you set your own `Escape`
function. There are two explicit opt-outs, both deliberate:

| Form | Meaning |
|------|---------|
| `{{ x }}` | Escaped text (the safe default). |
| `{{& x }}` | **Raw** — emitted verbatim, never escaped. |
| `RawHtml` value | A typed value that is always emitted verbatim. |

```csharp
public sealed class Markup : UIComponent
{
    public string Trusted { get; init; } = "";
    protected override string Structure => "{{& trusted }}";
}

new Markup { Trusted = "<b>bold</b>" }.Render();   // → "<b>bold</b>"
```

Use `Html.Raw("<b>…</b>")` (or a `RawHtml`-typed property) when a value is
already trusted markup. The opt-out is a *type*, not a string flag, so it cannot
be triggered by accident.

Child components (`Compositor`/`Sequence`/`IEnumerable<Compositor>`) are
structural and never escaped — their own rendering already escaped their text.

**Filters** (`{{ x | upper }}`) produce escaped text. To emit raw output from a
computed value, wrap it in `RawHtml` at the component level rather than piping a
filter.

## Elements

`Element` is the generic markup element. The `H` factory exposes one method per
HTML tag — `H.Div`, `H.Span`, `H.A`, `H.Img`, … — each returning a configured
`Element`. Three layouts drive formatting:

| Layout | Shape |
|--------|-------|
| Block | children on their own indented lines |
| Inline | children inline, one line |
| Void | self-closing, no children |

```csharp
H.Div(H.Span("hi")).Render();
// <div>
//     <span>hi</span>
// </div>

H.Br().Render();              // <br />
H.A("Docs").Render();         // <a>Docs</a>
```

Each method takes the children, then optional `classes`, then a raw `attrs`
slot: `H.Div(children, classes, attrs)`. Classes compose (see below); other
attributes go through `attrs` or an escaped placeholder in a `.html.tpl`
component.

Nesting preserves indentation at any depth — the whole point of the Templar
engine:

```csharp
H.Ul(new[] { H.Li("a"), H.Li("b") }).Render();
// <ul>
//     <li>
//         a
//     </li>
//     <li>
//         b
//     </li>
// </ul>
```

The element set is **generated from a data table**, `src/Templar.UI/Elements.elements`,
not hand-written. See [The element DSL](#the-element-dsl) below.

## Styling — classes as composed micro-templates

There is no imperative attribute builder. **A style is a micro-template that
contributes class tokens**, and an element's classes are a `ClassList` — a
`Sequence` joined by a single space. Composition *is* the merge: the join folds
fragments into one `class="…"` attribute.

The `classes` argument adds tokens; an empty class list emits no `class`
attribute at all.

```csharp
H.Div("body", "card shadow").Render();
// <div class="card shadow">
//     body
// </div>
```

A `classes` value can be a token string, a style fragment (`Cls` or any
`Compositor` that renders tokens), or a sequence of them. Token values are
escaped, so data-derived classes are safe.

### Default styles compose, they don't replace

An element carries a `DefaultClass`; the caller's classes are appended, never
substituted. This is how a predefined/themed element ships a default look that a
caller extends:

```csharp
new Element
{
    Tag = "button",
    Layout = ElementLayout.Inline,
    DefaultClass = "btn",       // the element's own style
    Class = "btn--primary",     // the caller's addition
    Children = "Save",
}.Render();
// <button class="btn btn--primary">Save</button>
```

The default + extras flow through one `ClassList`, so the space-join merges them
— no second `class` attribute, no merge pass. The stock `H` elements ship with
*no* default classes (no style opinion); a styled component set sets them in the
element table or in bespoke components.

### Other attributes

Attributes that aren't classes are built with `Attr`, whose value is **escaped
and, for URL attributes, scheme-sanitized**:

```csharp
H.Div("x", attrs: new Attr { Name = "id", Value = userId }).Render();

H.Div("x", attrs: new[]
{
    new Attr { Name = "id", Value = "main" },
    new Attr { Name = "data-count", Value = count },
}).Render();
// <div id="main" data-count="…">…</div>

new Attr { Name = "disabled", Boolean = true };   // bare boolean attribute
```

`Attr` is safe by default — see [Security](#security) for exactly what it
rejects and sanitizes. The `attrs` slot also accepts `Html.Raw(...)` for trusted
verbatim attribute markup; a plain `string` is **rejected** so raw injection is
never accidental.

## Inline content

A children *list* joins with newlines (good for block stacking). For prose with
inline markup — text interspersed with inline elements — use `H.Inline`, which
concatenates its parts with no separator. Strings are escaped; elements and
`Html.Raw` compose as-is.

```csharp
H.P(H.Inline("Hello ", H.Strong("world"), "!")).Render();
// <p>
//     Hello <strong>world</strong>!
// </p>
```

## Whitespace-sensitive elements

`<pre>` and `<textarea>` use the **verbatim** layout: their content is still
HTML-escaped, but it is emitted without the indentation engine reindenting
continuation lines, so significant whitespace survives even when the element is
nested deep.

```csharp
H.Div(H.Pre("line 1\nline 2")).Render();
// <div>
//     <pre>line 1
// line 2</pre>
// </div>
```

## The `Document` preset

`Document` is the HTML analog of `CSharpFile` — a five-region page shell.

```csharp
new Document
{
    Lang  = "en",
    Title = "Home",
    Head  = H.Link(attrs: Html.Raw("rel=\"stylesheet\" href=\"/app.css\"")),
    Body  = H.Main(H.H1("Welcome")),
}.Render();
```

`Title` is escaped; `Head` and `Body` take components (or `RawHtml`). It emits a
doctype, `<html lang>`, a `<head>` with charset/viewport/title, and a `<body>`.

## The `.html.tpl` generator

Markup files with the `.html.tpl` extension become strongly-typed `UIComponent`
subclasses at build time. Placeholders are typed by their marker:

| Placeholder | Generated property type |
|-------------|-------------------------|
| `{{ name }}` | `string` (escaped) |
| `{{& name }}` | `RawHtml` (verbatim) |
| `{{> name }}` | `Compositor?` (a child slot) |

`Components/Card.html.tpl`:

```html
<article class="card">
    <h2>{{ title }}</h2>
    {{& bodyHtml }}
    {{> footer }}
</article>
```

generates `Card : UIComponent` with `string Title`, `RawHtml BodyHtml`, and
`Compositor? Footer`:

```csharp
new Card
{
    Title    = "Hi <there>",
    BodyHtml = Html.Raw("<p>raw</p>"),
    Footer   = H.Span("footer"),
}.Render();
```

Wire it up by referencing the generator and listing the templates:

```xml
<ProjectReference Include="…/Templar.UI.Generators.csproj"
                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
<AdditionalFiles Include="Components/*.html.tpl" />
<CompilerVisibleProperty Include="RootNamespace" />
```

## The element DSL

The `H` factory is generated from a compact table by `ElementFactoryGenerator`.
Each line lists tags, a layout, and optional default class tokens:

```
div section article header footer nav main aside : block
span strong em code a : inline
br hr wbr img input : void
```

The optional third field is the element's `DefaultClass` — the tokens it wears
by default, composed with (not replaced by) the caller's `classes`. Leaving it
off ships an unstyled element. The `verbatim` layout marks whitespace-sensitive
elements (`pre`, `textarea`). Editing the table regenerates the factory; there
is no hand-maintained element list and no per-tag method written by hand. Every
generated method has the shape `H.Tag(children, classes, attrs)` (void elements
drop `children`); attributes flow through `Attr`/`Html.Raw`, never promoted
parameters.

## Security

The toolkit is **safe by default**; the only ways to emit unescaped output are
explicit and named (`Html.Raw`, the `{{& }}` marker). Concretely:

| Vector | Defense |
|--------|---------|
| Text / attribute-value injection | HTML-escaped (`& < > " '`) by default. |
| Tag-name injection (`Element.Tag`) | Validated; non-`[A-Za-z][A-Za-z0-9-]*` throws `MarkupSecurityException`. |
| Attribute-name injection (`Attr.Name`) | Validated against a safe charset; throws on breakout characters. |
| Event-handler injection (`onclick`, `on*`) | `Attr` rejects `on*` names — a script context can't be made safe by escaping. |
| URL-scheme injection (`href="javascript:…"`) | URL-context attribute values are scheme-checked; `javascript:`/`vbscript:`/`data:` (incl. control-char-obfuscated) become `about:invalid#blocked`. |
| Raw via the attrs slot | A plain `string` in `attrs` is rejected; raw markup must be explicit `Html.Raw`. |
| `<script>`/`<style>` | Not in the `H` factory; raw element content requires an explicit `RawContent` element. `<pre>` content stays escaped. |

Validation failures throw `MarkupSecurityException` (fail closed — no unsafe
output is ever produced).

**Residual responsibilities (yours):**

- **Serve as UTF-8 with an explicit `charset`.** Escaping is UTF-8-correct and
  non-ASCII is left as literal characters (not entity-encoded). Serving the
  output under a different/sniffed charset reopens charset-confusion XSS.
- **`Html.Raw` / `{{& }}` / `RawContent` are trust assertions.** They bypass
  escaping by design; never feed them untrusted data.

## Interactivity — an explicit non-goal

Templar.UI renders **static, stateless markup**. It does not ship a client
runtime, a virtual DOM, a hydration story, or an event/state model, and it will
not. This keeps the package zero-dependency, trim-safe, AOT-safe, and free of any
"runtime phase" beyond rendering a string.

For interactivity, emit attributes consumed by a client library you already use
— HTMX (`hx-*`), Alpine (`x-*`), or an islands approach — through `Attr`:

```csharp
H.Button("Load", attrs: new[]
{
    new Attr { Name = "hx-get", Value = "/rows" },
    new Attr { Name = "hx-target", Value = "#list" },
});
```

The server owns state and renders markup; the client library owns behavior.
Templar.UI's job ends at the string.
