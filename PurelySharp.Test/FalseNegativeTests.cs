using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class FalseNegativeTests
    {
        [Test]
        public async Task ThrowOnlyMethod_NoDiagnostic_Bug()
        {
            // This test highlights that the analyzer currently treats 'throw' statements
            // as pure even though they change control-flow and should be considered impure.
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:ThrowingMethod|}()
    {
        // Throwing an exception is impure (alters control-flow, allocates),
        // but the analyzer currently fails to report a diagnostic.
        throw new InvalidOperationException();
    }
}";
            // Analyzer correctly flags this, test expects it.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DelegateInvocationOfImpureAction_NoDiagnostic_Bug()
        {
            // The analyzer does not follow delegate targets when the delegate is stored in a field.
            // Invoking such a delegate should therefore be reported as impure but currently is not.
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    // Static readonly field hiding an impure Console.WriteLine call inside the delegate.
    private static readonly Action ImpureAction = () => Console.WriteLine(""Side-effect"");

    [EnforcePure]
    public void {|PS0002:CallImpureDelegate|}()
    {
        // Invoking the delegate causes side-effects, but the analyzer misses it.
        ImpureAction();
    }
}";
            // No diagnostic expected – the analyzer incorrectly considers the method pure.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LazyValueWithImpureFactory_NoDiagnostic_Bug()
        {
            // Accessing .Value on Lazy<T> executes the factory if not initialized.
            // If the factory is impure, this access is impure, but analyzer may miss it.
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    private static int counter = 0;
    private readonly Lazy<int> _lazyValue = new Lazy<int>(() => {
        Console.WriteLine(""Impure factory executed""); // Impure action
        counter++; // Impure action
        return counter;
    });

    [EnforcePure]
    public int {|PS0002:GetLazyValue|}()
    {
        // Accessing .Value triggers the impure factory on first call.
        return _lazyValue.Value;
    }
}";
            // Analyzer correctly flags this, test expects it.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConcurrentDictGetOrAddImpureFactory_NoDiagnostic_Bug()
        {
            // GetOrAdd executes the factory delegate only if the key is not present.
            // Analyzer might not check the purity of this conditionally executed factory.
            var test = @"
using System;
using System.Collections.Concurrent;
using PurelySharp.Attributes;

public class TestClass
{
    private readonly ConcurrentDictionary<string, int> _dict = new ConcurrentDictionary<string, int>();
    private static int _seed = 0;

    [EnforcePure]
    public int {|PS0002:GetValue|}(string key)
    {
        // The factory delegate () => { Console.WriteLine(); return ++_seed; } is impure.
        return _dict.GetOrAdd(key, k =>
        {
            Console.WriteLine($""Adding key {k}""); // Impure
            return ++_seed; // Impure
        });
    }
}";
            // Analyzer correctly flags this, test expects it.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ReturnRefToMutableField_NoDiagnostic_Bug()
        {
            // Returning a ref to a mutable field allows external modification, making the method impure.
            // The analyzer might not consider the implication of returning the ref.
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    private int _mutableField = 0;

    [EnforcePure]
    public ref int {|PS0002:GetMutableFieldRef|}()
    {
        // Returning a ref to a mutable field is impure.
        return ref _mutableField;
    }

    // Example of use making it impure:
    // var tester = new TestClass();
    // ref int fieldRef = ref tester.GetMutableFieldRef();
    // fieldRef = 100; // Modifies the internal state via the returned ref
}";
            // Analyzer correctly flags this, test expects it.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task VolatileRead_NoDiagnostic_Bug()
        {
            // Volatile.Read introduces memory barrier semantics and interacts with mutable state,
            // making it impure. Analyzer might incorrectly treat it as a simple read.
            var test = @"
using System;
using System.Threading;
using PurelySharp.Attributes;

public class TestClass
{
    private int _volatileField = 0;

    [EnforcePure]
    public int {|PS0002:ReadVolatile|}()
    {
        // Volatile.Read is impure.
        return Volatile.Read(ref _volatileField);
    }
}";
            // Analyzer correctly flags this, test expects it.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task EventSubscription_NoDiagnostic_Bug()
        {
            // Subscribing to an event modifies the event's invocation list (state change).
            // Analyzer currently misses this impurity.
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    public event EventHandler MyEvent;

    private void Handler(object sender, EventArgs e) { }

    [EnforcePure]
    public void {|PS0002:SubscribeEvent|}()
    {
        // Event subscription '+=' modifies state and is impure.
        MyEvent += Handler;
    }
}";
            // Analyzer correctly flags this, test expects it.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureStaticConstructorTrigger_NoDiagnostic_Bug()
        {
            // Accessing a static member triggers the static constructor.
            // If the .cctor is impure, the access point should be considered impure.
            var test = @"
using System;
using PurelySharp.Attributes;

public static class ImpureInitializer
{
    public static readonly int Value;

    // Static constructor with side-effects
    static ImpureInitializer()
    {
        Console.WriteLine(""Static constructor ran!""); // Impure action
        Value = 100;
    }
}

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:TriggerStaticConstructor|}()
    {
        // Accessing ImpureInitializer.Value triggers the impure .cctor
        return ImpureInitializer.Value;
    }
}";
            // No diagnostic expected – analyzer limitation.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task SuppressFinalizeCall_NoDiagnostic_Bug()
        {
            // GC.SuppressFinalize interacts with the garbage collector, a side effect.
            var test = @"
using System;
using PurelySharp.Attributes;

public class Resource : IDisposable { public void Dispose() {} }

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:UseResource|}(Resource r)
    {
        // GC.SuppressFinalize is impure.
        GC.SuppressFinalize(r);
    }
}";
            // No diagnostic expected – analyzer limitation.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureImplicitConversion_NoDiagnostic_Bug()
        {
            // Using an implicit conversion operator that has side effects.
            var test = @"
using System;
using PurelySharp.Attributes;

public class ImpureConverter
{
    public int Value { get; }
    public ImpureConverter(int value) { Value = value; }

    // Implicit conversion with a side effect
    public static implicit operator int(ImpureConverter ic)
    {
        Console.WriteLine($""Converting ImpureConverter({ic.Value}) to int"" + Environment.NewLine); // Impure
        return ic.Value;
    }
}

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:ConvertIt|}(ImpureConverter ic)
    {
        // The assignment triggers the impure implicit conversion.
        int result = ic;
        return result;
    }
}";
            // No diagnostic expected – analyzer limitation.
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}