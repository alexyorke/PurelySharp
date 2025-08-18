## PurelySharp Refactoring Status and Next Steps

### Where we are now

- **Dataflow-first architecture**
  - Added `Engine/Analysis/CallGraph*` to build a call graph from Roslyn `IOperation` trees (invocations + method group references).
  - Added `Engine/Analysis/WorklistPuritySolver` to run a worklist-style fixed-point over the call graph, propagating changes to callers via reverse edges.
  - Introduced `Engine/CompilationPurityService` to own per-compilation state (call graph, fixed-point cache) and serve purity queries thread-safely.

- **Centralized impurity/purity catalog**
  - Introduced `Engine/ImpurityCatalog` and routed helper checks in `PurityAnalysisEngine` through it.
  - Existing catalog content remains in `Engine/Constants.cs` (known impure members/namespaces/types and known pure BCL members).
  - Kept one explicit rule for `System.Text.Json.JsonSerializer.Deserialize*` (tests rely on it). This can be moved to the catalog once tests reflect the catalog behavior.

- **Rule modularization**
  - Centralized rule registration via `Engine/Rules/RuleRegistry`.
  - `PurityAnalysisEngine` now consumes an immutable list of rules from the registry.

- **Analyzer initialization**
  - Moved to a `CompilationStartAction` in `PurelySharpAnalyzer` and pass a shared `CompilationPurityService` into `MethodPurityAnalyzer`.
  - `AttributePlacementAnalyzer` remains a separate focused check.

- **Tests/build**
  - Full suite passing: 482/482 tests (Release). Verified on Windows PowerShell.

### What’s next (prioritized)

1. **Configurable catalogs (.editorconfig)**
   - DONE: Wired `Configuration/AnalyzerConfiguration` into `ImpurityCatalog` to allow overrides via AnalyzerConfig options:
     - `purelysharp_known_impure_methods`
     - `purelysharp_known_pure_methods`
     - `purelysharp_known_impure_namespaces`
     - `purelysharp_known_impure_types`
   - DONE: Parse values from `AnalyzerConfigOptionsProvider` (comma/semicolon-separated lists) and merge with `Constants` via `ImpurityCatalog.InitializeOverrides`.

2. **Call-graph coverage improvements**
   - DONE: Include edges for method group references, delegate creations, and anonymous functions.
   - DONE: Map delegate targets assigned/initialized to locals/fields and connect delegate `Invoke` to captured targets.
   - DONE: Handle `await` flows conservatively by adding edges for awaited invocations.
   - TODO: Consider using Roslyn `ControlFlowGraph` to resolve additional potential targets where simple symbol extraction is insufficient.

3. **Catalog consolidation**
   - PENDING: Migrate the explicit `JsonSerializer.Deserialize*` impurity check into `Constants.KnownImpureMethods` (or config) after aligning tests.
   - Add entries for common framework APIs (I/O, threading, environment) as needed; prefer signatures of `OriginalDefinition`.

4. **Rule set evolution**
   - Group rules by construct families (Invocation/Assignment/Flow/Patterns) for clarity.
   - Expand rules for newer C# features as needed (records, slices/spans patterns, primary constructors).

5. **Performance/robustness**
   - Cache call graph per compilation and consider incremental rebuild hooks (if analyzer context exposes them) for larger solutions.
   - Guard against pathological graphs (cycle limits already present in solver; review thresholds).

6. **Diagnostics/configuration**
   - Allow severity toggles per rule or per pattern via `.editorconfig`.
   - Add optional verbose logging toggle (kept no-op in Release builds today).

### Relevant files and ownership

- `PurelySharp.Analyzer/PurelySharpAnalyzer.cs` — analyzer entry; compilation start; action wiring
- `PurelySharp.Analyzer/MethodPurityAnalyzer.cs` — per-method checks; invokes purity service
- `PurelySharp.Analyzer/Engine/CompilationPurityService.cs` — compilation-scoped analysis/caching
- `PurelySharp.Analyzer/Engine/Analysis/CallGraph.cs` — call graph model
- `PurelySharp.Analyzer/Engine/Analysis/CallGraphBuilder.cs` — call graph construction from `IOperation`
- `PurelySharp.Analyzer/Engine/Analysis/WorklistPuritySolver.cs` — fixed-point solver
- `PurelySharp.Analyzer/Engine/ImpurityCatalog.cs` — centralized catalog adapters
- `PurelySharp.Analyzer/Engine/Constants.cs` — known pure/impure defaults
- `PurelySharp.Analyzer/Engine/Rules/RuleRegistry.cs` — rule list
- `PurelySharp.Analyzer/Engine/Rules/*` — modular rules
- `PurelySharp.Analyzer/Configuration/AnalyzerConfiguration.cs` — config reader
- `PurelySharp.Analyzer/Configuration/ConfigKeys.cs` — config keys

### Definition of done for this refactor

- Catalog-driven impurity/purity with .editorconfig overrides merged at compilation start.
- Call graph includes invocations, method groups, lambdas/delegates, and conservative async edges.
- Worklist solver stable and efficient on medium/large solutions.
- Rules structured by construct families; easy to extend with minimal cross-cutting changes.
- All tests passing; new tests added for config-driven behavior and expanded graph coverage.

### How to continue

- Extend call graph for delegate capture and async/await edges; keep tests green.
- Migrate explicit JSON check into catalog once tests reflect it.


