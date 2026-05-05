using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;
using PurelySharp.Analyzer;

namespace PurelySharp.Test
{
    [TestFixture]
    public class DiagnosticEvidenceTests
    {
        [Test]
        public async Task Ps0002_KnownImpureCatalogHit_IncludesStructuredEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        Console.WriteLine(""impure"");
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("MethodInvocationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityOperationKindProperty], Is.EqualTo("Invocation"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCatalogSourceProperty], Is.EqualTo("known_impure_namespace_or_type"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Console.WriteLine"));
        }

        [Test]
        public async Task Ps0002_ImpureCallee_IncludesCalleeChain()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void Caller()
    {
        Callee();
    }

    [EnforcePure]
    public void Callee()
    {
        Console.WriteLine(""impure"");
    }
}");

            var callerDiagnostic = diagnostics
                .Where(d => d.Id == PurelySharpDiagnostics.PurityNotVerifiedId)
                .Single(d => d.GetMessage().Contains("'Caller'", StringComparison.Ordinal));

            Assert.That(callerDiagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(callerDiagnostic.Properties[PurelySharpDiagnostics.ImpurityCalleeChainProperty], Does.Contain("TestClass.Callee"));
            Assert.That(callerDiagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Console.WriteLine"));
        }

        [Test]
        public async Task Ps0002_UnresolvedDelegateTarget_IncludesDistinctCategory()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void TestMethod(Action action)
    {
        action();
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("unresolved_delegate_target"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Action.Invoke"));
        }

        [Test]
        public async Task Ps0002_DynamicDispatch_IncludesDistinctCategory()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string TestMethod(dynamic value)
    {
        return value.ToString();
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("dynamic_dispatch"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityOperationKindProperty], Is.EqualTo("DynamicInvocation"));
        }

        [Test]
        public async Task Ps0002_DynamicBinaryOperation_IncludesDynamicDispatchCategory()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod(dynamic value)
    {
        return value + 1;
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("dynamic_dispatch"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("BinaryOperationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityOperationKindProperty], Is.EqualTo("Binary"));
        }

        [Test]
        public async Task Ps0002_DynamicUnaryOperation_IncludesDynamicDispatchCategory()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod(dynamic value)
    {
        return -value;
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("dynamic_dispatch"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("UnaryOperationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityOperationKindProperty], Is.EqualTo("Unary"));
        }

        [Test]
        public async Task Ps0002_SourceExternCall_IncludesUnknownExternalCallCategory()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System.Runtime.InteropServices;
using PurelySharp.Attributes;

public static class NativeMethods
{
    [DllImport(""native.dll"")]
    public static extern int ReadValue();
}

public class TestClass
{
    [EnforcePure]
    public int TestMethod()
    {
        return NativeMethods.ReadValue();
    }
}");

            var diagnostic = diagnostics
                .Where(d => d.Id == PurelySharpDiagnostics.PurityNotVerifiedId)
                .Single(d => d.GetMessage().Contains("'TestMethod'", StringComparison.Ordinal));

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("unknown_external_call"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("NativeMethods.ReadValue"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCatalogSourceProperty], Is.EqualTo("extern"));
        }

        [Test]
        public async Task Ps0002_MutableStateWrite_IncludesDistinctCategory()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using PurelySharp.Attributes;

public class TestClass
{
    private int _value;

    [EnforcePure]
    public void TestMethod()
    {
        _value = 1;
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("mutable_state_write"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("AssignmentPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("TestClass._value"));
        }

        [Test]
        public async Task Ps0002_AssignmentRhsImpurity_PreservesOriginalEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod()
    {
        int value;
        value = Console.Read();
        return value;
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("MethodInvocationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Console.Read"));
        }

        [Test]
        public async Task Ps0002_MutableStateRead_IncludesDistinctCategory()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using PurelySharp.Attributes;

public class TestClass
{
    private static int s_value;

    [EnforcePure]
    public int TestMethod()
    {
        return s_value;
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("mutable_state_read"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("FieldReferencePurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("TestClass.s_value"));
        }

        [Test]
        public async Task Ps0002_StaticPropertyGetterImpurity_PreservesGetterEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    private static int Value
    {
        get
        {
            Console.WriteLine(""impure"");
            return 1;
        }
    }

    [EnforcePure]
    public int TestMethod()
    {
        return Value;
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("MethodInvocationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Console.WriteLine"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCalleeChainProperty], Does.Contain("TestClass.Value.get"));
        }

        [Test]
        public async Task Ps0002_StaticConstructorTrigger_PreservesConstructorEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public class Config
{
    static Config()
    {
        Console.WriteLine(""impure"");
    }

    public static int Value => 1;
}

public class TestClass
{
    [EnforcePure]
    public int TestMethod()
    {
        return Config.Value;
    }
}");

            var diagnostic = diagnostics
                .Where(d => d.Id == PurelySharpDiagnostics.PurityNotVerifiedId)
                .Single(d => d.GetMessage().Contains("'TestMethod'", StringComparison.Ordinal));

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("MethodInvocationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Console.WriteLine"));
        }

        [Test]
        public async Task Ps0002_MethodArgumentImpurity_PreservesOriginalEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod()
    {
        return Math.Abs(Console.Read());
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("MethodInvocationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Console.Read"));
        }

        [Test]
        public async Task Ps0002_LinqArgumentImpurity_PreservesOriginalEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using System.Collections.Generic;
using System.Linq;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public IEnumerable<int> TestMethod(IEnumerable<int> values)
    {
        return values.Skip(Console.Read());
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("MethodInvocationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Console.Read"));
        }

        [Test]
        public async Task Ps0002_DirectThrowOnly_IncludesThrowCategory()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        throw new InvalidOperationException();
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("throw"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("ThrowOperationPurityRule"));
        }

        [Test]
        public async Task Ps0002_UnsafePointerOperation_IncludesUnsafePointerCategory()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public unsafe int TestMethod()
    {
        int value = 1;
        int* pointer = &value;
        return *pointer;
    }
}", allowUnsafe: true);

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("unsafe_pointer"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("UnsupportedOperation"));
        }

        [Test]
        public async Task Ps0002_RecursivePurityConservativeDiagnostic_IncludesStructuredEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int Fibonacci(int n)
    {
        if (n <= 1)
        {
            return n;
        }

        return Fibonacci(n - 1) + Fibonacci(n - 2);
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("unsupported_operation"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("RecursivePurityAnalysis"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCatalogSourceProperty], Is.EqualTo("recursive_call"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("TestClass.Fibonacci"));
        }

        [Test]
        public async Task Ps0002_EnvironmentProperty_IncludesReflectionEnvironmentCategory()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod()
    {
        return Environment.TickCount;
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("reflection_environment_source"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("PropertyReferencePurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Environment.TickCount"));
        }

        [Test]
        public async Task Ps0002_ReflectionCall_IncludesReflectionEnvironmentCategory()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public Type? TestMethod(string typeName)
    {
        return Type.GetType(typeName);
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("reflection_environment_source"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("MethodInvocationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Type.GetType"));
        }

        [Test]
        public async Task Ps0002_LockStatement_IncludesSynchronizationCategory()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using PurelySharp.Attributes;

public class TestClass
{
    private readonly object _gate = new object();

    [EnforcePure]
    public void TestMethod()
    {
        lock (_gate)
        {
        }
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("synchronization"));
        }

        [Test]
        public async Task Ps0002_MonitorCall_IncludesSynchronizationCategory()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System.Threading;
using PurelySharp.Attributes;

public class TestClass
{
    private static readonly object Gate = new object();

    [EnforcePure]
    public void TestMethod()
    {
        Monitor.Enter(Gate);
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("synchronization"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("MethodInvocationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Threading.Monitor.Enter"));
        }

        [Test]
        public async Task Ps0002_MutableCollectionCreation_IncludesCatalogEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public List<int> TestMethod()
    {
        return new List<int>();
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("ObjectCreationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCatalogSourceProperty], Is.EqualTo("known_mutable_collection"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Collections.Generic.List<int>"));
        }

        [Test]
        public async Task Ps0002_VariableInitializerImpurity_PreservesOriginalEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod()
    {
        int value = Console.Read();
        return value;
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("MethodInvocationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Console.Read"));
        }

        [Test]
        public async Task Ps0002_SpreadOperandImpurity_PreservesOriginalEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using System.Collections.Immutable;
using PurelySharp.Attributes;

public class TestClass
{
    private static ImmutableArray<int> GetValues()
    {
        Console.WriteLine(""side effect"");
        return ImmutableArray<int>.Empty;
    }

    [EnforcePure]
    public ImmutableArray<int> Extend()
    {
        return [.. GetValues(), 42];
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("MethodInvocationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Console.WriteLine"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCalleeChainProperty], Does.Contain("TestClass.GetValues"));
        }

        [Test]
        public async Task Ps0002_DirectArrayCreation_IncludesArrayCreationEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int[] TestMethod()
    {
        return new int[1];
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("mutable_state_write"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("ArrayCreationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCatalogSourceProperty], Is.EqualTo("array_creation"));
        }

        [Test]
        public async Task Ps0002_ArrayElementImpureArrayReference_PreservesOriginalEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod()
    {
        return GetValues()[0];
    }

    [EnforcePure]
    private int[] GetValues()
    {
        Console.WriteLine(""impure"");
        return new int[1];
    }
}");

            var diagnostic = diagnostics
                .Where(d => d.Id == PurelySharpDiagnostics.PurityNotVerifiedId)
                .Single(d => d.GetMessage().Contains("'TestMethod'", StringComparison.Ordinal));

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("MethodInvocationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Console.WriteLine"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCalleeChainProperty], Does.Contain("TestClass.GetValues"));
        }

        [Test]
        public async Task Ps0002_UserDefinedConversionImpurity_PreservesOperatorEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public readonly struct Wrapped
{
    public static explicit operator int(Wrapped value)
    {
        Console.WriteLine(""side effect"");
        return 1;
    }
}

public class TestClass
{
    [EnforcePure]
    public int TestMethod(Wrapped value)
    {
        return (int)value;
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("MethodInvocationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Console.WriteLine"));
        }

        [Test]
        public async Task Ps0002_UserDefinedBinaryOperatorImpurity_PreservesOperatorEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public readonly struct Wrapped
{
    public static Wrapped operator +(Wrapped left, Wrapped right)
    {
        Console.WriteLine(""side effect"");
        return left;
    }
}

public class TestClass
{
    [EnforcePure]
    public Wrapped TestMethod(Wrapped left, Wrapped right)
    {
        return left + right;
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("MethodInvocationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Console.WriteLine"));
        }

        [Test]
        public async Task Ps0002_UserDefinedUnaryOperatorImpurity_PreservesOperatorEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public readonly struct Wrapped
{
    public static Wrapped operator -(Wrapped value)
    {
        Console.WriteLine(""side effect"");
        return value;
    }
}

public class TestClass
{
    [EnforcePure]
    public Wrapped TestMethod(Wrapped value)
    {
        return -value;
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("MethodInvocationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Console.WriteLine"));
        }

        [Test]
        public async Task Ps0002_UsingDisposeImpurity_PreservesDisposeEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public sealed class Resource : IDisposable
{
    public void Dispose()
    {
        Console.WriteLine(""side effect"");
    }
}

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        using (var resource = new Resource())
        {
        }
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("MethodInvocationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Console.WriteLine"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCalleeChainProperty], Does.Contain("Resource.Dispose"));
        }

        [Test]
        public async Task Ps0002_ConstructorInitializerImpurity_PreservesConstructorEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public class BaseType
{
    public BaseType()
    {
        Console.WriteLine(""side effect"");
    }
}

public class DerivedType : BaseType
{
    [EnforcePure]
    public DerivedType()
        : base()
    {
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("MethodInvocationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Console.WriteLine"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCalleeChainProperty], Does.Contain("BaseType.BaseType"));
        }

        [Test]
        public async Task Ps0002_DelegateCreationImpurity_IncludesTargetCalleeChain()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    public static void ImpureTarget()
    {
        Console.WriteLine(""side effect"");
    }

    [EnforcePure]
    public void TestMethod()
    {
        Action action = ImpureTarget;
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("MethodInvocationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Console.WriteLine"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCalleeChainProperty], Does.Contain("TestClass.ImpureTarget"));
        }

        [Test]
        public async Task Ps0002_EventAssignment_IncludesMutableStateWriteEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    public event EventHandler? Changed;

    private void Handler(object? sender, EventArgs args)
    {
    }

    [EnforcePure]
    public void TestMethod()
    {
        Changed += Handler;
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("mutable_state_write"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("EventAssignmentPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCatalogSourceProperty], Is.EqualTo("event_subscription"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("TestClass.Changed"));
        }

        [Test]
        public async Task Ps0002_InterpolatedStringExpressionImpurity_PreservesOriginalEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string TestMethod()
    {
        return $""{Console.Read()}"";
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("catalog_hit"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("MethodInvocationPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpuritySymbolProperty], Does.Contain("System.Console.Read"));
        }

        [Test]
        public async Task Ps0002_ArrayCollectionExpression_IncludesTargetEvidence()
        {
            var diagnostics = await GetAnalyzerDiagnosticsAsync(@"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int[] TestMethod()
    {
        return [1, 2, 3];
    }
}");

            var diagnostic = SingleDiagnostic(diagnostics, PurelySharpDiagnostics.PurityNotVerifiedId);

            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCategoryProperty], Is.EqualTo("mutable_state_write"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityRuleProperty], Is.EqualTo("CollectionExpressionPurityRule"));
            Assert.That(diagnostic.Properties[PurelySharpDiagnostics.ImpurityCatalogSourceProperty], Is.EqualTo("collection_expression_target"));
        }

        [Test]
        public async Task Ps0009_IsOnlyEmittedWhenExplanationsAreEnabled()
        {
            var source = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        Console.WriteLine(""impure"");
    }
}";

            var defaultDiagnostics = await GetAnalyzerDiagnosticsAsync(source);
            var explanationDiagnostics = await GetAnalyzerDiagnosticsAsync(
                source,
                ImmutableDictionary<string, string>.Empty.Add("purelysharp_emit_explanations", "true"));

            Assert.That(defaultDiagnostics.Any(d => d.Id == PurelySharpDiagnostics.PurityExplanationId), Is.False);
            Assert.That(explanationDiagnostics.Any(d => d.Id == PurelySharpDiagnostics.PurityExplanationId), Is.True);
        }

        private static Diagnostic SingleDiagnostic(ImmutableArray<Diagnostic> diagnostics, string diagnosticId)
        {
            return diagnostics.Single(d => d.Id == diagnosticId);
        }

        private static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
            string source,
            ImmutableDictionary<string, string>? globalOptions = null,
            bool allowUnsafe = false)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
            var references = GetTrustedPlatformReferences()
                .Add(MetadataReference.CreateFromFile(typeof(PurelySharp.Attributes.EnforcePureAttribute).Assembly.Location));

            var compilation = CSharpCompilation.Create(
                "DiagnosticEvidenceTests",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: allowUnsafe));

            var analyzerOptions = new AnalyzerOptions(
                ImmutableArray<AdditionalText>.Empty,
                new TestAnalyzerConfigOptionsProvider(globalOptions ?? ImmutableDictionary<string, string>.Empty));

            var compilationWithAnalyzers = compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(new PurelySharpAnalyzer()),
                new CompilationWithAnalyzersOptions(
                    analyzerOptions,
                    onAnalyzerException: null,
                    concurrentAnalysis: false,
                    logAnalyzerExecutionTime: false,
                    reportSuppressedDiagnostics: false));

            return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
        }

        private static ImmutableArray<MetadataReference> GetTrustedPlatformReferences()
        {
            var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            if (string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
            {
                return ImmutableArray.Create<MetadataReference>(
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location));
            }

            return trustedPlatformAssemblies
                .Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path))
                .Cast<MetadataReference>()
                .ToImmutableArray();
        }

        private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
        {
            private readonly AnalyzerConfigOptions _globalOptions;
            private readonly AnalyzerConfigOptions _emptyOptions = new TestAnalyzerConfigOptions(ImmutableDictionary<string, string>.Empty);

            public TestAnalyzerConfigOptionsProvider(ImmutableDictionary<string, string> globalOptions)
            {
                _globalOptions = new TestAnalyzerConfigOptions(globalOptions);
            }

            public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

            public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _emptyOptions;

            public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _emptyOptions;
        }

        private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
        {
            private readonly ImmutableDictionary<string, string> _values;

            public TestAnalyzerConfigOptions(ImmutableDictionary<string, string> values)
            {
                _values = values;
            }

            public override bool TryGetValue(string key, out string value)
            {
                if (_values.TryGetValue(key, out var found))
                {
                    value = found;
                    return true;
                }

                value = string.Empty;
                return false;
            }
        }
    }
}
