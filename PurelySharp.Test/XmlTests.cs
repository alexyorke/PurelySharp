using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.PurelySharpAnalyzer>;

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

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public XDocument TestMethod(string xmlString)
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

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

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
        return value?.ToUpperInvariant(); // Pure string manipulation
    }
}";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // --- Creating XElements/XAttributes (Pure) ---
        [Test]
        public async Task XElement_Creation_NoDiagnostic()
        {
             var test = @"
#nullable enable
using System;
using System.Xml.Linq;

[AttributeUsage(AttributeTargets.Method)]
public class EnforcePureAttribute : Attribute { }

public class TestClass
{
    [EnforcePure]
    public XElement TestMethod(string name, object content)
    {
        // Pure: Creates in-memory XML structure
        return new XElement(name, content, new XAttribute(""created"", DateTime.Now));
    }
}";
            // Note: DateTime.Now is impure, but we haven't marked it yet.
            // The test focuses on the XElement creation itself.
            // TODO: Update test once DateTime.Now is marked impure.
            await VerifyCS.VerifyAnalyzerAsync(test); 
        }
    }
} 