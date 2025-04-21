using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using PurelySharp.Attributes;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class RequiredMembersTests
    {
        // Common attribute definitions for required members tests
        private const string AttributeDefinitions = @"
// Required for required members
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName) => FeatureName = featureName;
        public string FeatureName { get; }
    }
    
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
    public sealed class RequiredMemberAttribute : Attribute { }
    
    internal static class IsExternalInit { }
}

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
    public sealed class SetsRequiredMembersAttribute : Attribute { }
}";

        [Test]
        public async Task ClassWithRequiredMembers_PureMethod_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using PurelySharp.Attributes;
" + AttributeDefinitions + @"

namespace TestNamespace
{
    public class Person
    {
        public required string FirstName { get; init; }
        public required string LastName  { get; init; }
        
        [EnforcePure]
        public string {|PS0002:GetFullName|}()
        {
            return $""{FirstName} {LastName}"";
        }
    }

    public class Client
    {
        public string GetPersonInfo()
        {
            // Using object initializer with required members
            var person = new Person
            {
                FirstName = ""John"",
                LastName  = ""Doe""
            };
            
            return person.GetFullName();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RecordWithRequiredMembers_PureMethod_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using PurelySharp.Attributes;
" + AttributeDefinitions + @"

namespace TestNamespace
{
    public record Person
    {
        public required string FirstName { get; init; }
        public required string LastName  { get; init; }
        
        [EnforcePure]
        public string {|PS0002:GetFullName|}()
        {
            return $""{FirstName} {LastName}"";
        }
    }

    public class Client
    {
        public string GetPersonInfo()
        {
            // Using object initializer with required members
            var person = new Person
            {
                FirstName = ""John"",
                LastName  = ""Doe""
            };
            
            return person.GetFullName();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task StructWithRequiredMembers_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

// Required for required members
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName) => FeatureName = featureName;
        public string FeatureName { get; }
    }
    
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
    internal sealed class RequiredMemberAttribute : Attribute { }
    
    internal static class IsExternalInit { }
}

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    internal sealed class SetsRequiredMembersAttribute : Attribute { }
}

namespace TestNamespace
{
    public struct Point
    {
        public required double X { get; init; }
        public required double Y { get; init; }
        
        [EnforcePure]
        public double {|PS0002:CalculateDistance|}()
        {
            return Math.Sqrt(X * X + Y * Y);
        }
    }

    public class GeometryCalculator
    {
        public double CalculatePointDistance()
        {
            // Using object initializer with required members in a struct
            var point = new Point
            {
                X = 3.0,
                Y = 4.0
            };
            
            return point.CalculateDistance();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ClassWithRequiredFields_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

// Required for required members
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName) => FeatureName = featureName;
        public string FeatureName { get; }
    }
    
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
    internal sealed class RequiredMemberAttribute : Attribute { }
}

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    internal sealed class SetsRequiredMembersAttribute : Attribute { }
}

namespace TestNamespace
{
    public class Configuration
    {
        public required string ApiKey;
        public required string ApiEndpoint;
        
        [EnforcePure]
        public string {|PS0002:GetConfigSummary|}()
        {
            return $""API Key: {ApiKey.Substring(0, 3)}***, Endpoint: {ApiEndpoint}"";
        }
    }

    public class ApiClient
    {
        public string GetConfigInfo()
        {
            // Using object initializer with required fields
            var config = new Configuration
            {
                ApiKey      = ""abc123xyz456"",
                ApiEndpoint = ""https://api.example.com""
            };
            
            return config.GetConfigSummary();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ClassWithRequiredMembers_ImpureMethod()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;
using System.IO;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

// Required for required members
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    internal sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string featureName) => FeatureName = featureName;
        public string FeatureName { get; }
    }
    
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
    internal sealed class RequiredMemberAttribute : Attribute { }
    
    internal static class IsExternalInit { }
}

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    internal sealed class SetsRequiredMembersAttribute : Attribute { }
}

namespace TestNamespace
{
    public class UserProfile
    {
        private static int _lastId = 0;
        public required string Username { get; init; }
        public required string Email    { get; init; }
        
        [EnforcePure]
        public int {|PS0002:GenerateUniqueId|}()
        {
            return Guid.NewGuid().GetHashCode();
        }
        
        [EnforcePure]
        public void {|PS0002:SaveProfile|}()
        {
            // Impure operation: Writes to file
            File.WriteAllText($""{_lastId}.json"", $""{Username} - {Email}"");
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task MutableRequiredProperties_ImpureMethod()
        {
            var test = @"
#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using PurelySharp.Attributes;
" + AttributeDefinitions + @"

namespace TestNamespace
{
    public class Counter
    {
        public required int Count { get; set; } // Mutable required property

        [EnforcePure]
        public void {|PS0002:Increment|}()
        {
            Count++; // Modification of state
        }
    }

    public class Client
    {
        public void UseCounter()
        {
            var counter = new Counter { Count = 0 };
            counter.Increment(); // This method is impure
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task MutableInitOnlyRequiredProperties_ImpureMethod()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
" + AttributeDefinitions + @"

namespace TestNamespace
{
    public class Product
    {
        public required int    Id    { get; init; }
        public required string Name  { get; init; }
        public required decimal Price { get; init; }

        [SetsRequiredMembers]
        public Product(int id, string name, decimal price)
        {
            Id    = id;
            Name  = name;
            Price = price;
        }

        [EnforcePure] 
        public string {|PS0002:GetProductSummary|}()
        {
            return $""{Name} (ID: {Id}) - {Price:C}"";
        }
    }

    public class ProductManager
    {
        [EnforcePure]
        public void {|PS0002:UpdateProductName|}(Product product, string newName)
        {
            Console.WriteLine($""Updating name to {newName}"");
        }

        public string GetProductSummary(Product product)
        {
            var p = new Product(1, ""Sample"", 9.99m);
            return p.GetProductSummary();
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RequiredMembersWithNullCheck_PureMethod_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using PurelySharp.Attributes;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
" + AttributeDefinitions + @"

namespace TestNamespace
{
    public class Document
    {
        public required string Title       { get; init; }
        public string?        Description { get; init; }
        
        [EnforcePure]
        public string {|PS0002:GetSummary|}()
        {
            return $""Document: {Title}, {Description ?? ""No description provided""}"";
        }
    }

    public class DocumentManager
    {
        public string GetDocumentInfo()
        {
            var doc = new Document
            {
                Title = ""Annual Report""
            };

            return doc.GetSummary();
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RequiredMembers_ReadingInPureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
" + AttributeDefinitions + @"

namespace TestNamespace
{
    public class Config
    {
        public required string ApiKey      { get; init; }
        public required string ApiEndpoint { get; init; }
        
        [EnforcePure]
        public string {|PS0002:GetFullEndpoint|}()
        {
            return $""{ApiEndpoint}?key={ApiKey}"";
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RequiredMembers_InRecord_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
" + AttributeDefinitions + @"

namespace TestNamespace
{
    public record User
    {
        [SetsRequiredMembers]
        public User(string name, string email)
        {
            Name  = name;
            Email = email;
        }
        
        public required string Name  { get; init; }
        public required string Email { get; init; }
        
        [EnforcePure]
        public string {|PS0002:GetUserInfo|}()
        {
            return $""{Name} ({Email})"";
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RequiredMembers_WithStructs_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
" + AttributeDefinitions + @"

namespace TestNamespace
{
    public struct Point
    {
        public required int X { get; init; }
        public required int Y { get; init; }
        
        [EnforcePure]
        public double {|PS0002:GetDistance|}()
        {
            return Math.Sqrt(X * X + Y * Y);
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RequiredMembers_WithPrimaryConstructor_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
" + AttributeDefinitions + @"

namespace TestNamespace
{
    public class Product(string name, decimal price)
    {
        public required string  Name  { get; init; } = name;
        public required decimal Price { get; init; } = price;
        
        [EnforcePure]
        public string {|PS0002:GetFormattedPrice|}()
        {
            return $""{Name}: ${Price:F2}"";
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RequiredMembers_MultipleTypes_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
" + AttributeDefinitions + @"

namespace TestNamespace
{
    public record Settings
    {
        public required int TimeoutMs { get; init; }
    }

    public struct Coordinates
    {
        public required double Latitude  { get; init; }
        public required double Longitude { get; init; }
    }

    public class AppConfiguration
    {
        public required Settings    AppSettings     { get; init; }
        public required Coordinates DefaultLocation { get; init; }

        [EnforcePure]
        public string {|PS0002:GetSummary|}()
        {
            return $""Timeout: {AppSettings.TimeoutMs}ms, Location: ({DefaultLocation.Latitude}, {DefaultLocation.Longitude})"";
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RequiredMembers_TryingToModify_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
" + AttributeDefinitions + @"

namespace TestNamespace
{
    public class UserProfile
    {
        public required string Username { get; init; }
        public int Age { get; set; } // Mutable property

        [SetsRequiredMembers]
        public UserProfile(string username)
        {
            Username = username;
        }

        [EnforcePure]
        public void {|PS0002:UpdateAge|}(int newAge)
        {
            this.Age = newAge; // Modifying state - impure
        }

        [EnforcePure]
        public string {|PS0002:GetProfileInfo|}()
        {
            return $""User: {Username}, Age: {Age}"";
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
