# Integration

This page covers wiring Templar into a larger system: loading templates from
disk or embedded resources, sharing filters and options across a batch of
templates, handling failures, and the common shape of a code-generation
workflow. For one-off templates see [templates.md](templates.md); for class-as-
template authoring see [composition.md](composition.md).

## `TemplateSet` — a named batch of templates

A `TemplateSet` is a name → template-source dictionary plus a shared filter
table and a shared `RenderOptions`. You add templates by name, then resolve
each one to a fresh `Template` instance with `Get(name)`. Each `Get` returns a
new `Template` parsed from the stored source, with the set's filters and
options copied in.

### Loading paths

| Method                                                          | Source                                                | Name derivation                               |
|-----------------------------------------------------------------|-------------------------------------------------------|-----------------------------------------------|
| `Add(string name, string text)`                                 | In-memory string literal                              | The `name` you pass.                          |
| `AddDirectory(string path, string extension = ".tpl")`          | All `*.tpl` files in `path` (non-recursive)           | File name without extension.                  |
| `AddEmbeddedResource(Assembly asm, string resourceName)`        | One embedded resource in `asm`                        | Last `.X.tpl`-style segment — see below.      |
| `AddEmbeddedTemplates(Assembly asm, string prefix, string extension = ".tpl")` | Every embedded resource in `asm` whose name starts with `prefix` and ends with `extension` | Same rule as `AddEmbeddedResource`. |

All four return `this` for chaining.

### Embedded resource naming

`AddEmbeddedResource` derives the lookup name by stripping the extension and
the prefix up to the second-to-last dot. So a resource called
`MyAsm.Templates.Greeting.tpl` lands as `Greeting`. Names are case-insensitive.

```csharp
var set = new TemplateSet()
    .AddEmbeddedTemplates(typeof(MyType).Assembly, prefix: "MyAsm.Templates.");

set.Get("Greeting");   // looks up "Greeting", case-insensitive
```

### Resolving templates

```csharp
var set = new TemplateSet().AddDirectory("Templates");
if (set.Contains("Calculator"))
{
    var t = set.Get("Calculator");
    t["name"] = "BasicCalculator";
    var output = t.Render();
}
```

`Get` throws `KeyNotFoundException` if the name is not registered. Use
`Contains(name)` to test first when the call is conditional.

## Filters and options propagate

Filters added to the **set** are copied into every template the set produces.
The same for `RenderOptions`. Configure once, share across everything you
render through the set.

```csharp
var set = new TemplateSet()
    .Add("hello", "{{ who | shout }}")
    .AddFilter("shout", v => (v?.ToString() ?? "").ToUpperInvariant() + "!")
    .WithOptions(new RenderOptions { Newline = "\r\n" });

var t = set.Get("hello");
t["who"] = "team";
t.Render();   // → "TEAM!"
```

Templates returned by `Get` are independent instances. Adding a filter to a
returned template does **not** retroactively register it on the set or affect
other templates.

## Exception model

Three exception types are thrown across the Templar surface:

| Exception                    | When                                                                    | Useful members                                |
|------------------------------|-------------------------------------------------------------------------|-----------------------------------------------|
| `TemplateParseException`     | `Template.Parse` (or `Compositor.Render` on first instance of a type) sees malformed template text. | `Line`, `Column`                              |
| `TemplateRenderException`    | `Render()` references an unknown filter (and others).                   | `FilterName`, `VariableName`, `TemplateName?` |
| `KeyNotFoundException`       | `TemplateSet.Get(name)` for a name that wasn't added.                   | Standard BCL exception.                       |

Catch the Templar-specific exceptions to surface template-source diagnostics
to whatever build pipeline you're wiring into.

## Patterns

### One-off render

For a single template instance:

```csharp
var t = Template.Parse(source);
t["x"] = value;
var output = t.Render();
```

### Bulk render from a directory

```csharp
var set = new TemplateSet().AddDirectory("Templates");

foreach (var name in templateNames)
{
    var t = set.Get(name);
    t["data"] = dataFor(name);
    File.WriteAllText($"out/{name}.cs", t.Render());
}
```

### Embedded resources in a library

When Templar is consumed by a library that wants to ship templates inside its
own assembly, mark `.tpl` files as embedded resources in the `.csproj`:

```xml
<ItemGroup>
  <EmbeddedResource Include="Templates\*.tpl" />
</ItemGroup>
```

Then load them by prefix:

```csharp
var set = new TemplateSet()
    .AddEmbeddedTemplates(typeof(MyLibraryType).Assembly,
                          prefix: "MyLibrary.Templates.");
```

### Class-driven generators

When the generator emits many files with the same shape, define a `Compositor`
subclass per file shape (see [composition.md](composition.md)) — the
per-type parse cache means each subclass parses its `Structure` exactly once
for the life of the process, regardless of how many instances you render.

```csharp
foreach (var record in records)
{
    File.WriteAllText(
        $"Generated/{record.Name}.cs",
        new RecordFile
        {
            Namespace = "Acme.Generated",
            TypeName  = record.Name,
        }.Render());
}
```

### Custom filters

Filters are `Func<object?, string>`. Register them where the rendered output
needs them:

- On a single `Template` when the filter is one-off.
- On a `TemplateSet` when many templates share the same vocabulary.

The four built-in filters (`upper`, `lower`, `pascal`, `camel`) can be
overridden by registering a same-named filter at the template or set level.

### Newline + indent control

`RenderOptions` is the configuration knob. Set it on the `TemplateSet` so
every `Get` inherits, or on a `Compositor` via `WithOptions`, or on a
`Template` via `WithOptions`. Whichever level you set, the renderer honors
the configured newline end-to-end (literal text, multi-line value injection,
and the join used for `IEnumerable<string>` values).

## Zero dependencies

The runtime has no external NuGet references. Drop it into a library that
needs to remain dependency-light — a Roslyn source generator project, a build
task, a CLI — without dragging in a templating ecosystem.
