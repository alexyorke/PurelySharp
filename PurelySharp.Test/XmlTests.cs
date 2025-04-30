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
using System;
using PurelySharp.Attributes;
using System.Xml.Linq;

public class TestClass
{
    [EnforcePure]
    public XDocument TestMethod(string xml)
    {
        // XDocument.Parse is now known pure
        return XDocument.Parse(xml);
    }
}
";
            // Expect no diagnostics as XDocument.Parse is now known pure
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // --- LINQ to XML In-Memory Operations (Pure) ---

        [Test]
        public async Task LinqToXml_Add_Impure_Diagnostic()
        {
            var test = @"
using System;
using PurelySharp.Attributes;
using System.Xml.Linq;
using System.Linq;

public class TestClass
{
    [EnforcePure]
    public string TestMethod()
    {
        XElement root = new XElement(""Root"");
        root.Add(new XElement(""Child1"", ""Value1"")); // Add is impure
        root.Add(new XAttribute(""Attr1"", ""ValA""));  // Add is impure

        return root.Elements(""Child1"").First().Value; // Elements/First/Value are pure
    }
}
";
            // This involves known impure calls (XElement.Add).
            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                    .WithSpan(10, 19, 10, 29) // UPDATED Span to 'TestMethod' identifier
                                    .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
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
                                   .WithSpan(10, 21, 10, 31) // UPDATED Span to 'TestMethod' identifier
                                   .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, new[] { expected });
        }
    }
}