using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using Microsoft.CodeAnalysis;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ExceptionHandlingTests
    {
        [Test]
        public async Task PureMethodWithExceptionHandling_NoDiagnostic()
        {


            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public int TestMethod(int x)
    {
        try
        {
            // Exception handling itself is not impure
            if (x < 0)
            {
                throw new ArgumentException(""x cannot be negative"", nameof(x));
            }
            
            return x * 2;
        }
        catch (Exception ex)
        {
            // Reading the exception message is also pure
            string message = ex.Message;
            return 0;
        }
    }
}";


            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task PureMethodWithExceptionHandlingAndImpureOperation_NoDiagnostic()
        {



            var test = @"
using System;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public int TestMethod(int x)
    {
        try
        {
            if (x < 0)
            {
                throw new ArgumentException(""x cannot be negative"", nameof(x));
            }
            
            return x * 2;
        }
        catch (Exception ex)
        {
            // Writing to console is impure
            Console.WriteLine(ex.Message);
            return 0;
        }
    }
}";


            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        

        [Test]
        public async Task ThrowIfNull_IsTreatedAsPure()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void Check(object o)
    {
        if (o == null) throw new ArgumentNullException(nameof(o));
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}


