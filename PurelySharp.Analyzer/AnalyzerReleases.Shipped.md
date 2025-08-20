## Version 1.0.0

### New Rules

| Rule ID | Category | Severity | Notes |
| ------- | -------- | -------- | ----- |
| PS0002 | Purity | Error | Purity Not Verified: method marked with [EnforcePure] has implementation but purity was not proven by current rules. |
| PS0003 | Usage | Error | Misplaced [EnforcePure]/[Pure] attribute applied to a non-method declaration. |
| PS0004 | Purity | Warning | Missing [EnforcePure] on a method/accessor/ctor that appears pure. |
