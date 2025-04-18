using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper; // Assuming you have a TestHelper library
using PurelySharp; // Reference to your analyzer project
using System.Diagnostics.Contracts; // Add this for PureAttribute

namespace PurelySharp.Tests
{
    [TestClass]
    public class SystemDiagnosticsPureAttributeTests : CodeFixVerifier
    {
        // No diagnostics expected to show up
        [TestMethod]
        public void TestPureMethodWithSystemDiagnosticsPureAttribute()
        {
            var test = @"
    using System;
    using PurelySharp.Attributes;
    using System.Diagnostics.Contracts;

    namespace ConsoleApplication1
    {
        class TypeName
        {
            [Pure]
            public int PureMethod(int x)
            {
                return x * 2;
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        // Diagnostics expected to show up
        [TestMethod]
        public void TestImpureMethodWithSystemDiagnosticsPureAttribute()
        {
            var test = @"
    using System;
    using PurelySharp.Attributes;
    using System.Diagnostics.Contracts;

    namespace ConsoleApplication1
    {
        class TypeName
        {
            private static int _field = 0;

            [Pure]
            public int ImpureMethod(int x)
            {
                _field = x; // Impure operation: static field assignment
                return x * 2;
            }
        }
    }";
            var expected = new DiagnosticResult
            {
                Id = "PMA0001",
                Message = String.Format("Method '{0}' is marked as pure but contains impure operations", "ImpureMethod"),
                Severity = DiagnosticSeverity.Error,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 14, 17) // Adjust line and column as needed
                        }
            };

            VerifyCSharpDiagnostic(test, expected);
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new PurelySharpAnalyzer();
        }

        // If you have code fixes, override GetCSharpCodeFixProvider()
        // protected override CodeFixProvider GetCSharpCodeFixProvider()
        // {
        //     return new PurelySharpCodeFixProvider();
        // }
    }
} 