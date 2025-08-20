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
        public async Task ThrowOnlyMethod_NoDiagnostic()
        {


            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void ThrowingMethod()
    {
        // Throwing an exception is impure (alters control-flow, allocates),
        // but the analyzer currently fails to report a diagnostic.
        throw new InvalidOperationException();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DelegateInvocationOfImpureAction_NoDiagnostic_Bug()
        {


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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LazyValueWithImpureFactory_NoDiagnostic_Bug()
        {


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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ConcurrentDictGetOrAddImpureFactory_NoDiagnostic_Bug()
        {


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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ReturnRefToMutableField_NoDiagnostic_Bug()
        {


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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task VolatileRead_NoDiagnostic_Bug()
        {


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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task EventSubscription_NoDiagnostic_Bug()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class Button 
{
    public event EventHandler Clicked;
    public void OnClick() => Clicked?.Invoke(this, EventArgs.Empty);
}

public class TestForm
{
    private Button _button = new Button();

    [EnforcePure] // Should be impure due to subscription
    public void SetupForm()
    {
        _button.Clicked += Button_Clicked; // Event subscription (impure?)
    }

    private void Button_Clicked(object sender, EventArgs e)
    {
        Console.WriteLine(""Button clicked"");
    }
}";



            var expectedSetup = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(16, 17, 16, 26).WithArguments("SetupForm");


            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expectedSetup });
        }

        [Test]
        public async Task ImpureStaticConstructorTrigger_NoDiagnostic_Bug()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class Config
{
    private static readonly string Setting;

    [EnforcePure] // Static ctors with side effects are impure
    static Config()
    {
        Console.WriteLine(""Initializing Config...""); // Impure
        Setting = ""InitializedValue"";
    }
}

public class TestClass
{
    [EnforcePure] // Accessing Config triggers static ctor
    public void TriggerStaticConstructor()
    {
        string value = Config.Setting; // CS0122 Error Here
    }
}";

            var expectedCctor = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(10, 12, 10, 18).WithArguments(".cctor");
            var expectedTrigger = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(20, 17, 20, 41).WithArguments("TriggerStaticConstructor");
            var compilerError = DiagnosticResult.CompilerError("CS0122").WithSpan(22, 31, 22, 38).WithArguments("Config.Setting");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expectedCctor, expectedTrigger, compilerError });
        }

        [Test]
        public async Task SuppressFinalizeCall_NoDiagnostic_Bug()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class DisposableResource : IDisposable
{
    [EnforcePure] // Marked pure, but calls impure GC method
    public void Dispose() { GC.SuppressFinalize(this); } // Impure call - Line 8
}

public class TestClass
{
    // [EnforcePure] // Removed attribute
    public void UseResource() // Expect PS0004 here - Line 14
    {
        using (var res = new DisposableResource()) 
        { 
            // ... 
        }
    }
}";

            var expectedUseResource = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0004).WithSpan(14, 17, 14, 28).WithArguments("UseResource");
            var expectedDispose = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(8, 17, 8, 24).WithArguments("Dispose");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expectedUseResource, expectedDispose });
        }

        [Test]
        public async Task ImpureImplicitConversion_NoDiagnostic_Bug()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class ImpureConverter
{
    public int Value { get; }
    public ImpureConverter(int value) { Value = value; }

    public static implicit operator int(ImpureConverter ic)
    {
        Console.WriteLine($""Converting ImpureConverter({{ic.Value}}) to int"" + Environment.NewLine);
        return ic.Value;
    }
}

public class TestClass
{
    [EnforcePure]
    public int ConvertIt(ImpureConverter ic)
    {
        int result = ic;
        return result;
    }
}";
            var diagGetValue = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                          .WithSpan(7, 16, 7, 21)
                                          .WithArguments("get_Value");
            var diagCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                      .WithSpan(8, 12, 8, 27)
                                      .WithArguments(".ctor");
            var diagConvertIt = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                           .WithSpan(20, 16, 20, 25)
                                           .WithArguments("ConvertIt");


            await VerifyCS.VerifyAnalyzerAsync(test, new[] { diagGetValue, diagCtor, diagConvertIt });
        }




        [Test]

        public async Task ImpureDelegateViaParameter_NoDiagnostic_Bug()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass 
{
    private static void ImpureTarget() => Console.WriteLine(""Impure Target Called""); // Line 7

    private void InvokeDelegate(Action action) => action(); // Line 9

    [EnforcePure]
    public void CallImpureDelegateViaParam() // Line 12 
    {{
        InvokeDelegate(ImpureTarget);
    }}
}
";
            var expectedCallImpure = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                   .WithSpan(12, 17, 12, 43)
                                   .WithArguments("CallImpureDelegateViaParam");







            await VerifyCS.VerifyAnalyzerAsync(test, expectedCallImpure);
        }


        [Test]

        public async Task ImpureDelegateViaReturnValue_NoDiagnostic_Bug()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass 
{
    private Action GetImpureAction() // Line 7
    {{
        return () => Console.WriteLine(""Impure action returned and called"");
    }}

    [EnforcePure]
    public void CallImpureDelegateViaReturn() // Line 13
    {{
        Action impure = GetImpureAction();
        impure();
    }}
}
";



            var expectedCallImpureReturn = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                          .WithSpan(13, 17, 13, 44)
                                          .WithArguments("CallImpureDelegateViaReturn");

            await VerifyCS.VerifyAnalyzerAsync(test, expectedCallImpureReturn);
        }

        [Test]
        public async Task ImpureImplicitConversionViaMethodArg_NoDiagnostic_Bug()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class ImpureConverterArg
{
    public int Value { get; }
    public ImpureConverterArg(int value) { Value = value; }

    public static implicit operator int(ImpureConverterArg ic)
    {
        Console.WriteLine($""Converting ImpureConverterArg({{ic.Value}}) to int"" + Environment.NewLine);
        return ic.Value;
    }
}

public class TestClass
{
    private void TakesInt(int i) { /* Does nothing */ }

    [EnforcePure]
    public void ConvertItViaArg(ImpureConverterArg ic)
    {
        TakesInt(ic);
    }
}";
            var diagGetValue = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                          .WithSpan(7, 16, 7, 21)
                                          .WithArguments("get_Value");
            var diagCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                      .WithSpan(8, 12, 8, 30)
                                      .WithArguments(".ctor");
            var diagTakesInt = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                        .WithSpan(19, 18, 19, 26)
                                        .WithArguments("TakesInt");
            var diagConvertItViaArg = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                           .WithSpan(22, 17, 22, 32)
                                           .WithArguments("ConvertItViaArg");


            await VerifyCS.VerifyAnalyzerAsync(test, new[] { diagGetValue, diagCtor, diagTakesInt, diagConvertItViaArg });
        }

        [Test]
        public async Task IndirectStaticConstructorTrigger_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public static class Helper
{
    private static readonly string InitializedValue;
    [EnforcePure]
    static Helper()
    {
        Console.WriteLine(""Initializing Helper...""); // Impure
        InitializedValue = ""HelperValue"";
    }

    [EnforcePure]
    public static string GetValue() => InitializedValue;
}

public class AnotherClass
{
    // Calling Helper.GetValue implicitly runs Helper's static constructor
    [EnforcePure]
    public string TriggerIndirectStaticConstructor()
    {
        return Helper.GetValue();
    }
}
";

            var expectedCctor = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(9, 12, 9, 18).WithArguments(".cctor");
            var expectedGetValue = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(16, 26, 16, 34).WithArguments("GetValue");
            var expectedTrigger = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(23, 19, 23, 51).WithArguments("TriggerIndirectStaticConstructor");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expectedCctor, expectedGetValue, expectedTrigger });
        }

        [Test]
        public async Task DelegateInvocation_PureDelegate_NoDiagnostic_Bug()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int PureCalculation(int a, int b) => a + b;

    [EnforcePure]
    public int TestMethod()
    {
        Func<int, int, int> operation = PureCalculation;
        return operation(5, 10); // Should be pure, analyzer misses it
    }
}";

            var expected = VerifyCS.Diagnostic(PurelySharpAnalyzer.PS0002).WithSpan(11, 16, 11, 26).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expected });
        }
    }
}