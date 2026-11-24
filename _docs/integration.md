# Integration

This page covers template names, how the generator is wired, which (type, template)
pairs it compiles and what it emits, its diagnostics, what happens at run time, and
using Templar inside a source generator. For the model itself see
[composition.md](composition.md).

## Template names

Templates live under `<ProjectDir>/Templates/`, listed as `AdditionalFiles`. A
template's name is its path after `Templates/`, without `.tpl`, with `/` between
folders:

| File | Name |
|------|------|
| `Templates/Card.tpl` | `Card` |
| `Templates/Orders/Requisite.tpl` | `Orders/Requisite` |
| `Templates/Injection/Constructor.tpl` | `Injection/Constructor` |

Names are compared exactly (case-sensitive). The name is what `[Template("…")]` and
`Compositor.ResolveTemplate(obj, "…")` refer to, and it is searched only among the
templates of the project that declares the `[Template]` or makes the call: a
`[Template]` on a type of a referenced assembly names a template of that assembly
([Pairs](#pairs)). A template's name says nothing about the types it
renders: those live wherever their code lives, in any namespace. A `.tpl` listed as
`AdditionalFiles` outside `Templates/` is a compile-time error (`TMPLR003`).

## Wiring

### From the package

`Templar.Net` carries the generator (`analyzers/dotnet/cs/Templar.Generators.dll`)
and build targets that expose `RootNamespace` and `ProjectDir` to it and add
`Templar.Generated`, the namespace of the interceptors it emits, to
`InterceptorsNamespaces`. List the templates as `AdditionalFiles` — one line in the
`.csproj`:

```xml
<ItemGroup>
  <AdditionalFiles Include="Templates/**/*.tpl" />
</ItemGroup>
```

The templates are read at compile time and are not embedded as files. The text of each
template one of the project's `[Template]` types names is written into the assembly as
an attribute, so that a project referencing it can render those types ([Pairs](#pairs)).

### From source

Referencing the projects directly, wire what the package's targets do:

```xml
<ItemGroup>
  <ProjectReference Include="…/Templar.csproj" />
  <ProjectReference Include="…/Templar.Generators.csproj"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false"
                    PrivateAssets="all" />
  <AdditionalFiles Include="Templates/**/*.tpl" />
  <CompilerVisibleProperty Include="RootNamespace" />
  <CompilerVisibleProperty Include="ProjectDir" />
</ItemGroup>

<PropertyGroup>
  <InterceptorsNamespaces>$(InterceptorsNamespaces);Templar.Generated</InterceptorsNamespaces>
</PropertyGroup>
```

The `InterceptorsNamespaces` entry lets the compiler accept the interceptors generated
for the project's `ResolveTemplate` calls; without it they are compile errors, and a
project with no such call does not need it. The generator has no reference to
`Templar.dll`.

## Pairs

The generator compiles (type, template) pairs, starting from:

- every class or interface in the project with `[Template("Name")]` — the pair (type,
  `Name`);
- every `Compositor.ResolveTemplate(obj, "Name")` call — the pair (static type of
  `obj`, `Name`); the name must be a constant;
- every `Compositor.ResolveTemplate(obj)` call — the static type of `obj`, resolved
  through its hierarchy like a spot.

From each pair it follows the spots: a spot whose property's declared type (or item
type, for a list) has a `[Template]` in its hierarchy reaches the pair of every type
collected for it — the type itself if concrete and, when it is declared in the project
or in a referenced assembly that carries template text, its concrete subtypes of the
project and, for the latter, of that assembly — each with the most-derived `[Template]`
in its chain
([composition.md](composition.md#the-hierarchy)). Each pair is checked once — every spot names a readable
property, and every property's type can render — and errors are reported at the site
that first reached the pair: the call site, the `[Template]` attribute, or the spot,
with the template line in the message.

A type whose `[Template]` is declared in a referenced assembly pairs with that
assembly's template. The assembly carries the text of each template one of its
`[Template]` types names; the generator reads that text by name, among that assembly's
templates only, compiles the pair into the project's own renderer, and checks it like
any other pair. Its spots reach further pairs the same way, in that assembly or in any
assembly its types come from. A type whose `[Template]` is declared in an assembly that
carries no text for its template cannot render here (`TMPLR008`).

A template no pair reaches is still parsed and checked (`TMPLR002`, `TMPLR003`) but is
not compiled in.

## What it generates

For `Templates/Orders/Requisite.tpl` and `Templates/Orders/Line.tpl` in project `App`:

```
requisite {{ name }}
{
    {{ lines }}
}
```

```csharp
namespace App.Orders;

public sealed class Requisite
{
    public required string Name { get; init; }
    public required Sequence<Line> Lines { get; init; }
}

[Template("Orders/Line")]
public sealed record Line(string Sku);

// somewhere in the project
Compositor.ResolveTemplate(requisite, "Orders/Requisite");
```

the generator emits `Orders.Requisite.g.cs`:

```csharp
// <auto-generated/>
#nullable enable

namespace App.Templates.Orders;

internal static class RequisiteTemplate
{
    internal static global::System.Collections.Generic.IEnumerator<object?> Steps(global::Templar.Rendering.TemplarWriter w, global::App.Orders.Requisite model)
    {
        w.Literal("requisite ");
        w.Text(model.Name);
        w.Literal("\n{\n    ");
        yield return w.List(model.Lines, global::App.Templates.Orders.LineTemplate.Steps);
        w.Literal("\n}");
        yield break;
    }
}
```

and `Orders.Line.g.cs`, whose `LineTemplate.Steps` renders a `Line`.

Because `Line` names `Orders/Line` with `[Template]`, the generator also emits
`Templar.Templates.g.cs`, which writes that template's text into the assembly:

```csharp
// <auto-generated/>
#nullable enable

[assembly: global::Templar.Rendering.TemplateTextAttribute("Orders/Line", "…")]
```

It holds one attribute per template a `[Template]` type of the project names, the
second argument being the template's text; a template only a `ResolveTemplate` call
names is not written.

One class per reached template, in `<RootNamespace>.Templates.<folders>`, named
`<file>Template`; one `Steps` overload per type it renders. A referenced assembly's
template compiles into a class of its own, in
`<RootNamespace>.Templates.<assembly name>.<folders>`, named
`<assembly name>_<folders>_<file>Template` (every `.` and `/` as `_`), in the file
`<assembly name>.<folders>.<file>.g.cs`, apart from the project's template of the same
name. The generated renderers and resolvers are `internal`.
Each spot is one write, chosen from the property's declared type
([composition.md](composition.md#how-a-spot-is-written)): `w.Text` for a string,
`w.Format` for a primitive or enum, `w.Compose(value, …Template.Steps)` for a type
with `[Template]` (or the declared type's resolver, below), and `w.List(value, …)` for
a list. The generated renderers hold no `if`, no loop, and no other conditional; `null`
and empty lists are handled inside `TemplarWriter`.

The call site is resolved in place. Alongside the renderers, the generator emits one
`Templar.Interceptors.g.cs` per project, holding the file-local
`InterceptsLocationAttribute` declaration the compiler needs and, for the call above:

```csharp
namespace Templar.Generated
{
    file static class Interceptors
    {
        [global::System.Runtime.CompilerServices.InterceptsLocation(1, "…")]
        internal static string ResolveTemplate0(global::App.Orders.Requisite model, string template, global::Templar.Rendering.RenderOptions? options = null)
        {
            return global::Templar.Rendering.TemplarWriter.Render(model, global::App.Templates.Orders.RequisiteTemplate.Steps, options);
        }
    }
}
```

Every `ResolveTemplate` call is intercepted this way: the compiler replaces the call
with a method that runs that pair's renderer directly. The location comes from
`SemanticModel.GetInterceptableLocation`. Calls that resolve to the same pair through
the same overload share one method, with one `InterceptsLocation` per call.

A spot, list item or `ResolveTemplate(obj)` whose declared type resolves to several
templates ([composition.md](composition.md#the-hierarchy)) calls a resolver instead of a
renderer. The generator emits every resolver in one `Templar.Resolvers.g.cs`, in
`<RootNamespace>.Templates.Resolvers`, one class per declared type, named from its
qualified name. For a `Sequence<ILine>` whose values are `Stocked` or `Backordered`,
each with its own `[Template]`, in namespace `App.Orders`:

```csharp
// <auto-generated/>
#nullable enable

namespace App.Templates.Resolvers;

internal static class App_Orders_ILineResolver
{
    internal static global::System.Collections.Generic.IEnumerator<object?> Steps(global::Templar.Rendering.TemplarWriter w, global::App.Orders.ILine model)
    {
        return model switch
        {
            global::App.Orders.Backordered m => global::App.Templates.Orders.BackorderedTemplate.Steps(w, m),
            global::App.Orders.Stocked m => global::App.Templates.Orders.StockedTemplate.Steps(w, m),
            _ => throw new global::System.InvalidOperationException("Type '" + model.GetType() + "' was not resolved for 'App.Orders.ILine' at compile time."),
        };
    }
}
```

and the spot is written `yield return w.List(model.Lines, global::App.Templates.Resolvers.App_Orders_ILineResolver.Steps);`.
The resolver holds the type switch and nothing else: one arm per type collected for the
declared type ([Pairs](#pairs)), each a direct call to that type's renderer, and a last
arm that throws for a value no arm matches. A `ResolveTemplate(obj)` call on such a
declared type is intercepted by a method whose body is
`TemplarWriter.Render(model, <Resolver>.Steps, options)`. When the declared type has
only one resolution, no resolver is emitted and the write calls the pair's `Steps`
directly.

## Diagnostics

Every template, `[Template]`, call site and reached pair is checked at compile time.
Each diagnostic is an error in category `Templar`:

| Id | Reported when | Located at |
|----|---------------|------------|
| `TMPLR002` | The template is malformed: an unclosed tag, a `\|` inside a tag, a tag that opens with `{{?`, `{{&` or `{{>`, or a spot that is not a property name. Nothing is compiled from it. | The template line |
| `TMPLR003` | The template is outside `<ProjectDir>/Templates/`, so it has no name. | The template |
| `TMPLR004` | A `[Template]` or a `ResolveTemplate` call names a template that is not declared under `Templates/`. | The attribute or the call's argument |
| `TMPLR005` | The template name passed to `ResolveTemplate` is not a constant. | The call's argument |
| `TMPLR006` | A spot names no accessible, readable instance property of the type being rendered. | The site that reached the pair; the message names the template line |
| `TMPLR007` | A spot matches two properties differing only in case. | As `TMPLR006` |
| `TMPLR008` | A type cannot render: a spot's property is not a string, primitive, enum, list or type with a `[Template]` in its hierarchy; a spot's type, or the type of `ResolveTemplate(obj)`, has a `[Template]` declared in an assembly that carries no text for its template; `ResolveTemplate(obj)` on a type with no `[Template]` in its hierarchy; a concrete type a value of the declared type can be has no `[Template]` in its chain ("its subtype 'X' has no [Template]"); or a type that generated code cannot name (a value type, an open generic, an anonymous or inaccessible type), including a generic type marked `[Template]`. | The spot's pair site, the call, or the attribute |

A spot that fails `TMPLR006`–`TMPLR008` is left out of the renderer, which is still
emitted, so the build reports the template error alone.

## Run time

Every `Compositor.ResolveTemplate` call is intercepted and drives its pair's compiled
renderer directly; nested writes call the compiled renderers directly too, and a value
of a declared type with several resolutions goes through that type's resolver, a type
switch compiled with the project. No template is parsed, no `.tpl` is read, and
nothing is looked up. `Compositor.ResolveTemplate`'s
own bodies only throw `InvalidOperationException`, saying the call was not resolved at
compile time; that happens only in a project the Templar generator does not run on.

## Inside a source generator

A source generator can render its output with `Compositor.ResolveTemplate` in its
pipeline. The generator project references `Templar.Net` (which compiles its
templates) and ships `Templar.dll` beside itself to the compiler, so the analyzer
loader can resolve it:

```xml
<ItemGroup>
  <PackageReference Include="Templar.Net" PrivateAssets="all" />
  <AdditionalFiles Include="Templates/**/*.tpl" />
</ItemGroup>

<PropertyGroup>
  <GetTargetPathDependsOn>$(GetTargetPathDependsOn);GetDependencyTargetPaths</GetTargetPathDependsOn>
</PropertyGroup>

<Target Name="GetDependencyTargetPaths" DependsOnTargets="ResolveReferences">
  <ItemGroup>
    <TargetPathWithTargetPlatformMoniker Include="@(ReferencePath->WithMetadataValue('FileName', 'Templar'))"
                                         IncludeRuntimeDependency="false" />
  </ItemGroup>
</Target>
```

A consuming project references the generator with `OutputItemType="Analyzer"
ReferenceOutputAssembly="false"`. `fixtures/Fixture.Generator` and
`fixtures/Fixture.Consumer` in this repository build exactly that: the generator
renders an `Order` by its `[Template]` (its `Sequence<ILine>` mixes `Stocked` and
`Backordered` lines, each by its own template through a resolver), a `ConstantClass`
record by a named template,
and the file around it by its own `SourceFile` template, and `FixtureConsumerTests`
asserts the constant the consumer receives.

## Exception model

| Exception | When | Message |
|-----------|------|---------|
| `InvalidOperationException` | A `Compositor.ResolveTemplate` call was not resolved at compile time: the Templar generator does not run on the calling project. | Says the call was not resolved at compile time. |
| `InvalidOperationException` | A resolver received a value no arm matches: its type was not collected for the declared type (for example an implementation of a public interface in an assembly other than the project and the interface's own). | Names the value's type and the declared type. |

## Zero dependencies

The runtime has no external NuGet references. Drop it into a library that needs to
remain dependency-light — a Roslyn source generator project, a build task, a CLI —
without dragging in a templating ecosystem.
