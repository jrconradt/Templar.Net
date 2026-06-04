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

## The `.tpl` accessor generator

`TemplateAccessorGenerator` (in `src/Templar.Generators/`) turns each `.tpl`
file you place under a project-relative `Templates/` folder into a strongly
typed `Compositor` subclass, so you bind template variables through named C#
properties instead of string keys. It is the file-on-disk analog of the
class-driven generator pattern above — you write the template text once and the
generator **compiles** it into the class: a `RenderInto` method built from your
template, not a `Structure` string parsed at runtime.

### What it generates

For `Templates/Card.tpl`:

```
Hello {{ who }} and {{ place }}
```

the generator emits, into the `<RootNamespace>.Templates` namespace, a class whose
`RenderInto` is the template compiled to straight-line sink calls:

```csharp
#nullable enable

namespace App.Templates;

[global::System.CodeDom.Compiler.GeneratedCode("Templar.Generators", "1.0.0")]
public sealed class Card : global::Templar.Rendering.Compositor
{
    public required object? Who { get; init; }
    public required object? Place { get; init; }

    public override void RenderInto(global::Templar.Rendering.TemplarWriter w)
    {
        w.Compiled(Steps(w));
    }

    private global::System.Collections.Generic.IEnumerator<global::Templar.Rendering.IComposable> Steps(global::Templar.Rendering.TemplarWriter w)
    {
        w.Literal("Hello ");
        {
            var __c = w.Value(Who);
            if (__c is not null) { yield return __c; }
        }
        w.Literal(" and ");
        {
            var __c = w.Value(Place);
            if (__c is not null) { yield return __c; }
        }
        yield break;
    }
}
```

Each distinct placeholder becomes a `required object?` property named in PascalCase,
referenced directly by the compiled `RenderInto` — so a renamed or missing
placeholder is a compile error rather than a silent empty render. Conditional
variables (`{{? flag }}`) become properties too. A template with no placeholders
emits the class with none. Because the generated class carries no `Structure`
string and is never parsed at runtime, its `.tpl` — unlike a hand-written
`Compositor`'s embedded template — does **not** need to be an `EmbeddedResource`;
listing it as `AdditionalFiles` (so the generator can read it at compile time) is
enough.

Compiled accessors apply the four built-in filters (`upper`/`lower`/`pascal`/`camel`);
custom runtime-registered filters are a `Template`/`TemplateSet` feature and are not
available in a compiled accessor.

### Folder layout drives the namespace

The folder path under `Templates/` extends the namespace; the file's leaf name
(sanitized to a valid identifier) is the class name.

| `.tpl` path                     | Generated type            |
|---------------------------------|---------------------------|
| `Templates/Card.tpl`            | `App.Templates.Card`      |
| `Templates/Widgets/Button.tpl`  | `App.Templates.Widgets.Button` |

A `.tpl` file outside a `Templates/` folder is ignored — the generator only
claims templates under that root.

### Wiring

The generator reads two MSBuild properties through `CompilerVisibleProperty`
(`RootNamespace` for the namespace root and `ProjectDir` to resolve each
template's path relative to the project). Reference the generator as an analyzer
and list the templates as `AdditionalFiles`:

```xml
<ItemGroup>
  <ProjectReference Include="…/Templar.Generators.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>

<ItemGroup>
  <CompilerVisibleProperty Include="RootNamespace" />
  <CompilerVisibleProperty Include="ProjectDir" />
</ItemGroup>

<ItemGroup>
  <AdditionalFiles Include="Templates/**/*.tpl" />
</ItemGroup>
```

### Usage

```csharp
using App.Templates;

string output = new Card
{
    Who   = "World",
    Place = "Templar",
}.Render();
```

The property names are checked by the compiler, so a renamed or missing
placeholder is a build error rather than a silent empty render.

## Zero dependencies

The runtime has no external NuGet references. Drop it into a library that
needs to remain dependency-light — a Roslyn source generator project, a build
task, a CLI — without dragging in a templating ecosystem.
