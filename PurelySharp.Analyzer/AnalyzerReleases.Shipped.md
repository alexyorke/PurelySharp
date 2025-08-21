## Version 1.0.0

### New Rules

| Rule ID | Category | Severity | Notes |
| ------- | -------- | -------- | ----- |
| PS0002 | Purity | Error | Purity Not Verified: method marked [EnforcePure]/[Pure] contains operations the analyzer cannot prove pure. |
| PS0003 | Usage | Error | Misplaced [EnforcePure]/[Pure] attribute applied to a non-method declaration. |
| PS0004 | Purity | Warning | Missing [EnforcePure] on a method/accessor/ctor that appears pure. |

## Version 0.0.4

### New Rules

| Rule ID | Category | Severity | Notes |
| ------- | -------- | -------- | ----- |
| PS0005 | Usage | Warning | Conflicting purity attributes: a method marked with both [EnforcePure] and [Pure]. |
| PS0006 | Usage | Warning | [AllowSynchronization] used without [EnforcePure]/[Pure] on the method. |
| PS0007 | Usage | Error | Misplaced [AllowSynchronization] attribute applied to a non-method declaration. |
| PS0008 | Usage | Info | Redundant [AllowSynchronization] on method with no synchronization constructs. |

### Enhancements

- Treat range expressions (OperationKind.Range) as pure when both endpoints are pure.
- Treat nameof expressions (OperationKind.NameOf) as pure.
- Consider ArgumentNullException.ThrowIfNull overloads as known pure BCL methods.