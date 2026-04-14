## PurelySharp current refactoring status

### Current state

- Full analyzer suite is green: `591/591` tests in `PurelySharp.Test` on .NET 8.
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
  - immutable queue/stack and cryptographic-randomness regression coverage has been expanded
  - `TimeProvider.System` and `TimeZoneInfo.Local` are now treated as impure environment-sensitive sources
  - `DateTime.Today` is now treated as an impure environment-sensitive source
  - `CultureInfo.CurrentUICulture` is now treated as an impure environment-sensitive source
  - `Environment.UserName` is now treated as an impure environment-sensitive source
  - `Environment.UserDomainName` is now treated as an impure environment-sensitive source
  - `Environment.CurrentManagedThreadId` is now treated as an impure environment-sensitive source
  - `CultureInfo.InstalledUICulture` is now treated as an impure environment-sensitive source
  - `Environment.Is64BitOperatingSystem` is now treated as an impure environment-sensitive source
  - `Environment.Is64BitProcess` is now treated as an impure environment-sensitive source
  - `Environment.UserInteractive` is now treated as an impure environment-sensitive source
  - `Environment.SystemPageSize` is now treated as an impure environment-sensitive source
  - `Environment.WorkingSet` is now treated as an impure environment-sensitive source
  - `Environment.ProcessPath` is now treated as an impure environment-sensitive source
  - `Environment.Version` is now treated as an impure environment-sensitive source
  - `Environment.CommandLine` now has direct regression coverage for its existing environment-sensitive impurity catalog entry
  - `Environment.CurrentDirectory` now has direct regression coverage for its existing environment-sensitive impurity catalog entry
  - `CultureInfo.DefaultThreadCurrentCulture` is now treated as an impure environment-sensitive source
  - `CultureInfo.DefaultThreadCurrentUICulture` is now treated as an impure environment-sensitive source
  - `Environment.HasShutdownStarted` is now treated as an impure environment-sensitive source
  - `Environment.ExitCode` is now treated as an impure environment-sensitive source
  - `Random.Shared` is now treated as an impure environment-sensitive source
  - `Console.Out` is now treated as an impure IO source
  - `Console.Error` is now treated as an impure IO source
  - `Console.In` is now treated as an impure IO source
  - `Console.BackgroundColor` is now treated as an impure console-state source
  - `Console.ForegroundColor` is now treated as an impure console-state source
  - `Console.BufferWidth` is now treated as an impure console-state source
  - `Console.CapsLock` is now treated as an impure console-state source
  - `Console.NumberLock` is now treated as an impure console-state source
  - `Console.InputEncoding` is now treated as an impure console-state source
  - `Console.OutputEncoding` is now treated as an impure console-state source
  - `Console.LargestWindowHeight` is now treated as an impure console-state source
  - `Console.LargestWindowWidth` is now treated as an impure console-state source
  - `Console.TreatControlCAsInput` is now treated as an impure console-state source
  - `Console.BufferHeight` now has direct regression coverage for its existing console-state impurity catalog entry
  - `Console.Title` now has direct regression coverage for its existing console-state impurity catalog entry
  - `Console.WindowWidth` is now treated as an impure console-state source
  - `Console.WindowHeight` is now treated as an impure console-state source
  - `Console.CursorLeft` is now treated as an impure console-state source
  - `Console.CursorTop` is now treated as an impure console-state source
  - `Console.KeyAvailable` is now treated as an impure console-state source
  - `Console.WindowLeft` is now treated as an impure console-state source
  - `Console.WindowTop` is now treated as an impure console-state source
  - `Console.CursorVisible` is now treated as an impure console-state source
  - `Console.CursorSize` is now treated as an impure console-state source
  - `Console.IsOutputRedirected` is now treated as an impure console-state source
  - `Console.IsErrorRedirected` is now treated as an impure console-state source
  - `Console.IsInputRedirected` is now treated as an impure console-state source

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
