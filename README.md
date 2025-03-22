# PurelySharp

A C# analyzer that enforces method purity through the `[EnforcePure]` attribute. Methods marked with this attribute must be pure (no side effects, only pure operations).

## Supported Language Features

### Expressions

- [x] Literal expressions (numbers, strings, etc.)
- [x] Identifiers (variables, parameters)
- [x] Method invocations (if the method is pure)
- [x] Member access (properties, fields - if readonly)
- [x] Object creation (for immutable types)
- [x] Tuple expressions and deconstruction
- [x] Switch expressions (C# 8.0+)
- [x] Pattern matching
- [x] Null coalescing operators (`??`, `?.`)
- [x] Interpolated strings
- [x] Stack allocations and Span operations (when used in a read-only manner)
- [x] Indices and ranges (C# 8.0+)
- [ ] Async/await expressions
- [ ] Unsafe code blocks
- [ ] Pointer operations

### Statements

- [x] Local declarations
- [x] Return statements
- [x] Expression statements
- [x] If statements
- [x] Switch statements
- [x] Throw statements
- [x] Try-catch-finally blocks (if all blocks are pure)
- [x] Local functions (if body is pure)
- [x] Using statements
- [x] Using declarations (C# 8.0+)
- [x] Lock statements (when used with `[AllowSynchronization]` attribute and read-only lock objects)
- [x] Yield statements (iterator methods)
- [ ] Fixed statements

### Collections and Data Structures

- [x] Immutable collections (System.Collections.Immutable)
- [x] Read-only collections (IReadOnly\* interfaces)
- [x] Arrays (when used in a read-only manner)
- [x] Tuples
- [~] Collection expressions (C# 12) - _Partial support: only when creating immutable collections_
- [ ] Mutable collections (List, Dictionary, etc.)
- [ ] Modifying collection elements
- [ ] Inline arrays (C# 12)

### Method Types

- [x] Regular methods
- [x] Expression-bodied methods
- [x] Extension methods (if pure)
- [x] Local functions (if pure)
- [x] Abstract methods
- [x] Recursive methods (if pure)
- [x] Virtual/override methods (if pure)
- [x] Generic methods
- [ ] Async methods
- [x] Iterator methods (yield return)
- [ ] Unsafe methods
- [ ] Operator overloads
- [ ] User-defined conversions
- [ ] Static abstract/virtual interface members (C# 11)

### Type Declarations

- [x] Classes (when methods are pure)
- [x] Interfaces (methods are considered pure by default)
- [~] Structs (when methods are pure) - _Partial support: only for immutable structs_
- [x] Records (C# 9)
- [ ] Record structs (C# 10)
- [ ] Enums
- [ ] Delegates
- [ ] File-local types (C# 11)
- [ ] Primary constructors (C# 12)

### Member Declarations

- [x] Instance methods
- [x] Static methods
- [ ] Constructors
- [x] Properties (get-only)
- [x] Auto-properties (get-only or init-only)
- [x] Fields (readonly)
- [ ] Events
- [ ] Indexers
- [ ] Required members (C# 11)
- [ ] Partial properties (C# 13)

### Parameter Types

- [x] Value types
- [x] Reference types (when used in a read-only manner)
- [x] Ref parameters
- [x] Out parameters
- [x] In parameters
- [x] Params arrays
- [ ] Params collections (C# 13)
- [x] Optional parameters
- [ ] Optional parameters in lambda expressions (C# 12)
- [ ] Ref readonly parameters (C# 12)

### Special Features

- [x] LINQ methods (all considered pure)
- [x] String operations (all considered pure)
- [x] Math operations (all considered pure)
- [x] Tuple operations
- [x] Conversion methods (Parse, TryParse, etc.)
- [ ] I/O operations (File, Console, etc.)
- [ ] Network operations
- [ ] Threading/Task operations
- [ ] Random number generation
- [ ] Event subscription/invocation
- [ ] Delegate invocation

### Advanced Language Features

- [x] Pattern matching
- [x] Switch expressions
- [x] List patterns (C# 11)
- [ ] Top-level statements (C# 9)
- [ ] File-scoped namespaces (C# 10)
- [ ] Required members (C# 11)
- [ ] Nullable reference types annotations (C# 8.0+)
- [ ] Caller information attributes
- [ ] Source generators
- [ ] Partial classes/methods
- [ ] Global using directives (C# 10)
- [ ] Generic attributes (C# 11)
- [ ] Type alias for any type (C# 12)
- [ ] Experimental attribute (C# 12)
- [ ] Interceptors (C# 12 preview)
- [ ] Lock object (C# 13)
- [ ] Overload resolution priority (C# 13)

### C# 11 Specific Features

- [x] List patterns (matching against arrays and collections)
- [x] Raw string literals ("""...""")
- [ ] UTF-8 string literals (u8 suffix)
- [x] Newlines in string interpolation expressions
- [ ] Required members
- [ ] File-local types (file access modifier)
- [ ] Auto-default structs
- [ ] Pattern match Span<char> on constant string
- [ ] Extended nameof scope
- [ ] Numeric IntPtr (nint/nuint)
- [ ] ref fields and scoped ref
- [ ] Generic attributes
- [ ] Generic math support (static virtual/abstract interface members)
- [ ] Unsigned right-shift operator (>>>)
- [ ] Checked user-defined operators

### C# 12 Specific Features

- [~] Collection expressions - _Partial support: only when creating immutable collections_
- [ ] Primary constructors
- [ ] Inline arrays
- [ ] Optional parameters in lambda expressions
- [ ] ref readonly parameters
- [ ] Type alias for any type
- [ ] Experimental attribute
- [ ] Interceptors (preview)

### C# 13 Specific Features

- [ ] params collections
- [ ] Lock object
- [x] Escape sequence \e
- [ ] Method group natural type improvements
- [ ] Implicit indexer access in object initializers
- [ ] ref/unsafe in iterators/async
- [ ] ref struct interfaces
- [ ] Overload resolution priority
- [ ] Partial properties
- [ ] field contextual keyword (preview)

### Field/Property Access

- [x] Readonly fields
- [x] Const fields
- [x] Get-only properties
- [x] Init-only properties (C# 9)
- [ ] Mutable fields
- [ ] Properties with setters
- [ ] Static mutable fields
- [ ] Event fields
- [ ] Volatile fields

### Generic and Advanced Constructs

- [x] Generic methods
- [ ] Generic type parameters with constraints
- [ ] Covariance and contravariance
- [ ] Reflection
- [ ] Dynamic typing
- [ ] Unsafe code

## Attributes

- [x] `[EnforcePure]` - Marks a method that should be analyzed for purity
- [x] `[AllowSynchronization]` - Allows lock statements in pure methods when synchronizing on readonly objects

## Impure Namespaces (Always Considered Impure)

- System.IO
- System.Net
- System.Data
- System.Threading
- System.Threading.Tasks
- System.Diagnostics
- System.Security.Cryptography
- System.Runtime.InteropServices

## Impure Types (Always Considered Impure)

- Random
- DateTime
- File
- Console
- Process
- Task
- Thread
- Timer
- WebClient
- HttpClient
- StringBuilder
- Socket
- NetworkStream

## Common Impure Operations

- Modifying fields or properties
- Calling methods with side effects
- I/O operations (file, console, network)
- Async operations
- Locking (thread synchronization)
- Event subscription/raising
- Unsafe code and pointer manipulation
- Creating mutable collections

## Cross-Framework and Language Version Support

- [x] C# 8.0+ language features
- [ ] Different target frameworks (.NET Framework, .NET Core, .NET 5+)
- [ ] Performance considerations for large codebases
