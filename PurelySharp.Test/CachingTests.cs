using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
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
