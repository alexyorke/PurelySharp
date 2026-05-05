using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Test
{
    [TestFixture]
    public class ConstantsTests
    {
        [Test]
        public void StaticConstructor_DoesNotThrow_WhenInitialized()
        {
            var pureMembers = Constants.KnownPureBCLMembers;
            var impureMethods = Constants.KnownImpureMethods;

            Assert.That(pureMembers, Is.Not.Null, "KnownPureBCLMembers should be loaded.");
            Assert.That(impureMethods, Is.Not.Null, "KnownImpureMethods should be loaded.");

            var overlappingMethods = pureMembers.Intersect(impureMethods).ToList();

            Assert.That(overlappingMethods, Is.Empty,
                $"KnownImpureMethods and KnownPureBCLMembers should not overlap. Found overlaps: {string.Join(", ", overlappingMethods)}");
        }

        [Test]
        public void RepresentativeCatalogSignaturesResolveAgainstNet80References()
        {
            var source = @"
using System;
using System.Collections.Generic;

public static class CatalogSignatureSamples
{
    public static int Sample()
    {
        var list = new List<int>();
        list.Add(1);
        var now = DateTime.Now;
        return Array.Empty<int>().Length + list.Count + now.Day;
    }
}";
            var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
            var compilation = CSharpCompilation.Create(
                "CatalogSignatureResolution",
                new[] { syntaxTree },
                GetTrustedPlatformReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            Assert.That(GetInvocationSignature(compilation, syntaxTree, "list.Add(1)"), Is.EqualTo("System.Collections.Generic.List<T>.Add(T)"));
            Assert.That(Constants.KnownImpureMethods, Does.Contain(GetInvocationSignature(compilation, syntaxTree, "list.Add(1)")));

            Assert.That(GetPropertySignature(compilation, syntaxTree, "DateTime.Now"), Is.EqualTo("System.DateTime.Now.get"));
            Assert.That(Constants.KnownImpureMethods, Does.Contain(GetPropertySignature(compilation, syntaxTree, "DateTime.Now")));

            Assert.That(GetInvocationSignature(compilation, syntaxTree, "Array.Empty<int>()"), Is.EqualTo("System.Array.Empty<T>()"));
            Assert.That(Constants.KnownPureBCLMembers, Does.Contain(GetInvocationSignature(compilation, syntaxTree, "Array.Empty<int>()")));

            Assert.That(GetPropertySignature(compilation, syntaxTree, "list.Count"), Is.EqualTo("System.Collections.Generic.List<T>.Count.get"));
            Assert.That(Constants.KnownPureBCLMembers, Does.Contain(GetPropertySignature(compilation, syntaxTree, "list.Count")));
        }

        private static string GetInvocationSignature(Compilation compilation, SyntaxTree syntaxTree, string expressionText)
        {
            var invocation = syntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Single(node => node.ToString() == expressionText);
            var symbol = compilation.GetSemanticModel(syntaxTree).GetSymbolInfo(invocation).Symbol;
            Assert.That(symbol, Is.Not.Null, "Invocation should resolve: " + expressionText);
            return symbol!.OriginalDefinition.ToDisplayString();
        }

        private static string GetPropertySignature(Compilation compilation, SyntaxTree syntaxTree, string expressionText)
        {
            var memberAccess = syntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<MemberAccessExpressionSyntax>()
                .Single(node => node.ToString() == expressionText);
            var symbol = compilation.GetSemanticModel(syntaxTree).GetSymbolInfo(memberAccess).Symbol;
            Assert.That(symbol, Is.Not.Null, "Property should resolve: " + expressionText);

            var signature = symbol!.OriginalDefinition.ToDisplayString();
            return signature.EndsWith(".get", StringComparison.Ordinal) || signature.EndsWith(".set", StringComparison.Ordinal)
                ? signature
                : signature + ".get";
        }

        private static ImmutableArray<MetadataReference> GetTrustedPlatformReferences()
        {
            var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            if (string.IsNullOrWhiteSpace(trustedPlatformAssemblies))
            {
                return ImmutableArray.Create<MetadataReference>(
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(typeof(Console).Assembly.Location));
            }

            return trustedPlatformAssemblies
                .Split(Path.PathSeparator)
                .Select(path => MetadataReference.CreateFromFile(path))
                .Cast<MetadataReference>()
                .ToImmutableArray();
        }
    }
}
