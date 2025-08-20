## Unshipped Release

### New Rules

| Rule ID | Category | Severity | Notes |
| ------- | -------- | -------- | ----- |
| PS0005 | Usage | Warning | Conflicting purity attributes: a method marked with both [EnforcePure] and [Pure]. |
| PS0006 | Usage | Warning | [AllowSynchronization] used without [EnforcePure]/[Pure] on the method. |
| PS0007 | Usage | Error | Misplaced [AllowSynchronization] attribute applied to a non-method declaration. |
| PS0008 | Usage | Info | Redundant [AllowSynchronization] on method with no synchronization constructs. |