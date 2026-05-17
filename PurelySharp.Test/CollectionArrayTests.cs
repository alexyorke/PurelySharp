using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class CollectionArrayTests
    {
        [Test]
        public async Task ListToArray_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int[] {|PS0002:TestMethod|}(List<int> values)
    {
        return values.ToArray();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task QueueToArray_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int[] {|PS0002:TestMethod|}(Queue<int> values)
    {
        return values.ToArray();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task StackToArray_Diagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int[] {|PS0002:TestMethod|}(Stack<int> values)
    {
        return values.ToArray();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ArrayConvertAll_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int[] {|PS0002:TestMethod|}(int[] values)
    {
        return Array.ConvertAll(values, static value => value + 1);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ReadOnlySpanToArray_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int[] {|PS0002:TestMethod|}(ReadOnlySpan<int> values)
    {
        return values.ToArray();
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task FreshLocalArrayReturnedThroughObjectAlias_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public object {|PS0002:TestMethod|}()
    {
        var array = new int[1];
        object boxed = array;
        return boxed;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task FreshArrayCreationReturnedThroughObjectAlias_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public object {|PS0002:TestMethod|}()
    {
        object boxed = new int[1];
        return boxed;
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
