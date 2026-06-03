# Render-pattern inversion → compiled accessor templates

## Goal

Make it easy to author a `.tpl` and have the generator emit a **compiled** `Compositor`
whose `Render` is straight-line code that writes directly into a shared sink — instead of
embedding the template text and re-parsing/re-rendering it through the interpreter at
runtime.

This is the accessor-class style (template file in, typed class out), but with the body
**compiled to code** rather than interpreted from an embedded resource.

## The reframe (why this is not a thin wrapper)

Three behaviors in `src/Rendering/Renderer.cs` are computed from runtime *values*, not from
template text, so they cannot be compiled away into straight `result += "..."`:

1. **Indentation reflow** (`WriteValueString`, ~line 141): a multi-line value's injected
   column is `output.Length - lineStart`, computed live. Two placeholders on one line make
   the second's column depend on the first value's width.
2. **Line-swallowing** (`EmitNewline`, ~line 61): a newline is retroactively deleted
   (`output = output[..lineStart]`) when a line held an expression but rendered
   whitespace-only.
3. **Nested `Compositor` / `Sequence` / `IEnumerable<Compositor>` values** (~lines 524–598):
   pushed onto an explicit `Stack<object>`. Compiling nesting to direct method calls between
   generated classes would be cross-instance recursion — banned, and an overflow risk on deep
   runtime data.

So "compile the template" means: precompile the *parse*, but keep an emitted writer runtime
that carries the irreducible state machine. The cleanest substrate for that is to **invert
the render pattern** first.

## The inversion

The engine is already push-into-shared-state internally: `output` and `currentIndent` are
locals in `Renderer.Render` shared across every frame on the stack. A nested `Compositor`
value is not rendered by calling its `Render()`; the engine calls `Compile()` to get its
`Structure` text and re-scans it inline. The only string-return boundaries are the public
`Compositor.Render()` and the internal `Compile()` hand-off.

Inverting = hoist those shared locals into a real sink object and flip the contract from
"hand me your template text, I'll scan it" to "here's the sink, write yourself into it."

### `TemplarWriter` sink

Owns exactly what `Renderer.Render` keeps in locals today:

- the output accumulator (keep the existing `string +=` accumulation — not a perf rewrite)
- `currentIndent`
- the line-swallow triple: `lineStart`, `atLineStart`, `lineHadExpression`
- the work stack

Methods are the existing local functions relocated verbatim: `EnsureIndent`,
`WriteValueString`, `EmitNewline`, `BuildIndent`, `IsTruthy`, `WriteVerbatim`,
`WriteLiteralChar`. No logic change — closure-locals become instance state.

### Flipped contract

Add `Compositor.RenderInto(TemplarWriter writer)`.

- **Default** `RenderInto` is today's behavior: interpret my `Structure` into the sink (the
  current `Renderer.Render` loop, driving the passed-in sink instead of local state).
- `Render()` collapses to `{ var w = new TemplarWriter(options); RenderInto(w); return w.ToString(); }`.
- Compiled accessor classes later **override** `RenderInto` with straight-line sink calls and
  carry no `Structure` string.

The interpreted default and a compiled override drive the *same* sink, so they are provably
consistent — the existing `tests/` suite is the conformance net.

## Phases

### Phase 0 — invert the engine (behavior-preserving refactor)

No new features. Pure relocation.

- New `src/Rendering/TemplarWriter.cs`: the sink type above.
- Rewrite `src/Rendering/Renderer.cs` so the loop drives a `TemplarWriter` instance instead
  of method-local `output` / `currentIndent` / swallow state. The `Stack<object>` of
  `ScanFrame` / `SequenceFrame` moves onto the writer (or stays in the loop driving the
  writer's accumulator — whichever keeps the diff smallest while the locals become writer
  state).
- Add `Compositor.RenderInto(TemplarWriter)`; default = interpret `Structure`. `Render()`
  becomes the thin wrapper.
- Keep `Compile()` for the interpreted default path.

**Gate:** entire existing `dotnet test Templar.slnx` suite green, unchanged. Any test edit is
a signal the refactor changed behavior — stop and fix the refactor, not the test.

### Phase 1 — compiled emitter, linear templates

Targets `RenderInto`, no nested-compositor placeholders yet.

- New parse-to-nodes step (the codebase has no AST — parsing is fused into the renderer).
  Produce a flat node list: `Literal(text)`, `Expr(name, filter?, raw?)`, `If(cond, negated,
  then[], else[])`, `Comment` (dropped at emit). Iterative scan, modeled on the existing
  `PlaceholderScanner` / `HtmlComponentGenerator.Scan`.
- Code emitter in `src/Templar.Generators/`: walk nodes → emit a `RenderInto` body of
  straight-line sink calls. Literals → `w.WriteLiteral("...")`; comments → dropped (zero
  runtime cost); escapes → resolved to literal text; conditionals → `if (w.Truthy(X))
  {...} else {...}`; built-in filters → inlined or emitted helper; `{{ var }}` →
  `w.WriteValue(Var)`.
- The generated class overrides `RenderInto`, drops `Structure`, and no longer needs the
  `.tpl` wired as an `EmbeddedResource` (only `AdditionalFiles` for compile-time scan).
- Constraint honored: generator depends on Roslyn only — no `Templar.dll` reference
  (CS8785). The sink contract it emits against is shipped by the runtime the *consumer*
  references, not by the analyzer.

**Gate:** new generator-driver tests under `tests/Templar.UI.Generators.Tests/`-style harness:
a compiled linear template and the equivalent interpreted template produce byte-identical
output across the existing fixture cases.

### Phase 2 — nested compositors (the segment machine)

The irreducible cost. When a compiled `RenderInto` hits a nested compositor *value*, it must
preserve "before / child / after" ordering **and** not call the child's `RenderInto` on the
call stack.

- A linear template compiles to one straight-line `RenderInto` — no stack interaction.
- A template with nested-compositor placeholders compiles to a **resumable segment machine**:
  split the body at each nested boundary; emit segments that push their continuation + the
  child onto the sink's work stack, drained by the sink's loop. This mirrors how the
  interpreter pushes "remaining scan" + child today.
- `Sequence` / `IEnumerable<Compositor>` values flow through the same mechanism.

**Gate:** deep-nesting and sequence fixtures (mirroring `IterativeEngineTests`) match the
interpreter byte-for-byte, including a depth that would overflow a naive recursive emit.

## Open decisions

- **Custom runtime filters.** The engine supports `AddFilter(name, func)` registered at
  runtime; a compiled `RenderInto` only knows filters present at compile time. Either compiled
  mode supports built-ins only, or filter registration moves to compile time. Decide when
  Phase 1 first emits a `| filter` expression. Surface, do not silently narrow.
- **Opaque multi-line string values** still get reflowed by `WriteValueString` — that logic
  lives in the sink and is shared by both paths; no compiled special-casing.

## Constraints (standing)

- Generator assembly: Roslyn-only, no runtime DLL (CS8785 must stay structurally impossible).
- All recursion iterative — emitter walk and segment machine included.
- No `StringBuilder`; keep `string +=` / `string.Join` / `List<string>` + `Concat`.
- Zero code comments anywhere, including emitted output.
- No placeholders / stubs to coerce a green build; red between turns is fine.
</content>
</invoke>
