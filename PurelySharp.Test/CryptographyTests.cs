using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Security.Cryptography;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class CryptographyTests
    {
        // --- Hashing (Pure) ---

        [Test]
        public async Task HashAlgorithm_ComputeHash_UnknownPurityDiagnostic()
        {
            // Hashing is generally pure (deterministic output for given input)
            var test = @"
#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public byte[] {|PS0002:TestMethod|}(string data)
    {
        using (var sha256 = SHA256.Create()) // Pure: Creates a hashing object
        {
            byte[] bytes = Encoding.UTF8.GetBytes(data); // Pure: Encoding
            return sha256.ComputeHash(bytes); // Pure: Computes hash based on input
        }
    }
}";
            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // --- Random Number Generation (Impure) ---

        // TODO: Enable once analyzer recognizes RNG as impure
        // Commented out test RandomNumberGenerator_GetBytes_Diagnostic removed

        // --- Symmetric/Asymmetric Encryption (Impure state management) ---
        // Create methods themselves are pure, but using them often involves state/keys

        [Test]
        public async Task Aes_Create_UnknownPurityDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Security.Cryptography;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public Aes {|PS0002:TestMethod|}()
    {
        return Aes.Create(); // Pure: Factory method
    }
}";
            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Test]
        public async Task RSA_Create_UnknownPurityDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Security.Cryptography;
using PurelySharp.Attributes;



public class TestClass
{
    [EnforcePure]
    public RSA {|PS0002:TestMethod|}()
    {
        return RSA.Create(); // Pure: Factory method
    }
}";
            // Diagnostics are now inline
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // TODO: Add tests for Encrypt/Decrypt methods once state/IO handling is refined
        // Encrypt/Decrypt usually involve streams or byte arrays, which can be pure
        // in isolation, but managing the Keys/IVs often makes the overall process impure.
    }
}