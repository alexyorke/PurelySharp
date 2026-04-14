using System.Threading.Tasks;
using PurelySharp.Attributes;
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

        [Test]
        public async Task RequiredFields_PureMethod_NoDiagnostic()
        {
            var test = @"
#nullable enable
using PurelySharp.Attributes;

namespace TestNamespace;

public class Configuration
{
    public required string ApiKey;
    public required string ApiEndpoint;

    [EnforcePure]
    public string GetConfigSummary()
    {
        return $""API Key: {ApiKey.Substring(0, 3)}***, Endpoint: {ApiEndpoint}"";
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RequiredMembers_WithPrimaryConstructor_ReportPureGetterSuggestions()
        {
            var test = @"
#nullable enable
using PurelySharp.Attributes;

namespace TestNamespace;

public class Product(string name, decimal price)
{
    public required string {|PS0004:Name|} { get; init; } = name;
    public required decimal {|PS0004:Price|} { get; init; } = price;

    [EnforcePure]
    public string GetFormattedPrice()
    {
        return $""{Name}: {Price}"";
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RequiredMembers_WithSetsRequiredMembersConstructor_ReportPureSuggestions()
        {
            var test = @"
#nullable enable
using System.Diagnostics.CodeAnalysis;
using PurelySharp.Attributes;

namespace TestNamespace;

public record User
{
    public required string {|PS0004:Name|} { get; init; }
    public required string {|PS0004:Email|} { get; init; }

    [SetsRequiredMembers]
    public {|PS0004:User|}(string name, string email)
    {
        Name = name;
        Email = email;
    }

    [EnforcePure]
    public string GetUserInfo()
    {
        return $""{Name} ({Email})"";
    }
}
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
