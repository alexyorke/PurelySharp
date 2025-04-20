# PurelySharp Analyzer

A Roslyn analyzer designed to help enforce method purity in C# projects.

**Note:** This project is currently under active development and refactoring. The features described below reflect the _current_ state, which is simpler than previous versions.

## Goal

The primary goal of PurelySharp is to provide developers with tools to write and maintain functionally pure C# code by identifying methods that might introduce side effects when they are intended to be pure.

## Current Status & Features

As of now, the analyzer focuses on identifying methods marked for purity enforcement that require analysis:

1.  **Attribute Recognition:** The analyzer recognizes methods marked with the `[PurelySharp.Attributes.EnforcePure]` attribute.
2.  **Implementation Check:** It checks if a method marked with `[EnforcePure]` has an actual implementation (a method body `{...}` or an expression body `=> ...`). Abstract or partial method definitions without implementation are ignored.
3.  **Diagnostic Reporting:** If a method is marked with `[EnforcePure]` and has an implementation, the analyzer reports diagnostic **PS0002: Purity Not Verified**.

**What PS0002 Means:**

- This diagnostic does **not** mean the method is impure.
- It signifies that the method is _intended_ to be pure (due to `[EnforcePure]`) and _has code that needs checking_, but the detailed analysis rules to verify its purity (e.g., checking for I/O, state modification, impure calls) **have not been implemented yet**.
- It serves as a placeholder indicating that purity analysis is required for this method.

**Features NOT Currently Implemented (but planned):**

- Detailed analysis of method bodies to detect specific impurities (I/O, static field mutations, mutable object modifications, calls to impure methods, etc.).
- Code fixes for reported diagnostics.
- Analysis based on the standard `[System.Diagnostics.Contracts.Pure]` attribute.
- Configurability of rules.

## Installation

_This analyzer is not yet published._ Once released, installation will likely involve:

1.  **Attributes Package:** Adding a reference to the `PurelySharp.Attributes` NuGet package in projects where you want to use `[EnforcePure]`.
    ```bash
    # Example command (package not yet available)
    dotnet add package PurelySharp.Attributes --version <version>
    ```
2.  **Analyzer Package/VSIX:**
    - Adding the `PurelySharp.Analyzer` NuGet package to your project(s) for build-time analysis.
    - Installing the `PurelySharp.Vsix` extension in Visual Studio for real-time feedback.

## Usage

1.  Add the `PurelySharp.Attributes` package reference to your project.
2.  Add the `[EnforcePure]` attribute (from `PurelySharp.Attributes`) to methods you intend to be functionally pure.

    ```csharp
    using PurelySharp.Attributes;

    public class Calculator
    {
        [EnforcePure]
        public int Add(int a, int b)
        {
            // Purity analysis needed here - PS0002 will be reported currently.
            return a + b;
        }

        [EnforcePure]
        public int GetConstant(); // No implementation, PS0002 NOT reported.
    }
    ```

3.  Observe the `PS0002` diagnostic for methods with implementations marked `[EnforcePure]`.
4.  _(Future)_ As analysis rules are implemented, `PS0002` will be replaced by more specific diagnostics if impurity is found, or suppressed if purity is confirmed.

## Diagnostics

- **PS0002: Purity Not Verified**

  - **Message:** `Method '{0}' marked with [EnforcePure] has implementation, but its purity has not been verified by existing rules`
  - **Severity:** Warning
  - **Meaning:** The method requires purity analysis, but the necessary rules haven't been implemented or run yet.

- **(Defined but Unused) PS0001: Impure Method Assumed**
  - This diagnostic ID is defined but is not currently reported by the core analyzer. It may be used in the future by specific impurity detection rules.

## Contributing

Contributions are welcome! Please feel free to open issues or submit pull requests.

## License

(Assume MIT License if not specified otherwise - You should confirm/add the actual license)

This project is licensed under the MIT License - see the LICENSE file for details.

## Supported Language Features

Supported means there is _some_ level of test coverage. It does not mean it is 100% supported.

### Expressions

- [x] Literal expressions (numbers, strings, etc.)
- [x] `nameof` and `typeof` expressions (compile-time resolved)
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
- [x] Bit shift operations including unsigned right shift (`>>>`)
- [x] Async/await expressions
- [x] Unsafe code blocks
- [x] Pointer operations

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
- [x] Fixed statements

### Collections and Data Structures

- [x] Immutable collections (System.Collections.Immutable)
- [x] Read-only collections (IReadOnly\* interfaces)
- [x] Arrays (when used in a read-only manner)
- [x] Tuples
- [~] Collection expressions (C# 12) - _Partial support: only when creating immutable collections_
- [x] Mutable collections (List, Dictionary, etc.)
- [x] Modifying collection elements
- [x] Inline arrays (C# 12)

### Method Types

- [x] Regular methods
- [x] Expression-bodied methods
- [x] Extension methods (if pure)
- [x] Local functions (if pure)
- [x] Abstract methods
- [x] Recursive methods (if pure)
- [x] Virtual/override methods (if pure)
- [x] Generic methods
- [x] Async methods
- [x] Iterator methods (yield return)
- [x] Unsafe methods
- [x] Operator overloads (including checked operators)
- [x] User-defined conversions
- [x] Static abstract/virtual interface members (C# 11)

### Type Declarations

- [x] Classes (when methods are pure)
- [x] Interfaces (methods are considered pure by default)
- [~] Structs (when methods are pure) - _Partial support: only for immutable structs_
- [x] Records (C# 9)
- [x] Record structs (C# 10)
- [x] Enums
- [ ] Delegates
- [x] File-local types (C# 11)
- [x] Primary constructors (C# 12)

### Member Declarations

- [x] Instance methods
- [x] Static methods
- [x] Constructors
- [x] Properties (get-only)
- [x] Auto-properties (get-only or init-only)
- [x] Fields (readonly)
- [x] Events
- [x] Indexers
- [x] Required members (C# 11)
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
- [x] Ref readonly parameters (C# 12)

### Special Features

- [x] LINQ methods (all considered pure)
- [x] String operations (all considered pure)
- [x] Math operations (all considered pure)
- [x] Tuple operations
- [x] Conversion methods (Parse, TryParse, etc.)
- [x] I/O operations (File, Console, etc.)
- [x] Network operations
- [x] Threading/Task operations
- [x] Random number generation
- [x] Event subscription/invocation
- [x] Delegate invocation

### Advanced Language Features

- [x] Pattern matching
- [x] Switch expressions
- [x] List patterns (C# 11)
- [ ] Top-level statements (C# 9)
- [x] File-scoped namespaces (C# 10)
<!-- Implementation note: The analyzer implicitly supports file-scoped namespaces as it analyzes syntax nodes regardless of namespace declaration style. -->
- [x] Required members (C# 11)
- [ ] Nullable reference types annotations (C# 8.0+)
- [ ] Caller information attributes
- [ ] Source generators
- [ ] Partial classes/methods
- [ ] Global using directives (C# 10)
- [x] Generic attributes (C# 11)
- [ ] Type alias for any type (C# 12)
- [ ] Experimental attribute (C# 12)
- [ ] Interceptors (C# 12 preview)
- [ ] Lock object (C# 13)
- [ ] Overload resolution priority (C# 13)

### C# 11 Specific Features

- [x] Extended nameof scope
- [x] Numeric IntPtr (nint/nuint)
- [x] Generic attributes
- [x] Unsigned right-shift operator (>>>)
- [x] Checked user-defined operators
- [x] Raw string literals
- [x] UTF-8 string literals
- [x] List patterns
- [x] File-local types
- [x] Required members
- [x] Auto-default structs
- [x] Pattern match Span<char> on constant string
- [x] Newlines in string interpolation expressions
- [x] ref fields and scoped ref
- [x] Generic math support (static virtual/abstract interface members)

### C# 12 Specific Features

- [~] Collection expressions - _Partial support: only when creating immutable collections_
- [x] Primary constructors
- [x] Inline arrays
- [ ] Optional parameters in lambda expressions
- [x] ref readonly parameters
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
- [x] Mutable fields
- [x] Properties with setters
- [x] Static mutable fields
- [x] Event fields
- [x] Volatile fields (both reads and writes are considered impure due to their special memory ordering semantics and thread safety implications)

### Generic and Advanced Constructs

- [x] Generic methods
- [ ] Generic type parameters with constraints
- [ ] Covariance and contravariance
- [x] Reflection
- [x] Dynamic typing
- [x] Unsafe code

## Enum Operations

PurelySharp treats enums as pure data types. The following operations with enums are considered pure:

- Accessing enum values
- Converting enums to their underlying numeric type
- Comparing enum values
- Using methods from the `Enum` class

Note that the analyzer includes special handling for `Enum.TryParse<T>()` to treat it as a pure method despite using an `out` parameter.

### Examples

```csharp
public enum Status
{
    Pending,
    Active,
    Completed,
    Failed
}

public class EnumOperations
{
    [EnforcePure]
    public bool IsActiveOrPending(Status status)
    {
        return status == Status.Active || status == Status.Pending;
    }

    [EnforcePure]
    public int GetStatusCode(Status status)
    {
        return (int)status;
    }

    [EnforcePure]
    public bool ParseStatus(string value, out Status status)
    {
        return Enum.TryParse(value, out status);
    }

    [EnforcePure]
    public Status GetStatusFromValue(int value)
    {
        return (Status)value;
    }
}
```

## Delegate Operations

PurelySharp supports delegate types and operations. The purity analysis for delegates focuses on both the creation of delegates and their invocation:

### Delegate Purity Rules

- **Delegate Type Definitions**: Defining delegate types is always pure.
- **Delegate Creation**:
  - Creating a delegate from a pure method is pure.
  - Creating a lambda expression is pure if the lambda body is pure and it doesn't capture impure state.
  - Creating an anonymous method is pure if its body is pure and it doesn't capture impure state.
- **Delegate Invocation**:
  - Invoking a delegate is pure if the delegate target is pure and all arguments are pure.
  - If the analyzer can't determine the purity of the delegate target, it conservatively marks the invocation as impure.
- **Delegate Combination**:
  - Combining delegates (`+=`, `+`) is pure if both delegate operands are pure.
  - Removing delegates (`-=`, `-`) is pure if both delegate operands are pure.

### Examples

```csharp
// Define delegate types (always pure)
public delegate int Calculator(int x, int y);
public delegate void Logger(string message);

public class DelegateOperations
{
    // Pure delegate field
    private readonly Func<int, int, int> _adder = (x, y) => x + y;

    [EnforcePure]
    public int Add(int x, int y)
    {
        // Creating and invoking a pure lambda delegate
        Calculator calc = (a, b) => a + b;
        return calc(x, y);
    }

    [EnforcePure]
    public IEnumerable<int> ProcessNumbers(IEnumerable<int> numbers)
    {
        // Using delegates with LINQ (pure)
        return numbers.Where(n => n > 0)
                     .Select(n => n * 2);
    }

    [EnforcePure]
    public Func<int, int> GetMultiplier(int factor)
    {
        // Higher-order function returning a pure delegate
        return x => x * factor;
    }

    [EnforcePure]
    public int CombineDelegates(int x, int y)
    {
        // Combining pure delegates
        Func<int, int> doubler = n => n * 2;
        Func<int, int> incrementer = n => n + 1;

        // Combined delegate is pure if components are pure
        Func<int, int> combined = n => incrementer(doubler(n));

        return combined(x) + combined(y);
    }

    // This would generate a diagnostic
    [EnforcePure]
    public void ImpureDelegateExample()
    {
        int counter = 0;

        // Impure delegate - captures and modifies a local variable
        Action incrementCounter = () => { counter++; };

        // Invoking impure delegate
        incrementCounter(); // Analyzer will flag this
    }
}
```

Note that delegate invocations are analyzed conservatively. If the analyzer cannot determine the purity of a delegate, it will mark the invocation as impure.

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
- Reading or writing volatile fields
- Using Interlocked operations
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

## Examples

### Pure Method Example

The analyzer ensures that methods marked with `[EnforcePure]` don't contain impure operations:

```csharp
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public int PureHelperMethod(int x)
    {
        return x * 2; // Pure operation
    }

    [EnforcePure]
    public int TestMethod(int x)
    {
        // Call to pure method is also pure
        return PureHelperMethod(x) + 5;
    }
}
```

### Impure Method Examples

The analyzer detects impure operations and reports diagnostics:

#### Modifying State

```csharp
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private int _state;

    [EnforcePure]
    public int TestMethod(int value)
    {
        _state++; // Impure operation: modifies class state
        return _state;
    }
}

// Analyzer Error: PMA0001 - Method 'TestMethod' is marked as pure but contains impure operations
```

#### I/O Operations

```csharp
using System;
using System.IO;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod(string path)
    {
        File.WriteAllText(path, "test"); // Impure operation: performs I/O
    }
}

// Analyzer Error: PMA0001 - Method 'TestMethod' is marked as pure but contains impure operations
```

#### Console Output

```csharp
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public int TestMethod()
    {
        Console.WriteLine("Hello World"); // Impure operation: console output
        return 42;
    }
}

// Analyzer Error: PMA0001 - Method 'TestMethod' is marked as pure but contains impure operations
```

#### Static Field Access

```csharp
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private static string sharedState = "";

    [EnforcePure]
    public string TestMethod()
    {
        // Reading from static field is considered impure
        return sharedState;
    }
}

// Analyzer Error: PMA0001 - Method 'TestMethod' is marked as pure but contains impure operations
```

#### Volatile Field Access

```csharp
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private volatile int _counter;

    [EnforcePure]
    public int GetCounter()
    {
        // Both reading and writing to volatile fields is considered impure
        // due to their special memory ordering semantics
        return _counter;
    }

    [EnforcePure]
    public void UpdateCounter(int value)
    {
        _counter = value; // Writing to volatile field is impure
    }
}

// Analyzer Error: PMA0001 - Method 'GetCounter' is marked as pure but contains impure operations
// Analyzer Error: PMA0001 - Method 'UpdateCounter' is marked as pure but contains impure operations
```

#### Thread Synchronization with Volatile Fields

```csharp
using System;
using System.Threading;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Method)]
public class AllowSynchronizationAttribute : Attribute { }

public class TestClass
{
    private volatile bool _initialized;
    private readonly object _lock = new object();

    [EnforcePure]
    [AllowSynchronization] // Even with AllowSynchronization, volatile read is impure
    public void EnsureInitialized()
    {
        if (!_initialized) // Reading volatile field is impure
        {
            lock (_lock)
            {
                if (!_initialized) // Reading volatile field again is impure
                {
                    Initialize();
                    _initialized = true; // Writing to volatile field is impure
                }
            }
        }
    }

    private void Initialize() { /* ... */ }
}

// Analyzer Error: PMA0001 - Method 'EnsureInitialized' is marked as pure but contains impure operations
```

#### Atomic Operations with Interlocked

```csharp
using System;
using System.Threading;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    private volatile int _counter;

    [EnforcePure]
    public int IncrementAndGet()
    {
        // Using Interlocked with volatile fields is impure
        // since it modifies shared state in a thread-safe manner
        return Interlocked.Increment(ref _counter);
    }

    [EnforcePure]
    public int CompareExchange(int newValue, int comparand)
    {
        // All Interlocked operations are impure
        return Interlocked.CompareExchange(ref _counter, newValue, comparand);
    }
}

// Analyzer Error: PMA0001 - Method 'IncrementAndGet' is marked as pure but contains impure operations
// Analyzer Error: PMA0001 - Method 'CompareExchange' is marked as pure but contains impure operations
```

### More Complex Examples

#### LINQ Operations (Pure)

LINQ operations are generally considered pure as they work on immutable views of data:

```csharp
using System;
using System.Linq;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public IEnumerable<int> FilterAndTransform(IEnumerable<int> numbers)
    {
        // LINQ operations are pure
        return numbers
            .Where(n => n > 10)
            .Select(n => n * 2)
            .OrderBy(n => n);
    }
}

// No analyzer errors - method is pure
```

#### Iterator Methods (Pure)

Iterator methods using `yield return` can be pure:

```csharp
using System;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public IEnumerable<int> GenerateFibonacciSequence(int count)
    {
        int a = 0, b = 1;

        for (int i = 0; i < count; i++)
        {
            yield return a;
            (a, b) = (b, a + b); // Tuple deconstruction for swapping
        }
    }
}

// No analyzer errors - method is pure
```

#### Lock Statements with AllowSynchronization

Lock statements are allowed in pure methods when using the `[AllowSynchronization]` attribute:

```csharp
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method)]
public class AllowSynchronizationAttribute : Attribute { }

public class TestClass
{
    private readonly object _lockObj = new object();
    private readonly Dictionary<string, int> _cache = new Dictionary<string, int>();

    [EnforcePure]
    [AllowSynchronization]
    public int GetOrComputeValue(string key, Func<string, int> computeFunc)
    {
        lock (_lockObj) // Normally impure, but allowed with [AllowSynchronization]
        {
            if (_cache.TryGetValue(key, out int value))
                return value;

            // Compute is allowed if the function is pure
            int newValue = computeFunc(key);
            _cache[key] = newValue; // This would be impure without [AllowSynchronization]
            return newValue;
        }
    }
}

// No analyzer errors with [AllowSynchronization]
```

#### Switch Expressions and Pattern Matching

Modern C# pattern matching and switch expressions are supported:

```csharp
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class Shape { }
public class Circle : Shape { public double Radius { get; } }
public class Rectangle : Shape { public double Width { get; } public double Height { get; } }

public class TestClass
{
    [EnforcePure]
    public double CalculateArea(Shape shape)
    {
        // Switch expression with pattern matching
        return shape switch
        {
            Circle c => Math.PI * c.Radius * c.Radius,
            Rectangle r => r.Width * r.Height,
            _ => throw new ArgumentException("Unknown shape type")
        };
    }
}

// No analyzer errors - method is pure
```

#### Working with Immutable Collections

Methods that use immutable collections remain pure:

```csharp
using System;
using System.Collections.Immutable;
using System.Linq;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public ImmutableDictionary<string, int> AddToCounters(
        ImmutableDictionary<string, int> counters,
        string key)
    {
        // Working with immutable collections preserves purity
        if (counters.TryGetValue(key, out int currentCount))
            return counters.SetItem(key, currentCount + 1);
        else
            return counters.Add(key, 1);

        // Note: The original collection is not modified
    }
}

// No analyzer errors - method is pure
```

#### Complex Method with Multiple Pure Operations

Complex methods combining multiple pure operations:

```csharp
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public (double Average, int Min, int Max, ImmutableList<int> Filtered) AnalyzeData(
        IEnumerable<int> data, int threshold)
    {
        // Local function (must also be pure)
        bool IsOutlier(int value) => value < 0 || value > 1000;

        // Use LINQ to process the data
        var filteredData = data
            .Where(x => !IsOutlier(x) && x >= threshold)
            .ToImmutableList();

        if (!filteredData.Any())
            throw new ArgumentException("No valid data points after filtering");

        // Multiple computations on the filtered data
        var average = filteredData.Average();
        var min = filteredData.Min();
        var max = filteredData.Max();

        // Return a tuple with the results
        return (Average: average, Min: min, Max: max, Filtered: filteredData);
    }
}

// No analyzer errors - method is pure
```

### Limitations and Edge Cases

The following examples demonstrate cases where the analyzer may fail to correctly identify impure operations:

#### Indirect Method Impurity

```

```

## Constructor Analysis

When applying `[EnforcePure]` to constructors, the analyzer applies special rules to account for the unique purpose of constructors. A constructor marked as pure must follow these rules:

1. **Instance field/property assignment is permitted**: Unlike regular methods, constructors can assign values to instance fields and properties of the containing type.

2. **Static field modification is not permitted**: Modifying static fields is still considered impure, as this affects state beyond the instance being constructed.

3. **Impure method calls are not permitted**: Calling impure methods (like I/O operations) from a pure constructor is not allowed.

4. **Collection initialization is permitted**: Creating and initializing collections (e.g., `new List<int> { 1, 2, 3 }`) is allowed if the collection is assigned to an instance field.

5. **Base constructor calls**: If a constructor calls a base constructor, the base constructor must also be pure.

### Examples

#### Pure Constructor

```csharp
[AttributeUsage(AttributeTargets.Constructor)]
public class EnforcePureAttribute : Attribute { }

public class Person
{
    private readonly string _name;
    private readonly int _age;
    private readonly List<string> _skills;

    [EnforcePure]
    public Person(string name, int age)
    {
        _name = name;
        _age = age;
        _skills = new List<string>(); // Allowed: initializing a collection field
    }
}
```

#### Impure Constructor (Static Field Modification)

```csharp
public class Counter
{
    private static int _instanceCount = 0;
    private readonly int _id;

    [EnforcePure]
    public Counter() // Error: Modifies static state
    {
        _id = ++_instanceCount; // Impure: modifies static field
    }
}
```

#### Impure Constructor (Calling Impure Methods)

```csharp
public class Logger
{
    private readonly string _name;

    [EnforcePure]
    public Logger(string name) // Error: Calls an impure method
    {
        _name = name;
        InitializeLog(); // Calls impure method
    }

    private void InitializeLog()
    {
        Console.WriteLine($"Logger {_name} initialized"); // Impure operation
    }
}
```

## Cross-Framework and Language Version Support
