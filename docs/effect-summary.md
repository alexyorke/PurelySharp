# Effect summary tooling

`Tools/PurelySharp.EffectSummary` is the first step toward evidence-based BCL and framework purity summaries.

The goal is to reduce hand-maintained heuristics by summarizing implementation assemblies and then feeding stable effect facts back into the analyzer/catalog pipeline.

## Why assembly summaries first

Building all of `dotnet/runtime` or the unified .NET source tree is useful for source navigation and calibration, but it is not required for the first durable proof layer. Installed implementation assemblies already contain the IL that user code will execute for a given runtime version.

The summary tool can inspect those assemblies directly and emit JSON facts such as:

- method calls
- virtual calls
- object and array allocation
- static and instance field reads
- static and instance field writes
- indirect memory writes through spans, pointers, refs, arrays, or block operations
- throws
- direct thrown exception types for simple IL patterns such as `new SomeException(...); throw`
- P/Invoke, native, internal-call, abstract, and no-IL-body roots

Those facts are intentionally lower-level than `pure` or `impure`. Final purity decisions should be made by a later fixed-point classifier that applies PurelySharp policy profiles to the evidence.

Each method also emits `RootCandidates`, which are explicit review categories derived from low-level effects:

- `pinvoke`
- `runtime_native_or_internal`
- `metadata_only_or_external`
- `global_state_read`
- `global_state_write`
- `object_state_write`
- `caller_visible_memory_write`
- `dynamic_dispatch`
- `throw`
- `unsafe_or_block_memory_write`

These are not final purity verdicts. They are evidence labels used to seed later policy-aware fixed-point classification.

## Root seed policy

Some roots cannot be proven from managed IL alone:

- P/Invoke and OS calls
- native runtime implementation calls
- JIT intrinsics
- reflection and dynamic dispatch
- environment, current culture, time, randomness, process, and filesystem state
- synchronization, threading, volatile, and interlocked state

These should become explicit root seeds with categories and evidence, not broad guesses hidden in analyzer code.

Examples:

- current-culture formatting is environment-dependent unless an invariant or explicitly analyzable provider is supplied
- file writes are impure because the call chain reaches OS/filesystem mutation roots
- span write helpers are impure because their IL writes through caller-provided memory
- reflection remains conservative unless the target is statically resolved and bounded

## Current CLI

Smoke run against the latest installed .NET 8 `System.Private.CoreLib.dll`:

```powershell
dotnet run --project Tools\PurelySharp.EffectSummary -- --framework net8.0 --limit 20
```

Inspect a specific API family such as `System.String.Format`:

```powershell
dotnet run --project Tools\PurelySharp.EffectSummary -- --framework net8.0 --symbol-prefix System.String.Format --limit 20
```

Include same-assembly callees from the matched symbols:

```powershell
dotnet run --project Tools\PurelySharp.EffectSummary -- --framework net8.0 --symbol-prefix System.String.Format --include-callees --max-depth 1 --limit 50
```

Propagate root candidate labels through same-assembly calls:

```powershell
dotnet run --project Tools\PurelySharp.EffectSummary -- --framework net8.0 --symbol-prefix System.String.Format --include-callees --max-depth 2 --transitive-roots --limit 50
```

When transitive roots are enabled, the JSON also includes `TransitiveThrownExceptionTypes`. For example, `System.ArgumentNullException.ThrowIfNull(...)` can surface `System.ArgumentNullException` from its helper callee even when the public guard method does not directly contain the `throw` instruction.

## Analyzer consumption

The analyzer can consume generated exception summaries when the JSON is supplied as an additional file named `PurelySharp.EffectSummary.json` or `*.PurelySharp.EffectSummary.json`.

With `purelysharp_report_exceptions = true`, `PS0010` uses `ThrownExceptionTypes` and `TransitiveThrownExceptionTypes` for matching metadata/library method calls. This extends exception-flow reporting beyond current-compilation source without doing slow live decompilation inside Roslyn analyzer callbacks.

The lookup is exact and evidence-based: summaries are keyed by method symbol strings emitted by this tool, and catch filtering still happens at the source call site when the exception type resolves in the current compilation.

Run against a specific assembly:

```powershell
dotnet run --project Tools\PurelySharp.EffectSummary -- --assembly "C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.28\System.Private.CoreLib.dll" --output artifacts\effect-summary\corelib-net8.json
```

The output schema is versioned and includes the assembly module version ID so generated summaries can be tied to the exact runtime build.

## Next steps

The durable path from here is:

1. Add a fixed-point classifier over the emitted call/effect graph.
2. Add explicit root seed files for native/runtime/OS/environment/reflection/threading categories.
3. Generate checked-in summaries for supported target frameworks.
4. Generate and version checked-in framework summaries for supported target frameworks.
5. Optionally clone `dotnet/runtime` or the unified .NET source tree to map IL summaries back to source files and comments for review.
