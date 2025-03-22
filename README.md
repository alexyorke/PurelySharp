# PurelySharp

A C# analyzer that enforces method purity through the `[EnforcePure]` attribute. Methods marked with this attribute must be pure (no side effects, only pure operations).

## Beta Warning ⚠️

**This analyzer is currently in beta. Some cases may not be properly detected or may produce false positives.**

Known limitations:

- **Indirect Impurities**: The analyzer may not detect impure operations across multiple method calls, especially if those methods are in different assemblies.
- **Static Fields**: Access to static fields from other types/assemblies may not always be correctly identified as impure.
- **External Dependencies**: Methods from third-party libraries without proper annotations might be incorrectly assumed to be pure.
- **Reflection**: Code using reflection to modify state may evade detection.
- **Constructors**: Analysis of constructors is limited and may miss impurities.
- **Thread-Static Fields**: Thread-static field access isn't always properly detected as impure.
- **Collection Modifications**: The analyzer may not detect all collection modifications, especially through indirect method calls.
- **Delegate Invocations**: Delegate invocations to impure methods might not be detected.

For best results, ensure that all called methods in the dependency chain are also marked with `[EnforcePure]` when appropriate.

## Supported Language Features

Supported means there is _some_ level of test coverage. It does not mean it is 100% supported.

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
- [ ] Enums
- [ ] Delegates
- [x] File-local types (C# 11)
- [x] Primary constructors (C# 12)

### Member Declarations

- [x] Instance methods
- [x] Static methods
- [ ] Constructors
- [x] Properties (get-only)
- [x] Auto-properties (get-only or init-only)
- [x] Fields (readonly)
- [x] Events
- [ ] Indexers
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
- [ ] Inline arrays
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
- [x] Volatile fields

### Generic and Advanced Constructs

- [x] Generic methods
- [ ] Generic type parameters with constraints
- [ ] Covariance and contravariance
- [x] Reflection
- [x] Dynamic typing
- [x] Unsafe code

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

```csharp
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        // PureWrapper hides the impure operation
        PureWrapper();
    }

    // This method doesn't have [EnforcePure] but is called from a pure method
    private void PureWrapper()
    {
        // This calls an impure method, but the analyzer may not detect it
        IndirectImpure();
    }

    private void IndirectImpure()
    {
        Console.WriteLine("Hello"); // Impure operation
    }
}

// No analyzer error is reported, even though TestMethod is impure
// because it indirectly calls Console.WriteLine
```

#### External Library Method Calls

```csharp
using System;
using ExternalLibrary; // Hypothetical external library

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        // The analyzer doesn't know if ExternalHelper is pure
        ExternalHelper.DoSomething();
    }
}

// No analyzer error is reported, even though ExternalHelper.DoSomething
// might be impure, because the analyzer can't analyze its implementation
```

#### Reflection-Based Modification

```csharp
using System;
using System.Reflection;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TargetClass
{
    public int Value { get; private set; }
}

public class TestClass
{
    [EnforcePure]
    public void TestMethod(TargetClass target)
    {
        // Using reflection to bypass access restrictions and modify state
        typeof(TargetClass)
            .GetProperty("Value")
            .SetValue(target, 42); // Impure operation via reflection
    }
}

// No analyzer error is reported, even though TestMethod is modifying state
// through reflection, which is impure
```

### String Manipulation Examples

String operations are generally considered pure, but care must be taken with culture-dependent operations:

#### Pure String Operations

```csharp
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public string ProcessText(string input)
    {
        // Basic string operations are pure
        string trimmed = input.Trim();
        string upperCase = input.ToUpper();
        string replaced = input.Replace("old", "new");
        string substring = input.Substring(0, Math.Min(10, input.Length));

        // String concatenation is pure
        string combined = $"{trimmed} - {upperCase}";

        return combined;
    }
}

// No analyzer errors - method is pure
```

#### Culture-Dependent String Operations (Potentially Impure)

```csharp
using System;
using System.Globalization;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public string CultureDependentMethod(string input)
    {
        // Without specifying culture, this depends on the current thread's culture
        // which is not deterministic and therefore impure
        string upperCase = input.ToUpper(); // Potentially impure!

        return upperCase;
    }

    [EnforcePure]
    public string CultureInvariantMethod(string input)
    {
        // Specifying culture makes the operation pure and deterministic
        string upperCase = input.ToUpper(CultureInfo.InvariantCulture);
        string lowerCase = input.ToLower(CultureInfo.InvariantCulture);

        return $"{upperCase} - {lowerCase}";
    }
}

// Current analyzer may not detect the impurity in CultureDependentMethod
```

#### String.Format with Culture (Pure)

```csharp
using System;
using System.Globalization;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public string FormatNumber(double value)
    {
        // Specifying culture ensures deterministic output regardless of system settings
        return string.Format(CultureInfo.InvariantCulture, "Value: {0:F2}", value);
    }

    [EnforcePure]
    public string FormatDateTime(DateTime date)
    {
        // Culture-specific formatting should always specify culture for purity
        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}

// No analyzer errors - methods are pure
```

#### String Parsing Examples

```csharp
using System;
using System.Globalization;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public int ParseNumber(string input)
    {
        // TryParse with invariant culture is pure
        if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
            return result;

        // Input validation through exceptions is pure
        throw new ArgumentException("Invalid number format");
    }

    [EnforcePure]
    public DateTime ParseDate(string input)
    {
        // DateTime.Parse without culture specification is potentially impure
        // as it depends on the current thread's culture
        var date = DateTime.Parse(input); // Potentially impure!

        return date;
    }

    [EnforcePure]
    public DateTime ParseDatePure(string input)
    {
        // Specifying culture makes it pure
        return DateTime.Parse(input, CultureInfo.InvariantCulture);
    }
}

// Current analyzer may not detect the impurity in ParseDate
```

### Async Methods and Tasks

The analyzer supports async methods and Task/ValueTask operations with the following considerations:

- Methods marked with `async` and returning `Task` or `ValueTask` can be marked as `[EnforcePure]`
- Pure async methods should only await other pure operations or methods
- The following Task operations are considered pure:
  - `Task.FromResult()`
  - `Task.CompletedTask`
  - `ValueTask.FromResult()`
  - `ValueTask<T>` constructors
  - Awaiting parameters of Task types
  - Awaiting other pure methods
- The following Task operations are considered impure:
  - `Task.Delay()`
  - `Task.Run()`
  - `Task.Factory.StartNew()`
  - `Task.WhenAll()/WhenAny()`
  - `Task.Yield()`

#### Pure Async Method Example

```csharp
using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public async Task<int> PureAsyncMethod(int value)
    {
        // Task.FromResult is pure
        int result = await Task.FromResult(value * 2);

        // Calling another pure method is also pure
        return await PureHelperAsync(result);
    }

    [EnforcePure]
    private async Task<int> PureHelperAsync(int value)
    {
        // Task.CompletedTask is pure
        await Task.CompletedTask;
        return value + 1;
    }
}

// No analyzer errors - methods are pure
```

#### Impure Async Method Example

```csharp
using System;
using System.Threading.Tasks;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public async Task ImpureAsyncMethod()
    {
        // Task.Delay is impure as it has side effects (waits)
        await Task.Delay(1000);

        // Task.Run is impure as it launches work on another thread
        await Task.Run(() => Console.WriteLine("Hello"));
    }
}

// Analyzer Error: PMA0001 - Method 'ImpureAsyncMethod' is marked as pure but contains impure operations
```
