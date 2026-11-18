# Integration

This page covers how a type's template is found and embedded, where the `.tpl`
accessor generator fits and where it stops, string-keyed loading with
`TemplateSet`, options, and failures. For the model itself see
[composition.md](composition.md); for one-off templates see
[templates.md](templates.md).

## Finding a template for a type

A `Compositor` with no `Structure` override loads the embedded resource named
`<FullName>.tpl` from its own assembly (a nested type's `+` becomes `.`),
walking base types until one is found.

The SDK names an `EmbeddedResource` `<RootNamespace>.<folder path>.<file name>`,
each folder a dot-separated segment. In a project whose root namespace is
`Shop`:

| File                        | Default resource name            | Found by type                |
|-----------------------------|----------------------------------|------------------------------|
| `Orders/Requisite.tpl`      | `Shop.Orders.Requisite.tpl`      | `Shop.Orders.Requisite`      |
| `Orders/Lines/Stocked.tpl`  | `Shop.Orders.Lines.Stocked.tpl`  | `Shop.Orders.Lines.Stocked`  |

So a template beside its type is found whenever the type's namespace follows
its folder (the .NET convention, IDE0130) and the file is named for the type.
Embed the templates with one line:

```xml
<ItemGroup>
  <EmbeddedResource Include="**/*.tpl" />
</ItemGroup>
```

Where a type's namespace departs from its folder, name that one file after the
type (the `Update` follows the `Include` that globbed it):

```xml
<ItemGroup>
  <EmbeddedResource Update="Legacy/Invoice.tpl" LogicalName="Shop.Billing.Invoice.tpl" />
</ItemGroup>
```

`DependentUpon` does not do this. The SDK names a resource that depends on a
`.cs` after that file's first class — `Shop.Orders.Requisite`, with the `.tpl`
dropped — and fails the build (`MSB3041`) for a `.tpl` with no `.cs` beside it.

When the lookup misses, `Render` throws `InvalidOperationException` (“…no
embedded template resource was found in the inheritance chain”); compare
`typeof(T).FullName + ".tpl"` with `typeof(T).Assembly.GetManifestResourceNames()`.

Templar's package adds no `EmbeddedResource` items and sets no resource names;
its build targets only expose `RootNamespace` and `ProjectDir` to the accessor
generator.

## The `.tpl` accessor generator

`TemplateAccessorGenerator` (in `src/Templar.Generators/`) goes the other way:
template first. Each `.tpl` listed as `AdditionalFiles` under a
project-relative `Templates/` folder becomes a sealed `Compositor` whose
properties are the template's placeholders and whose `RenderInto` is the
template compiled to sink calls. It is not the compiled form of a model type: a
type you write never becomes an accessor, and an accessor is not a model.

### What it generates

For `Templates/Card.tpl`:

```
Hello {{ who }} and {{ place }}
```

the generator emits, into the `<RootNamespace>.Templates` namespace:

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
            if (__c is not null)
            {
                yield return __c;
            }
        }
        w.Literal(" and ");
        {
            var __c = w.Value(Place);
            if (__c is not null)
            {
                yield return __c;
            }
        }
        yield break;
    }
}
```

Each distinct placeholder becomes a `required object?` property named in
PascalCase. A template with no placeholders emits the class with none. The
`.tpl` is read at compile time, so it need not be an `EmbeddedResource`.

### Limits

| The accessor                                  | So                                                                          |
|-----------------------------------------------|-----------------------------------------------------------------------------|
| Types every hole `required object?`           | The hole says nothing about what composes there. The caller constructs the class and fills every hole by hand. |
| Is `sealed`                                   | No subtypes, so no variants: choosing between accessors is a choice the caller makes in code. |
| Lives at `<RootNamespace>.Templates.<folders>.<Name>` | The full name a model type would have beside the same file under default resource naming. A model type at `Templates/Card.cs` with `Templates/Card.tpl` listed as `AdditionalFiles` is defined twice (`CS0101`). Keep model types and their templates out of `Templates/`. |
| Tests each hole's value at render time        | Every compiled hole carries `if (__c is not null)` to decide whether to descend into the value. |

Use it for a free-standing text with a few holes and no model behind it.

### Malformed templates

A `.tpl` with an unclosed tag, a `|` inside a tag, or a tag that opens with
`{{?`, `{{&`, or `{{>` is reported at compile time as
`TMPLR002` (severity `Error`, category `Templar`) carrying the template's file path
and line. No accessor is emitted for that file, so the build fails with a located
diagnostic instead of crashing the generator or emitting uncompilable C#.

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

## `TemplateSet` — string-keyed templates

For templates with no type behind them. A `TemplateSet` is a name →
template-source dictionary plus a shared `RenderOptions`; the caller picks a
template by name with `Get(name)` and fills its holes by key. Each `Get`
returns a new `Template` parsed from the stored source, with the set's options
copied in.

### Loading paths

| Method                                                          | Source                                                | Name derivation                               |
|-----------------------------------------------------------------|-------------------------------------------------------|-----------------------------------------------|
| `Add(string name, string text)`                                 | In-memory string literal                              | The `name` you pass.                          |
| `AddDirectory(string path, string extension = ".tpl")`          | All `*.tpl` files in `path` (non-recursive)           | File name without extension.                  |
| `AddEmbeddedResource(Assembly asm, string resourceName)`        | One embedded resource in `asm`                        | Last `.X.tpl`-style segment — see below.      |
| `AddEmbeddedTemplates(Assembly asm, string prefix, string extension = ".tpl")` | Every embedded resource in `asm` whose name starts with `prefix` and ends with `extension` | The name between `prefix` and `extension`. |

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
var t = set.Get("Calculator");
t["name"] = "BasicCalculator";
var output = t.Render();
```

`Get` throws `KeyNotFoundException` if the name is not registered;
`Contains(name)` tests membership.

### Options propagate

`RenderOptions` set on the **set** are copied into every template the set
produces.

```csharp
var set = new TemplateSet()
    .Add("hello", "Hello,\n{{ who }}")
    .WithOptions(new RenderOptions { Newline = "\r\n" });

var t = set.Get("hello");
t["who"] = "team";
t.Render();   // → "Hello,\r\nteam"
```

Templates returned by `Get` are independent instances.

## Exception model

| Exception                    | When                                                                    | Useful members                                |
|------------------------------|-------------------------------------------------------------------------|-----------------------------------------------|
| `TemplateParseException`     | `Template.Parse` (or `Compositor.Render` on first instance of a type) sees malformed template text. | `Line`, `Column`                              |
| `InvalidOperationException`  | `Compositor.Render` finds no `<FullName>.tpl` resource for the type or any base type, and `Structure` is not overridden. | Message names the type.                       |
| `KeyNotFoundException`       | `TemplateSet.Get(name)` for a name that wasn't added.                   | Standard BCL exception.                       |

Catch `TemplateParseException` to surface template-source diagnostics
to whatever build pipeline you're wiring into.

## Patterns

### Rendering a model

Build the model, render each root. Each root's type decides its template and
every value under it composes by its own type.

```csharp
foreach (var requisite in requisites)
{
    File.WriteAllText($"Generated/{requisite.Name}.txt", requisite.Render());
}
```

### One-off render

For a single template with no type behind it:

```csharp
var t = Template.Parse(source);
t["x"] = value;
var output = t.Render();
```

### Newline + indent control

`RenderOptions` is the configuration knob. Set it on a `Compositor` via
`WithOptions`, on a `TemplateSet` so every `Get` inherits, or on a `Template`
via `WithOptions`. Whichever level you set, the renderer honors the configured
newline end-to-end (literal text, multi-line value injection, and the join used
for `IEnumerable<string>` values).

## Zero dependencies

The runtime has no external NuGet references. Drop it into a library that
needs to remain dependency-light — a Roslyn source generator project, a build
task, a CLI — without dragging in a templating ecosystem.
