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

            await VerifyCS.VerifyAnalyzerAsync(test);
        }



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

            var expected = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                    .WithSpan(10, 19, 10, 29)
                                    .WithArguments("TestMethod");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }


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