using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

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
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
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
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
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
" + AttributeDefinitions + @"

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class Person
    {
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        
        [EnforcePure]
        public string GetFullName()
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
                LastName = ""Doe""
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
" + AttributeDefinitions + @"

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public record Person
    {
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        
        [EnforcePure]
        public string GetFullName()
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
                LastName = ""Doe""
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

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public struct Point
    {
        public required double X { get; init; }
        public required double Y { get; init; }
        
        [EnforcePure]
        public double CalculateDistance()
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

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
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

    public class ApiClient
    {
        public string GetConfigInfo()
        {
            // Using object initializer with required fields
            var config = new Configuration
            {
                ApiKey = ""abc123xyz456"",
                ApiEndpoint = ""https://api.example.com""
            };
            
            return config.GetConfigSummary();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task ClassWithRequiredMembers_ImpureMethod_Diagnostic()
        {
            var test = @"
using System;
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

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class UserProfile
    {
        private static int _lastId = 0;
        public required string Username { get; init; }
        public required string Email { get; init; }
        
        [EnforcePure]
        public int GenerateUniqueId()
        {
            // Impure operation: modifies static state
            return ++_lastId;
        }
        
        [EnforcePure]
        public void SaveProfile()
        {
            // Impure operation: file system access
            File.WriteAllText(""test.txt"", Email);
        }
    }
}";

            var expected = new[] {
                VerifyCS.Diagnostic("PMA0001")
                    .WithSpan(44, 22, 44, 29)
                    .WithArguments("GenerateUniqueId"),
                VerifyCS.Diagnostic("PMA0001")
                    .WithSpan(51, 13, 51, 49)
                    .WithArguments("SaveProfile")
            };

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task MutableRequiredProperties_ImpureMethod_Diagnostic()
        {
            var test = @"
using System;
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

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class Counter
    {
        public required int InitialValue { get; set; }
        public required int CurrentValue { get; set; }
        
        [EnforcePure]
        public int Increment()
        {
            // Impure operation: modifies instance property
            return ++CurrentValue;
        }
    }
}";

            var expected = new[] {
                VerifyCS.Diagnostic("PMA0001")
                    .WithSpan(40, 20, 40, 34)
                    .WithArguments("Increment")
            };

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task RequiredMembersWithNullCheck_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
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

#nullable enable
[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class Document
    {
        public required string Title { get; init; }
        public string? Description { get; init; }
        
        [EnforcePure]
        public string GetSummary()
        {
            // Pure operation with null checking on optional property
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
                // Description is not set as it's not required
            };
            
            return doc.GetSummary();
        }
    }
}
#nullable disable";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RequiredMembers_ReadingInPureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
" + AttributeDefinitions + @"

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class Config
    {
        public required string ApiKey { get; init; }
        public required string ApiEndpoint { get; init; }
        
        [EnforcePure]
        public string GetFullEndpoint()
        {
            // Reading required members is fine
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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
" + AttributeDefinitions + @"

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public record User
    {
        [SetsRequiredMembers]
        public User(string name, string email)
        {
            Name = name;
            Email = email;
        }
        
        public required string Name { get; init; }
        public required string Email { get; init; }
        
        [EnforcePure]
        public string GetUserInfo()
        {
            // Reading required members is pure
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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
" + AttributeDefinitions + @"

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public struct Point
    {
        public required int X { get; init; }
        public required int Y { get; init; }
        
        [EnforcePure]
        public double GetDistance()
        {
            // Reading required members in a struct is pure
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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
" + AttributeDefinitions + @"

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class Product(string name, decimal price)
    {
        public required string Name { get; init; } = name;
        public required decimal Price { get; init; } = price;
        
        [EnforcePure]
        public string GetFormattedPrice()
        {
            // Reading required members is pure
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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
" + AttributeDefinitions + @"

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class Customer
    {
        public required string Name { get; init; }
        public required string Email { get; init; }
    }
    
    public class Order
    {
        public required string OrderId { get; init; }
        public required Customer Customer { get; init; }
        public required decimal Total { get; init; }
        
        [EnforcePure]
        public string GetOrderSummary()
        {
            // Reading required members from multiple types is pure
            return $""Order {OrderId} for {Customer.Name} ({Customer.Email}): ${Total:F2}"";
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RequiredMembers_TryingToModify_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
" + AttributeDefinitions + @"

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class Product
    {
        public required string Name { get; set; } // Note: using set, not init
        
        [EnforcePure]
        public void UpdateProductName(string newName)
        {
            // Create a variable to ensure we have a local state change
            // that the analyzer can clearly detect
            var product = this;
            
            // Impure operation: modifying a property even if it's required
            product.Name = newName;
            
            // This is also impure and should be detected
            Name = Name + "" (updated)"";
        }
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(46, 13, 46, 35)
                .WithArguments("UpdateProductName");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}


