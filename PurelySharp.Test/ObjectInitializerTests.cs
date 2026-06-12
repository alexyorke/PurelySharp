using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ObjectInitializerTests
    {
        [Test]
        public async Task ObjectInitializerWithImpureSetter_Diagnostic()
        {
            var testCode = @"
using System;
using PurelySharp.Attributes;

public class Target
{
    public int Value
    {
        set { Console.WriteLine(value); }
    }
}

public class TestClass
{
    [EnforcePure]
    public Target {|PS0002:Create|}()
    {
        return new Target { Value = 1 };
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task NestedObjectInitializerMutatingExistingMember_Diagnostic()
        {
            var testCode = @"
using PurelySharp.Attributes;

public class Shared
{
    public int X;
}

public class Holder
{
    public static readonly Shared SharedInstance = new Shared();

    public Shared Field = SharedInstance;
}

public class TestClass
{
    [EnforcePure]
    public Holder {|PS0002:Create|}()
    {
        return new Holder { Field = { X = 1 } };
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }

        [Test]
        public async Task ObjectInitializerIndexerWithImpureIndex_Diagnostic()
        {
            var testCode = @"
using System;
using PurelySharp.Attributes;

public class Target
{
    public int this[int index]
    {
        [EnforcePure]
        set { }
    }
}

public class TestClass
{
    [EnforcePure]
    public Target {|PS0002:Create|}()
    {
        return new Target { [Console.Read()] = 1 };
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(testCode);
        }
    }
}
