using System.Threading.Tasks;
using NUnit.Framework;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class RequiredMembersSmokeTests
    {
        [Test]
        public async Task RequiredInitOnlyProperties_ReportPureGetterSuggestions()
        {
            var test = @"
#nullable enable
using PurelySharp.Attributes;

namespace TestNamespace;

public class Person
{
    public required string {|PS0004:FirstName|} { get; init; }
    public required string {|PS0004:LastName|} { get; init; }

    [EnforcePure]
    public string GetFullName()
    {
        return $""{FirstName} {LastName}"";
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task MutableRequiredProperty_ReportsGetterSuggestionAndImpureMethod()
        {
            var test = @"
#nullable enable
using PurelySharp.Attributes;

namespace TestNamespace;

public class Counter
{
    public required int {|PS0004:Count|} { get; set; }

    [EnforcePure]
    public void {|PS0002:Increment|}()
    {
        Count++;
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
