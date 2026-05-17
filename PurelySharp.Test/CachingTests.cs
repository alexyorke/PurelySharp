using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using System;
using System.Reflection;
using System.Threading.Tasks;

using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;

#nullable enable

namespace PurelySharp.Test
{
    [TestFixture]
    public class CachingTests
    {
        [Test]
        public void CompilationPurityService_DoesNotBuildCallGraphInConstructor()
        {
            var syntaxTree = CSharpSyntaxTree.ParseText("public class C { public int M() => 1; }");
            var compilation = CSharpCompilation.Create(
                "LazyCallGraphTest",
                new[] { syntaxTree },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var serviceType = typeof(PurelySharpAnalyzer).Assembly.GetType("PurelySharp.Analyzer.Engine.CompilationPurityService", throwOnError: true)!;
            var service = Activator.CreateInstance(
                serviceType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { compilation },
                culture: null);

            var callGraphField = serviceType.GetField("_callGraph", BindingFlags.Instance | BindingFlags.NonPublic)!;

            Assert.That(callGraphField.GetValue(service), Is.Null);
        }

        [Test]
        public async Task SharedImpureCallee_ReusedAcrossManyCallers_ReportsAllCallers()
        {
            var test = @"
using System;
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public void {|PS0002:ImpureLeaf|}()
    {
        Console.WriteLine(""side effect"");
    }

    [EnforcePure]
    public void {|PS0002:Caller1|}() => ImpureLeaf();

    [EnforcePure]
    public void {|PS0002:Caller2|}() => ImpureLeaf();

    [EnforcePure]
    public void {|PS0002:Caller3|}() => ImpureLeaf();

    [EnforcePure]
    public void {|PS0002:Caller4|}() => ImpureLeaf();

    [EnforcePure]
    public void {|PS0002:Caller5|}() => ImpureLeaf();
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}
