using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Security.Cryptography;
using System.Threading.Tasks;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class CryptographyTests
    {
        // --- Hashing (Pure) ---

        [Test]
        public async Task HashAlgorithm_ComputeHash_NoDiagnostic()
        {
            // Hashing is generally pure (deterministic output for given input)
            var test = @"
#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public byte[] TestMethod(string data)
    {
        using (var sha256 = SHA256.Create()) // Pure: Creates a hashing object
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data); // Pure: Encoding
            return sha256.ComputeHash(bytes); // Pure: Computes hash based on input
        }
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // --- Random Number Generation (Impure) ---

        // TODO: Enable once analyzer recognizes RNG as impure
        /*
        [Test]
        public async Task RandomNumberGenerator_GetBytes_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Security.Cryptography;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public void TestMethod()
    {
        byte[] randomBytes = new byte[16];
        RandomNumberGenerator.Fill(randomBytes); // Impure: Non-deterministic source
    }
}";
            var expected = VerifyCS.Diagnostic("PMA0001").WithSpan(14, 9, 14, 45).WithArguments("TestMethod");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
        */

        // --- Symmetric/Asymmetric Encryption (Impure state management) ---
        // Create methods themselves are pure, but using them often involves state/keys

        [Test]
        public async Task Aes_Create_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Security.Cryptography;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public Aes TestMethod()
    {
        return Aes.Create(); // Pure: Factory method
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RSA_Create_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Security.Cryptography;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public RSA TestMethod()
    {
        return RSA.Create(); // Pure: Factory method
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // TODO: Add tests for Encrypt/Decrypt methods once state/IO handling is refined
        // Encrypt/Decrypt usually involve streams or byte arrays, which can be pure
        // in isolation, but managing the Keys/IVs often makes the overall process impure.
    }
} 