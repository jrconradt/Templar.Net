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

new Greeting { Name = "<World>" }.Render();
```

renders `Hello, &lt;World&gt;!`.

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
public sealed class TrustedMarkup : UIComponent
{
    public string Trusted { get; init; } = "";
    protected override string Structure => "{{& trusted }}";
}

new TrustedMarkup { Trusted = "<b>bold</b>" }.Render();
```

renders `<b>bold</b>` verbatim.

Use `Markup.Raw("<b>…</b>")` (or a `RawHtml`-typed property) when a value is
already trusted markup. The opt-out is a *type*, not a string flag, so it cannot
be triggered by accident.

Child components (`Compositor`/`Sequence`/`IEnumerable<Compositor>`) are
structural and never escaped — their own rendering already escaped their text.

**Filters** (`{{ x | upper }}`) produce escaped text. To emit raw output from a
computed value, wrap it in `RawHtml` at the component level rather than piping a
filter.

## Elements

`Element` is the generic markup element. The `Markup` factory exposes one method
per HTML tag — `Markup.Div`, `Markup.Span`, `Markup.A`, `Markup.Img`, … — each
returning a configured `Element`. Three layouts drive formatting:

| Layout | Shape |
|--------|-------|
| Block | children on their own indented lines |
| Inline | children inline, one line |
| Void | self-closing, no children |

```csharp
Markup.Div(Markup.Span("hi")).Render();
```

renders the block layout:

```html
<div>
    <span>hi</span>
</div>
```

`Markup.Br().Render()` produces `<br />`, and `Markup.A("Docs").Render()`
produces `<a>Docs</a>`.

Each method takes the children, then optional `classes`, then a raw `attrs`
slot: `Markup.Div(children, classes, attrs)`. Classes compose (see below); other
attributes go through `attrs` or an escaped placeholder in a `.html.tpl`
component.

Nesting preserves indentation at any depth — the whole point of the Templar
engine:

```csharp
Markup.Ul(new[] { Markup.Li("a"), Markup.Li("b") }).Render();
```

renders:

```html
<ul>
    <li>
        a
    </li>
    <li>
        b
    </li>
</ul>
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
Markup.Div("body", "card shadow").Render();
```

renders:

```html
<div class="card shadow">
    body
</div>
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
    DefaultClass = "btn",
    Class = "btn--primary",
    Children = "Save",
}.Render();
```

renders `<button class="btn btn--primary">Save</button>` — `DefaultClass` is the
element's own style and `Class` is the caller's addition.

The default + extras flow through one `ClassList`, so the space-join merges them
— no second `class` attribute, no merge pass. The stock `Markup` elements ship
with *no* default classes (no style opinion); a styled component set sets them in
the element table or in bespoke components.

### Other attributes

Attributes that aren't classes are built with `Attr`, whose value is **escaped
and, for URL attributes, scheme-sanitized**:

```csharp
Markup.Div("x", attrs: new Attr { Name = "id", Value = userId }).Render();

Markup.Div("x", attrs: new[]
{
    new Attr { Name = "id", Value = "main" },
    new Attr { Name = "data-count", Value = count },
}).Render();
```

The second call renders `<div id="main" data-count="…">…</div>`. A bare boolean
attribute is `new Attr { Name = "disabled", Boolean = true }`.

`Attr` is safe by default — see [Security](#security) for exactly what it
rejects and sanitizes. The `attrs` slot also accepts `Markup.Raw(...)` for
trusted verbatim attribute markup; a plain `string` is **rejected** so raw
injection is never accidental.

## Inline content

A children *list* joins with newlines (good for block stacking). For prose with
inline markup — text interspersed with inline elements — use `Markup.Inline`,
which concatenates its parts with no separator. Strings are escaped; elements and
`Markup.Raw` compose as-is.

```csharp
Markup.P(Markup.Inline("Hello ", Markup.Strong("world"), "!")).Render();
```

renders:

```html
<p>
    Hello <strong>world</strong>!
</p>
```

## Whitespace-sensitive elements

`<pre>` and `<textarea>` use the **verbatim** layout: their content is still
HTML-escaped, but it is emitted without the indentation engine reindenting
continuation lines, so significant whitespace survives even when the element is
nested deep.

```csharp
Markup.Div(Markup.Pre("line 1\nline 2")).Render();
```

renders, with the `<pre>` content kept exactly as written:

```html
<div>
    <pre>line 1
line 2</pre>
</div>
```

## The `Document` preset

`Document` is the HTML analog of `CSharpFile` — a five-region page shell.

```csharp
new Document
{
    Lang  = "en",
    Title = "Home",
    Head  = Markup.Link(attrs: Markup.Raw("rel=\"stylesheet\" href=\"/app.css\"")),
    Body  = Markup.Main(Markup.H1("Welcome")),
}.Render();
```

`Title` is escaped; `Head` and `Body` take components (or `RawHtml`). It emits a
doctype, `<html lang>`, a `<head>` with charset/viewport/title, and a `<body>`.

## The `.html.tpl` generator

Markup files with the `.html.tpl` extension become strongly-typed `UIComponent`
subclasses at build time. Placeholders are typed by their marker:

| Placeholder | Generated property type | Rendering |
|-------------|-------------------------|-----------|
| `{{ name }}` | `string` | Escaped text. |
| `{{& name }}` | `RawHtml` | Emitted verbatim, never escaped. |
| `{{> name }}` | `Compositor?` (a child slot) | The slotted component is written **structurally** — its own rendered markup is emitted unescaped, indented to the marker's column, exactly like any nested `Compositor` value. |

The `{{> }}` slot marker shares the unescaped path with `{{& }}`: it never
HTML-escapes the value it writes. The distinction is the property type — `{{& }}`
takes pre-escaped `RawHtml`, while `{{> }}` takes a `Compositor` whose own render
already escaped its text — so the slot is the structural composition point for
child components, not a place to inject a trusted string.

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
    BodyHtml = Markup.Raw("<p>raw</p>"),
    Footer   = Markup.Span("footer"),
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

The `Markup` factory is generated from a compact table by
`ElementFactoryGenerator`. Each line lists tags, a layout, and optional default
class tokens:

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
generated method has the shape `Markup.Tag(children, classes, attrs)` (void
elements drop `children`); attributes flow through `Attr`/`Markup.Raw`, never
promoted parameters.

The element factory (`Markup.Div`, `Markup.Span`, …), the escaping/raw helpers
(`Markup.Escape`, `Markup.Raw`, `Markup.Options`), and the inline combinator
(`Markup.Inline`) are all one surface — the `static partial class Markup` — so
authoring markup goes through a single entry point.

## Security

The toolkit is **safe by default**; the only ways to emit unescaped output are
explicit and named (`Markup.Raw`, the `{{& }}` marker). Concretely:

| Vector | Defense |
|--------|---------|
| Text / attribute-value injection | HTML-escaped (`& < > " '`) by default. |
| Tag-name injection (`Element.Tag`) | Validated; non-`[A-Za-z][A-Za-z0-9-]*` throws `MarkupSecurityException`. |
| Attribute-name injection (`Attr.Name`) | Validated against a safe charset; throws on breakout characters. |
| Event-handler injection (`onclick`, `on*`) | `Attr` rejects `on*` names — a script context can't be made safe by escaping. |
| URL-scheme injection (`href="javascript:…"`) | URL-context attribute values are scheme-checked. `javascript:` and `vbscript:` become `about:invalid#blocked`. A `data:` URI is allowed **only** when its media type is in the image allowlist (`image/png`, `image/jpeg`, `image/gif`, `image/webp`, case-insensitive) and carries a payload separator; every other `data:` URI — `text/html`, `application/javascript`, `image/svg+xml`, a bare `data:,`, or a media-type-only `data:image/png` with no payload — becomes `about:invalid#blocked`. |
| Raw via the attrs slot | A plain `string` in `attrs` is rejected; raw markup must be explicit `Markup.Raw`. |
| `<script>`/`<style>` | Not in the `Markup` factory; raw element content requires an explicit `RawContent` element. `<pre>` content stays escaped. |

Validation failures throw `MarkupSecurityException` (fail closed — no unsafe
output is ever produced).

The `data:` image allowlist permits inline raster images (the common, safe use
of `data:` in `src`/`href`) while still blocking the script-bearing media types:
`text/html` and `application/javascript` execute, and `image/svg+xml` can carry
script, so none of them are allowed.

**Residual responsibilities (yours):**

- **Serve as UTF-8 with an explicit `charset`.** Escaping is UTF-8-correct and
  non-ASCII is left as literal characters (not entity-encoded). Serving the
  output under a different/sniffed charset reopens charset-confusion XSS.
- **`Markup.Raw` / `{{& }}` / `RawContent` are trust assertions.** They bypass
  escaping by design; never feed them untrusted data.

## Interactivity — an explicit non-goal

Templar.UI renders **static, stateless markup**. It does not ship a client
runtime, a virtual DOM, a hydration story, or an event/state model, and it will
not. This keeps the package zero-dependency, trim-safe, AOT-safe, and free of any
"runtime phase" beyond rendering a string.

For interactivity, emit attributes consumed by a client library you already use
— HTMX (`hx-*`), Alpine (`x-*`), or an islands approach — through `Attr`:

```csharp
Markup.Button("Load", attrs: new[]
{
    new Attr { Name = "hx-get", Value = "/rows" },
    new Attr { Name = "hx-target", Value = "#list" },
});
```

The server owns state and renders markup; the client library owns behavior.
Templar.UI's job ends at the string.

## Accessibility

Templar.UI emits the markup you describe and nothing more — it does not add
ARIA roles, labels, or landmarks on your behalf. Accessible output is therefore
an authoring responsibility, and the toolkit's design supports it: `Attr`
accepts any non-`on*` attribute name that passes the safe-charset check, so the
full `aria-*` and `role` vocabulary flows through unchanged, and attribute
values are escaped so an accessible name derived from data stays safe.

Author against [WCAG 2.2](https://www.w3.org/TR/WCAG22/) and the
[WAI-ARIA Authoring Practices](https://www.w3.org/WAI/ARIA/apg/). The points
that bear on server-rendered markup:

- **Prefer native semantics over ARIA.** A real `Markup.Button(...)`,
  `Markup.Nav(...)`, or `Markup.Main(...)` carries its role implicitly; reach for
  `role=` only when no native element fits. The first rule of ARIA is to not use
  ARIA when HTML already says it.
- **Give every control and image an accessible name.** Supply `alt` on
  `Markup.Img(...)` (empty `alt=""` for purely decorative images), and label
  form controls with a `<label>` association or `aria-label` / `aria-labelledby`
  through `Attr`.
- **Set the document language.** `Document` takes `Lang`, which emits
  `<html lang>` — keep it set so assistive tech picks the right pronunciation.
- **Provide a page title and a logical heading order.** `Document.Title` emits
  `<title>`; structure body content with a single `<h1>` and nested headings
  that do not skip levels.
- **Wire ARIA state for interactive widgets.** Because behavior comes from a
  client library (see above), the matching ARIA attributes — `aria-expanded`,
  `aria-controls`, `aria-hidden`, `aria-live` — are attributes you emit through
  `Attr` alongside the `hx-*` / `x-*` hooks.

```csharp
Markup.Nav(
    Markup.Ul(new[]
    {
        Markup.Li(Markup.A("Home", attrs: new Attr { Name = "href", Value = "/" })),
        Markup.Li(Markup.A("Docs", attrs: new Attr { Name = "href", Value = "/docs" })),
    }),
    attrs: new Attr { Name = "aria-label", Value = "Primary" });
```

The toolkit will faithfully emit whatever ARIA you author and escape the values;
verifying the result against WCAG — contrast, focus order, screen-reader
behavior — remains yours.
