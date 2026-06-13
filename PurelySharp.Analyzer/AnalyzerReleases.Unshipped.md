## Unshipped Release

### New Rules

| Rule ID | Category | Severity | Notes |
| ------- | -------- | -------- | ----- |
| PS0009 | Purity | Info | Optional purity diagnostic explanation emitted when `purelysharp_emit_explanations` is enabled. |
| PS0010 | ExceptionFlow | Info | Optional thrown-exception summary emitted when `purelysharp_report_exceptions` is enabled. |

### Enhancements

- `PS0010` can consume generated `PurelySharp.EffectSummary.json` additional files and propagate summarized metadata/library exception types through source callers.
