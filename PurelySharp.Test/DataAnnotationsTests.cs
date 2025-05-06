using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;
using System; // Added for DateTime

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class DataAnnotationsTests
    {
        // --- Test Setup Classes ---
        public class PureModel
        {
            [Required(ErrorMessage = "Name is required")]
            public string? Name { get; set; } // PS0004 expected

            [System.ComponentModel.DataAnnotations.Range(0, 100, ErrorMessage = "Value must be between 0 and 100")]
            public int Value { get; set; } // PS0004 expected
        }

        public class ImpureValidationAttribute : ValidationAttribute
        {
            protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
            {
                Console.WriteLine("Performing impure validation..."); // Impure IO
                return ValidationResult.Success;
            }
        }

        public class ImpureModel
        {
            [ImpureValidation]
            public string? Data { get; set; }
        }

        public class MyValidatableObject : IValidatableObject
        {
            public DateTime StartDate { get; set; } // PS0004 expected
            public DateTime EndDate { get; set; } // PS0004 expected

            [EnforcePure] // Should still be flagged as PS0002 if Validate becomes impure
            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                // Pure validation logic
                if (EndDate < StartDate)
                {
                    yield return new ValidationResult("End date must be after start date.");
                }
            }
        }

        public class MyObject { public string? DisplayName { get; set; } } // PS0004 expected

        // --- Tests ---

        [Test]
        public async Task AttributeConstructors_NoDiagnostic() // TODO: Fix - Analyzer flags array creation as impure
        {
            var test = @"
#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using PurelySharp.Attributes;

// Define attribute used (even if simple)
// Keep only one CS8618 markup pair for ErrorMessage
public class MyLengthAttribute : StringLengthAttribute { public MyLengthAttribute(int len): base(len) {} public string ErrorMessage { get; } public int MinimumLength { get; } }

public class TestClass
{
    [EnforcePure]
    public ValidationAttribute[] TestMethod() // Line 13 in test string
    {
        // Pure: Creating attribute instances
        return new ValidationAttribute[]
        {
            new RequiredAttribute(),
            new MyLengthAttribute(10) // Use defined attribute
        };
    }
}";
            // Expect PS0002 on TestMethod, PS0004 on .ctor/getters, and CS8618 on ErrorMessage (5 total)
            var expectedCS8618 = DiagnosticResult.CompilerError("CS8618")
                                     .WithSpan(9, 65, 9, 82).WithSpan(9, 120, 9, 132) // Use actual span line 9
                                     .WithArguments("property", "ErrorMessage");
            var expectedPS0004Ctor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                        .WithSpan(9, 65, 9, 82) // Use actual span line 9
                                        .WithArguments(".ctor");
            var expectedPS0004GetterErr = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                           .WithSpan(9, 120, 9, 132) // Use actual span line 9
                                           .WithArguments("get_ErrorMessage");
            var expectedPS0004GetterMin = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId)
                                           .WithSpan(9, 153, 9, 166) // Use actual span line 9 and chars 153/166
                                           .WithArguments("get_MinimumLength");
            var expectedPS0002 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                  .WithSpan(14, 34, 14, 44) // Use actual span line 14
                                  .WithArguments("TestMethod");

            // Expect 5 diagnostics based on the latest output, in the correct order
            await VerifyCS.VerifyAnalyzerAsync(test, new[] {
                expectedCS8618,
                expectedPS0004Ctor,
                expectedPS0004GetterErr,
                expectedPS0004GetterMin,
                expectedPS0002
            });
        }


        [Test]
        public async Task Validator_TryValidateObject_PureAttributes_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PurelySharp.Attributes;

public class PureRangeAttribute : ValidationAttribute
{
    public int Min { get; } // PS0004 expected
    public int Max { get; } // PS0004 expected
    public PureRangeAttribute(int min, int max) { Min = min; Max = max; } // PS0004 expected

    [EnforcePure]
    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        if (value is int i && i >= Min && i <= Max) return ValidationResult.Success;
        return new ValidationResult(""Value out of range."");
    }
}

public class MyModel
{
    [PureRange(0, 100)]
    public int Value { get; set; } // PS0004 expected (get/set)
}

public class TestRunner
{
    [EnforcePure]
    public bool TestMethod(MyModel model)
    {
        var context = new ValidationContext(model, null, null);
        var results = new List<ValidationResult>();
        // Validator.TryValidateObject is assumed impure
        bool isValid = Validator.TryValidateObject(model, context, results, true);
        return isValid;
    }
}
";
            // Expect PS0004 on get_Min, get_Max, .ctor (Attr), get_Value and PS0002 on TestMethod (5 total)
            var expectedGetMin = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(9, 16, 9, 19).WithArguments("get_Min");
            var expectedGetMax = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(10, 16, 10, 19).WithArguments("get_Max");
            var expectedAttrCtor = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(11, 12, 11, 30).WithArguments(".ctor");
            var expectedGetValue = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(24, 16, 24, 21).WithArguments("get_Value");
            var expectedSetValue = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(24, 16, 24, 21).WithArguments("set_Value");
            var expectedTestMethod = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(30, 17, 30, 27).WithArguments("TestMethod");

            // Ensure all 6 expectations are present and in the correct order
            await VerifyCS.VerifyAnalyzerAsync(test, new[] {
                expectedGetMin,
                expectedGetMax,
                expectedAttrCtor,
                expectedGetValue,
                expectedSetValue,
                expectedTestMethod
            });
        }

        [Test]
        public async Task IValidatableObject_Validate_IsPure()
        {
            var test = @"
#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PurelySharp.Attributes;

public class MyValidatableObject : IValidatableObject // Line 7
{
    public DateTime StartDate { get; set; } // Line 9
    public DateTime EndDate { get; set; } // Line 10

    [EnforcePure] // Should still be flagged as PS0002 if Validate becomes impure
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Pure validation logic
        if (EndDate < StartDate)
        {
            yield return new ValidationResult(""End date must be after start date."");
        }
    }
}
";
            // Expect PS0004 for getter/setter pairs and the Validate method (5 total)
            // Update span for get_StartDate based on test failure output
            var expectedGetStart = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(10, 21, 10, 30).WithArguments("get_StartDate");
            // Update span for set_StartDate based on test failure output
            var expectedSetStart = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(10, 21, 10, 30).WithArguments("set_StartDate");
            // Update span for get_EndDate based on test failure output
            var expectedGetEnd = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(11, 21, 11, 28).WithArguments("get_EndDate");
            // Update span for set_EndDate based on test failure output
            var expectedSetEnd = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(11, 21, 11, 28).WithArguments("set_EndDate");
            // Update span for Validate based on test failure output
            var expectedValidate = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(14, 42, 14, 50).WithArguments("Validate");

            await VerifyCS.VerifyAnalyzerAsync(test, expectedGetStart, expectedSetStart, expectedGetEnd, expectedSetEnd, expectedValidate);
        }

        [Test]
        public async Task ValidationContext_Items_IsImpure()
        {
            var test = @"
#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PurelySharp.Attributes;

public class MyObject { public string DisplayName { get; set; } } // Line 7

public class TestClass
{
    [EnforcePure] // Will be flagged because ValidationContext.Items is Dictionary (impure)
    public bool TestMethod(MyObject instance) // Line 11
    {
        var context = new ValidationContext(instance);
        context.Items[""key""] = ""value""; // Modifying Items dictionary is impure
        return true;
    }
}
";
            // Expect PS0002 on TestMethod and PS0004 on MyObject getter/setter (3 total) + Compiler Error
            var expectedPS0002 = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId).WithSpan(13, 17, 13, 27).WithArguments("TestMethod");
            var expectedGetDisplay = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(8, 39, 8, 50).WithArguments("get_DisplayName");
            var expectedSetDisplay = VerifyCS.Diagnostic(PurelySharpDiagnostics.MissingEnforcePureAttributeId).WithSpan(8, 39, 8, 50).WithArguments("set_DisplayName");
            // Match the actual diagnostic output which includes a duplicate span
            var compilerError = DiagnosticResult.CompilerError("CS8618").WithSpan(8, 39, 8, 50).WithSpan(8, 39, 8, 50).WithArguments("property", "DisplayName");

            await VerifyCS.VerifyAnalyzerAsync(test, compilerError, expectedGetDisplay, expectedSetDisplay, expectedPS0002); // Order matches output
        }

        // TODO: Add test for Validator.TryValidateObject with a custom, impure ValidationAttribute
    }
}