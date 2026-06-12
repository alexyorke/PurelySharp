using System.Threading.Tasks;
using NUnit.Framework;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ObjectEqualsDispatchTests
    {
        [Test]
        public async Task ObjectEqualsOnObjectParameter_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(object left, object right)
    {
        return left.Equals(right);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task IEquatableDispatchToImpureImplementation_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public sealed class MutableRecord : IEquatable<MutableRecord>
{
    public bool Equals(MutableRecord other)
    {
        Console.WriteLine(""equals"");
        return true;
    }
}

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(MutableRecord left, MutableRecord right)
    {
        return ((IEquatable<MutableRecord>)left).Equals(right);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task IEquatableDispatchToPureImplementation_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public sealed class MutableRecord : IEquatable<MutableRecord>
{
    public bool Equals(MutableRecord other)
    {
        return other != null;
    }
}

public class TestClass
{
    [EnforcePure]
    public bool TestMethod(MutableRecord left, MutableRecord right)
    {
        return ((IEquatable<MutableRecord>)left).Equals(right);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task EqualityComparerDefaultEqualsDispatchToImpureImplementation_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using PurelySharp.Attributes;

public sealed class MutableRecord : IEquatable<MutableRecord>
{
    public bool Equals(MutableRecord other)
    {
        Console.WriteLine(""equals"");
        return true;
    }

    public override bool Equals(object value)
    {
        return value is MutableRecord other && Equals(other);
    }

    public override int GetHashCode()
    {
        return 0;
    }
}

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(MutableRecord left, MutableRecord right)
    {
        return EqualityComparer<MutableRecord>.Default.Equals(left, right);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task EqualityComparerDefaultGetHashCodeDispatchToImpureOverride_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using PurelySharp.Attributes;

public sealed class MutableRecord
{
    public override int GetHashCode()
    {
        Console.WriteLine(""hash"");
        return 0;
    }
}

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:TestMethod|}(MutableRecord value)
    {
        return EqualityComparer<MutableRecord>.Default.GetHashCode(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task EqualityComparerDefaultEqualsForFloatingAndDecimalValues_NoDiagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool TestMethod(double left, double right, decimal first, decimal second)
    {
        return EqualityComparer<double>.Default.Equals(left, right) &&
            EqualityComparer<decimal>.Default.Equals(first, second);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task EqualityComparerDefaultGetHashCodeForFloatingAndDecimalValues_NoDiagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod(double value, decimal amount)
    {
        return EqualityComparer<double>.Default.GetHashCode(value) +
            EqualityComparer<decimal>.Default.GetHashCode(amount);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ListContainsDispatchToImpureEquatableImplementation_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using PurelySharp.Attributes;

public sealed class MutableRecord : IEquatable<MutableRecord>
{
    public bool Equals(MutableRecord other)
    {
        Console.WriteLine(""equals"");
        return true;
    }
}

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(List<MutableRecord> values, MutableRecord value)
    {
        return values.Contains(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ListContainsForBuiltinValueEquality_NoDiagnostic()
        {
            var test = @"
using System.Collections.Generic;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool TestMethod(List<int> values, int value)
    {
        return values.Contains(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ArrayIndexOfDispatchToImpureEquatableImplementation_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public sealed class MutableRecord : IEquatable<MutableRecord>
{
    public bool Equals(MutableRecord other)
    {
        Console.WriteLine(""equals"");
        return true;
    }
}

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:TestMethod|}(MutableRecord[] values, MutableRecord value)
    {
        return Array.IndexOf(values, value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ArrayIndexOfForBuiltinValueEquality_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int TestMethod(int[] values, int value)
    {
        return Array.IndexOf(values, value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LinqContainsDispatchToImpureEquatableImplementation_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;
using PurelySharp.Attributes;

public sealed class MutableRecord : IEquatable<MutableRecord>
{
    public bool Equals(MutableRecord other)
    {
        Console.WriteLine(""equals"");
        return true;
    }
}

public class TestClass
{
    [EnforcePure]
    public bool {|PS0002:TestMethod|}(IEnumerable<MutableRecord> values, MutableRecord value)
    {
        return values.Contains(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LinqContainsForBuiltinValueEquality_NoDiagnostic()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public bool TestMethod(IEnumerable<int> values, int value)
    {
        return values.Contains(value);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
