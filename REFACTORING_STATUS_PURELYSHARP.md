## PurelySharp current refactoring status

### Current state

- Full analyzer suite is green: `537/537` tests in `PurelySharp.Test` on .NET 8.
- The analyzer is operating on the current dataflow-first architecture:
  - compilation-scoped purity service
  - call-graph + worklist solver
  - centralized impurity/purity catalog with `.editorconfig` overrides
  - modular rule registry
- Recent completed work since the earlier `505/505` checkpoint:
  - nested local-function and lambda purity fallback analysis
  - getter-body verification instead of attribute-shape trust
  - explicit `if`/`while`/`for` condition impurity propagation through CFG branch values
  - direct throw-only body reporting without reclassifying guard throws as impure
  - constant-condition CFG pruning plus dead-branch suppression for post-CFG known-impure scans
  - field-initializer delegate target resolution for stored delegate invocation
  - expanded known-pure `Enum.TryParse` overload coverage, including nullable-string forms
  - mutable `System.Collections.Immutable` builder members are no longer treated as pure
  - `System.Collections.Immutable.ImmutableInterlocked` APIs are treated as impure synchronization/mutation helpers
  - collection-expression spread operands are analyzed instead of defaulting to conservative impurity
  - the previously disabled required-members suite is being reintroduced as active smoke coverage in small green slices

### What is already done

- Catalog-driven impurity/purity with analyzer-config overrides is in place.
- Delegate tracking covers locals, flow captures, anonymous functions, delegate creations, and initializer-resolved member targets.
- Property accessor analysis is recursive and no longer trusts `[Pure]` or getter shape alone.
- CFG propagation uses an in-queue set and merged delegate-target exit state aggregation.
- Explicit condition branches are analyzed without tainting compiler-lowered null/coalesce branches.

### Remaining backlog

1. Continue targeted false-positive/false-negative hunting in areas still expected to stay conservative:
   - dynamic dispatch
   - opaque library calls
   - harder virtual/interface cases
   - reflection and environment-sensitive APIs
2. Expand constant-condition pruning only if more real regressions appear outside the current `if`/`while`/`for` coverage.
3. Audit and rename any remaining stale regression names/comments outside the updated suites.
4. Keep `README.md` aligned whenever behavior-level purity assumptions change.

### Working rules for future passes

- Land one behavior theme per commit.
- Add the narrowest regression first, then rerun the touched slice, then rerun the full analyzer suite.
- Prefer member-level catalog additions over broad namespace/type whitelisting.
- Preserve conservative behavior for environment, IO, threading, reflection, and dynamic code unless a bounded proof and regression tests justify a narrower rule.
