using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using PurelySharp.Analyzer;
using PurelySharp.Attributes;
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
        public void CompilationPurityService_ReusesLazyCallGraphAcrossRepeatedPurityRequests()
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(@"
using PurelySharp.Attributes;

public class TestClass
{
    [EnforcePure]
    public int Caller1() => Shared();

    [EnforcePure]
    public int Caller2() => Shared();

    private int Shared() => 42;
}");
            var compilation = CSharpCompilation.Create(
                "RepeatedPurityRequestCachingTest",
                new[] { syntaxTree },
                new[]
                {
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(EnforcePureAttribute).Assembly.Location)
                },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            var enforcePureAttributeSymbol = compilation.GetTypeByMetadataName(typeof(EnforcePureAttribute).FullName!)!;
            var testClass = compilation.GetTypeByMetadataName("TestClass")!;
            var caller1 = testClass.GetMembers("Caller1").OfType<IMethodSymbol>().Single();
            var caller2 = testClass.GetMembers("Caller2").OfType<IMethodSymbol>().Single();

            var serviceType = typeof(PurelySharpAnalyzer).Assembly.GetType("PurelySharp.Analyzer.Engine.CompilationPurityService", throwOnError: true)!;
            var service = Activator.CreateInstance(
                serviceType,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                args: new object[] { compilation },
                culture: null);
            var callGraphField = serviceType.GetField("_callGraph", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var getPurityMethod = serviceType.GetMethod("GetPurity", BindingFlags.Instance | BindingFlags.Public)!;

            Assert.That(callGraphField.GetValue(service), Is.Null);

            var firstResult = getPurityMethod.Invoke(service, new object?[] { caller1, semanticModel, enforcePureAttributeSymbol, null });
            var firstCallGraph = callGraphField.GetValue(service);

            Assert.That(firstResult, Is.Not.Null);
            Assert.That(firstCallGraph, Is.Not.Null);

            var secondResult = getPurityMethod.Invoke(service, new object?[] { caller2, semanticModel, enforcePureAttributeSymbol, null });

            Assert.That(secondResult, Is.Not.Null);
            Assert.That(callGraphField.GetValue(service), Is.SameAs(firstCallGraph));
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
