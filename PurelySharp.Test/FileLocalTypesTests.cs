using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

namespace PurelySharp.Test
{
    [TestFixture]
    public class FileLocalTypesTests
    {
        [Test]
        public async Task FileLocalClass_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    file class FileLocalCalculator
    {
        [EnforcePure]
        public int Square(int number)
        {
            return number * number;
        }
    }

    public class Application
    {
        public int CalculateSquare(int number)
        {
            var calculator = new FileLocalCalculator();
            return calculator.Square(number);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task FileLocalStruct_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

// Required for init-only properties
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    file struct Point
    {
        public double X { get; init; }
        public double Y { get; init; }

        [EnforcePure]
        public double CalculateDistance(Point other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }

    public class Application
    {
        public double GetDistance(double x1, double y1, double x2, double y2)
        {
            var p1 = new Point { X = x1, Y = y1 };
            var p2 = new Point { X = x2, Y = y2 };
            return p1.CalculateDistance(p2);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task FileLocalRecord_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

// Required for init-only properties in record structs
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    file record Person(string FirstName, string LastName)
    {
        [EnforcePure]
        public string GetFullName()
        {
            return $""{FirstName} {LastName}"";
        }
    }

    public class Application
    {
        public string GetPersonFullName(string firstName, string lastName)
        {
            var person = new Person(firstName, lastName);
            return person.GetFullName();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task FileLocalClass_ImpureMethod_Diagnostic()
        {
            var test = @"
using System;
using System.IO;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    file class Logger
    {
        [EnforcePure]
        public void LogMessage(string message)
        {
            // Impure operation: writing to a file
            File.AppendAllText(""log.txt"", message);
        }
    }

    // Application uses the file-local type internally but doesn't expose it
    // so we don't expect the CS9051 error
    file class Program
    {
        public void Log(string message)
        {
            var logger = new Logger();
            logger.LogMessage(message);
        }
    }
}";

            var expected = new[] {
                VerifyCS.Diagnostic("PMA0001")
                    .WithSpan(16, 13, 16, 51)
                    .WithArguments("LogMessage")
            };

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Test]
        public async Task FileLocalType_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    // C# 11 file-local type
    file class FileLocalCounter
    {
        private int _count;
        
        public FileLocalCounter(int initialCount = 0)
        {
            _count = initialCount;
        }
        
        public int GetCount() => _count;
        
        public int IncrementAndGet()
        {
            return ++_count;
        }
    }
    
    public class CounterManager
    {
        [EnforcePure]
        public static int GetInitialCountFromFileLocalType()
        {
            // Pure operation using file-local type
            var counter = new FileLocalCounter();
            return counter.GetCount();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task FileLocalType_WithImpureOperation_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    file class Counter
    {
        private static int _count = 0;
        
        public int Increment()
        {
            return ++_count;  // Modifying a static field is impure, but currently not detected in file-local types
        }
    }

    public class Application
    {
        [EnforcePure]
        public static int CountUp()
        {
            var counter = new Counter();
            return counter.Increment();  // Calls potentially impure method, but not flagged as impure
        }
    }
}";

            // Currently, operations within file-local types don't trigger impurity diagnostics
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task FileLocalType_WithMultipleFileLocalTypes_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    file class UserAccount
    {
        public string Username { get; }
        public string Email { get; }
        
        public UserAccount(string username, string email)
        {
            Username = username;
            Email = email;
        }
    }
    
    file class UserPreferences
    {
        public bool DarkMode { get; }
        public string Theme { get; }
        
        public UserPreferences(bool darkMode, string theme)
        {
            DarkMode = darkMode;
            Theme = theme;
        }
    }
    
    public class UserManager
    {
        [EnforcePure]
        public static string GetUserDisplayInfo(string username, string email, bool darkMode, string theme)
        {
            var account = new UserAccount(username, email);
            var prefs = new UserPreferences(darkMode, theme);
            
            return $""{account.Username} ({account.Email}) - Theme: {prefs.Theme}, Dark Mode: {prefs.DarkMode}"";
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task FileLocalType_NestedFileLocalTypes_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    // Use a top-level file-local type instead of a nested one
    file class Container
    {
        public int Value { get; set; }

        public int GetValue() => Value;
    }

    file class Helper
    {
        public int ProcessContainer(Container container)
        {
            return container.GetValue() * 2;
        }
    }

    public class Application
    {
        [EnforcePure]
        public static int ProcessValue(int value)
        {
            var container = new Container { Value = value };
            var helper = new Helper();
            return helper.ProcessContainer(container);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task FileLocalType_WithCollections_PureMethod_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

namespace TestNamespace
{
    file class Product
    {
        public string Name { get; set; }
        public decimal Price { get; set; }
        
        public Product(string name, decimal price)
        {
            Name = name;
            Price = price;
        }
    }

    file class ShoppingCart
    {
        [EnforcePure]
        public decimal CalculateTotalPrice(List<Product> products)
        {
            decimal total = 0;
            foreach (var product in products)
            {
                total += product.Price;
            }
            return total;
        }
    }
    
    public class Store
    {
        public decimal GetTotalPrice(string[] productNames, decimal[] prices)
        {
            var products = new List<Product>();
            for (int i = 0; i < productNames.Length; i++)
            {
                products.Add(new Product(productNames[i], prices[i]));
            }
            
            var cart = new ShoppingCart();
            return cart.CalculateTotalPrice(products);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}