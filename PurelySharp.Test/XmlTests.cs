using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class XmlTests
    {
        // --- XDocument.Parse (Now considered Impure by default) ---

        [Test]
        public async Task XDocument_Parse_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Xml.Linq;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public XDocument TestMethod(string xmlString)
    {
        // Impure: XDocument.Parse is not explicitly pure
        return XDocument.Parse(xmlString);
    }
}";
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule.Id)
                                   .WithSpan(10, 22, 10, 32)
                                   .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expected });
        }

        // --- LINQ to XML In-Memory Operations (Pure) ---

        [Test]
        public async Task LinqToXml_InMemory_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Linq;
using System.Xml.Linq;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public string? TestMethod(XDocument doc)
    {
        // Pure: In-memory querying and transformation
        var value = doc.Root?
                       .Elements(""Element"")
                       .FirstOrDefault(e => (string?)e.Attribute(""id"") == ""1"")?
                       .Value;
        return value == null ? string.Empty : value.ToUpperInvariant();
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // --- Creating XElements/XAttributes (Pure) ---
        [Test]
        public async Task XElement_Creation_WithImpureDateTime_Diagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Xml.Linq;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public XElement TestMethod(string name, object content)
    {
        // Impure: Creates in-memory XML structure but uses DateTime.Now
        return new XElement(name, content, new XAttribute(""created"", DateTime.Now));
    }
}";
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule.Id)
                                   .WithSpan(10, 21, 10, 31)
                                   .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expected });
        }
    }
}