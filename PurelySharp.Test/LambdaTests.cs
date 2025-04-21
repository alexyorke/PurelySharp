using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Microsoft.CodeAnalysis.Testing;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class LambdaTests
    {
        [Test]
        public async Task PureMethodWithLambda_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Linq;



public class TestClass
{
    [EnforcePure]
    public int[] {|PS0002:TestMethod|}(int[] numbers)
    {
        // Lambda that performs a pure operation
        return numbers.Select(x => x * 2).ToArray();
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ImpureMethodWithLambda_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Linq;



public class TestClass
{
    [EnforcePure]
    public void {|PS0002:TestMethod|}(int[] numbers)
    {
        // Lambda that performs an impure operation (Console.WriteLine)
        numbers.ToList().ForEach(x => Console.WriteLine(x));
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithLambdaCapture_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Linq;



public class TestClass
{
    private int _sum;

    [EnforcePure]
    public int[] {|PS0002:TestMethod|}(int[] numbers)
    {
        // Lambda that captures and modifies a field (impure)
        numbers.ToList().ForEach(x => _sum += x);
        return numbers;
    }
}";

            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


