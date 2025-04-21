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
        // --- XDocument.Parse (Pure) ---

        [Test]
        public async Task XDocument_Parse_NoDiagnostic()
        {
            var test = @"
#nullable enable
using System;
using System.Xml.Linq;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public XDocument {|PS0002:TestMethod|}(string xmlString)
    {
        // Pure: Parses in-memory string
        return XDocument.Parse(xmlString);
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
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
    public string? {|PS0002:TestMethod|}(XDocument doc)
    {
        // Pure: In-memory querying and transformation
        var value = doc.Root?
                       .Elements(""Element"")
                       .FirstOrDefault(e => (string?)e.Attribute(""id"") == ""1"")?
                       .Value;
        return value?.ToUpperInvariant();
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
    public XElement {|PS0002:TestMethod|}(string name, object content)
    {
        // Impure: Creates in-memory XML structure but uses DateTime.Now
        return new XElement(name, content, new XAttribute(""created"", DateTime.Now));
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}