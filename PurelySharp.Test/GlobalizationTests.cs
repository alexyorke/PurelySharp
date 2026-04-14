using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Globalization;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using PurelySharp.Attributes;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class GlobalizationTests
    {



        [Test]
        public async Task CultureInfo_InvariantCulture_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Globalization;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public string TestMethod()
    {
        // Pure: InvariantCulture is constant
        CultureInfo invariant = CultureInfo.InvariantCulture;
        return invariant.Name;
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DateTimeParse_InvariantCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Globalization;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public DateTime {|PS0002:TestMethod|}(string dateStr)
    {
        // DateTime.Parse remains impure even with an explicit culture provider.
        return DateTime.Parse(dateStr, CultureInfo.InvariantCulture);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DateTimeParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public DateTime {|PS0002:TestMethod|}(string dateStr)
    {
        return DateTime.Parse(dateStr);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DateTimeOffsetParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public DateTimeOffset {|PS0002:TestMethod|}(string dateStr)
    {
        return DateTimeOffset.Parse(dateStr);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DoubleParse_InvariantCulture_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Globalization;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public double TestMethod(string numStr)
    {
        // Pure: Explicitly uses InvariantCulture
        return double.Parse(numStr, CultureInfo.InvariantCulture);
    }
}";






            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DoubleParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public double {|PS0002:TestMethod|}(string numStr)
    {
        return double.Parse(numStr);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task IntParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int {|PS0002:TestMethod|}(string numStr)
    {
        return int.Parse(numStr);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task LongParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public long {|PS0002:TestMethod|}(string numStr)
    {
        return long.Parse(numStr);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ByteParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public byte {|PS0002:TestMethod|}(string numStr)
    {
        return byte.Parse(numStr);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task DecimalParse_CurrentCulture_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public decimal {|PS0002:TestMethod|}(string numStr)
    {
        return decimal.Parse(numStr);
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
