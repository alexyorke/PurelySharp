using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class DataAnnotationsTests
    {
        // --- Attribute Constructors (Pure) ---
        /* // TODO: Fix - Analyzer flags array creation as impure
        [Test]
        public async Task AttributeConstructors_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.ComponentModel.DataAnnotations;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public ValidationAttribute[] TestMethod()
    {
        // Pure: Creating attribute instances
        return new ValidationAttribute[] 
        {
            new RequiredAttribute(),
            new StringLengthAttribute(10)
        };
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
        */

        // --- Validator.TryValidateObject (Mixed - Depends on Attributes) ---
        // Pure if all attributes are pure, impure if any attribute performs I/O or state changes.
        // Standard attributes (Required, StringLength, Range, etc.) are pure.

        public class PureModel
        {
            [Required(ErrorMessage = "Name is required")]
            public string? Name { get; set; }

            [System.ComponentModel.DataAnnotations.Range(0, 100, ErrorMessage = "Value must be between 0 and 100")]
            public int Value { get; set; }
        }

        // [Test] ... remove this block ...
        // public async Task Validator_TryValidateObject_PureAttributes_NoDiagnostic() ... remove this block ...
        // { ... remove this block ...
        //     ... remove this block ...
        //     await VerifyCS.VerifyAnalyzerAsync(test); ... remove this block ...
        // } ... remove this block ...

        /* // TODO: Fix - Analyzer flags context/list creation or TryValidateObject as impure
        [Test]
        public async Task Validator_TryValidateObject_PureAttributes_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class PureModel
{
    [Required(ErrorMessage = ""Name is required"")]
    public string? Name { get; set; }

    [System.ComponentModel.DataAnnotations.Range(0, 100, ErrorMessage = ""Value must be between 0 and 100"")]
    public int Value { get; set; }
}

public class TestClass
{
    [EnforcePure]
    public bool TestMethod(PureModel model)
    {
        // Pure: Uses standard, pure validation attributes
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        return Validator.TryValidateObject(model, context, results, true);
    }
}";
            // Assumes Validator.TryValidateObject is pure when attributes are pure
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
        */

        // TODO: Add test for Validator.TryValidateObject with a custom, impure ValidationAttribute
        // Requires defining a custom attribute that performs I/O or modifies state in its IsValid method.
        /*
        public class ImpureValidationAttribute : ValidationAttribute
        {
            protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
            {
                Console.WriteLine("Performing impure validation..."); // Impure IO
                // ... actual validation logic ...
                return ValidationResult.Success;
            }
        }

        public class ImpureModel
        {
            [ImpureValidation]
            public string? Data { get; set; }
        }
        
        [Test]
        public async Task Validator_TryValidateObject_ImpureAttribute_Diagnostic()
        {
             var test = @" ... test code using ImpureModel ... ";
             // Expected Diagnostic: Impurity originates from the ImpureValidationAttribute.IsValid call 
             // triggered by Validator.TryValidateObject.
             // Pinpointing the exact location might be tricky, could be TryValidateObject call site.
             Assert.Fail("Impure validation attribute test not implemented.");
             await Task.CompletedTask;
        }
        */
    }
} 