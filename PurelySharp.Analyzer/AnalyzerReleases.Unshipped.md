## Unshipped Release

### New Rules

| Rule ID | Category | Severity | Notes |
| ------- | -------- | -------- | ----- |
| PS0009 | Purity | Info | Optional purity diagnostic explanation emitted when `purelysharp_emit_explanations` is enabled. |
| PS0010 | ExceptionFlow | Info | Optional thrown-exception summary emitted when `purelysharp_report_exceptions` is enabled. |

### Enhancements

- `PS0010` can consume generated `PurelySharp.EffectSummary.json` additional files and propagate summarized metadata/library exception types through source callers.
- `PS0010` infers typed `throw;` rethrows from enclosing catch clauses and still suppresses them when an outer catch handles the same exception type.
- `PS0010` reports definite integer/decimal divide-by-zero and modulo-by-zero expressions with compile-time constant zero divisors, excluding floating-point division.
