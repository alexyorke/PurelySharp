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
        [Test]
        public async Task ClassWithRequiredMembers_PureMethod_NoDiagnostic()
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
    public record Product
    {
        public required string Name { get; init; }
        public required decimal Price { get; init; }
        
        [EnforcePure]
        public string GetFormattedPrice()
        {
            return $""{Name}: ${Price:0.00}"";
        }
    }

    public class ShoppingCart
    {
        public string GetProductInfo()
        {
            // Using object initializer with required members in a record
            var product = new Product
            {
                Name = ""Widget"",
                Price = 9.99m
            };
            
            return product.GetFormattedPrice();
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

// Required for init-only properties
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
    internal static class RequiredMemberAttribute { }
}

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class Person
    {
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public int? Age { get; init; }

        [EnforcePure]
        public string GetFullName()
        {
            // Reading required properties is pure
            return $""{FirstName} {LastName}"";
        }
    }

    public class Application
    {
        [EnforcePure]
        public string GetPersonInfo(string firstName, string lastName, int? age = null)
        {
            // Object initialization with required members
            var person = new Person
            {
                FirstName = firstName,
                LastName = lastName,
                Age = age
            };

            return person.GetFullName() + (age.HasValue ? $"" ({age} years old)"" : """");
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

// Required for init-only properties and records
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
    internal static class RequiredMemberAttribute { }
}

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public record Employee
    {
        public required string Id { get; init; }
        public required string Department { get; init; }
        public string? Title { get; init; }

        [EnforcePure]
        public string GetEmployeeInfo()
        {
            // Reading required properties in a record is pure
            return $""ID: {Id}, Department: {Department}"" + 
                  (Title != null ? $"", Title: {Title}"" : """");
        }
    }

    public class EmployeeManager
    {
        [EnforcePure]
        public string GetEmployeeDetails(string id, string department, string? title = null)
        {
            // Object initialization with required members in a record
            var employee = new Employee
            {
                Id = id,
                Department = department,
                Title = title
            };

            return employee.GetEmployeeInfo();
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

// Required for init-only properties
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
    internal static class RequiredMemberAttribute { }
}

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public readonly struct Point
    {
        public required int X { get; init; }
        public required int Y { get; init; }

        [EnforcePure]
        public double GetDistance()
        {
            // Reading required properties in a struct is pure
            return Math.Sqrt(X * X + Y * Y);
        }
    }

    public class GeometryCalculator
    {
        [EnforcePure]
        public double CalculateDistance(int x, int y)
        {
            // Object initialization with required members in a struct
            var point = new Point
            {
                X = x,
                Y = y
            };

            return point.GetDistance();
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

// Required for init-only properties
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
    internal static class RequiredMemberAttribute { }
}

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    // Combining required members with primary constructor
    public class Configuration(string environment)
    {
        public required string ApiKey { get; init; }
        public string Environment { get; } = environment;
        public string? Region { get; init; }

        [EnforcePure]
        public string GetConnectionString()
        {
            // Reading required property and constructor parameter is pure
            return $""ApiKey={ApiKey};Environment={Environment}"" +
                  (Region != null ? $"";Region={Region}"" : """");
        }
    }

    public class ConfigurationManager
    {
        [EnforcePure]
        public string GetConnectionString(string apiKey, string environment, string? region = null)
        {
            // Using required members with a primary constructor
            var config = new Configuration(environment)
            {
                ApiKey = apiKey,
                Region = region
            };

            return config.GetConnectionString();
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
using System.Collections.Generic;

// Required for init-only properties
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
    internal static class RequiredMemberAttribute { }
}

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class Address
    {
        public required string Street { get; init; }
        public required string City { get; init; }
        public required string ZipCode { get; init; }
        public string? State { get; init; }
    }

    public class Customer
    {
        public required string Name { get; init; }
        public required string Email { get; init; }
        public required Address HomeAddress { get; init; }
        public Address? WorkAddress { get; init; }
    }

    public class CustomerService
    {
        [EnforcePure]
        public string FormatCustomerInfo(Customer customer)
        {
            // Reading required properties from nested types
            var result = $""Name: {customer.Name}\nEmail: {customer.Email}\n"";
            result += $""Home Address: {customer.HomeAddress.Street}, {customer.HomeAddress.City}, {customer.HomeAddress.ZipCode}"";
            
            if (customer.HomeAddress.State != null)
                result += $"", {customer.HomeAddress.State}"";
                
            if (customer.WorkAddress != null)
            {
                result += $""\nWork Address: {customer.WorkAddress.Street}, {customer.WorkAddress.City}, {customer.WorkAddress.ZipCode}"";
                if (customer.WorkAddress.State != null)
                    result += $"", {customer.WorkAddress.State}"";
            }
            
            return result;
        }
        
        [EnforcePure]
        public Customer CreateCustomer(string name, string email, string street, string city, string zipCode, string? state = null)
        {
            // Initializing nested objects with required members
            return new Customer
            {
                Name = name,
                Email = email,
                HomeAddress = new Address
                {
                    Street = street,
                    City = city,
                    ZipCode = zipCode,
                    State = state
                }
            };
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

// Required for init-only properties
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
    internal static class RequiredMemberAttribute { }
}

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    public class Product
    {
        public required string Name { get; set; } // Using set instead of init
        public required decimal Price { get; set; } // Using set instead of init
    }

    public class ProductService
    {
        [EnforcePure]
        public void UpdateProductName(Product product, string newName)
        {
            // Impure operation: modifying a property even if it's required
            product.Name = newName;
        }
    }
}";

            var expected = VerifyCS.Diagnostic("PMA0001")
                .WithSpan(25, 13, 25, 30)
                .WithArguments("UpdateProductName");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}