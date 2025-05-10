using Microsoft.CodeAnalysis.Testing;
using NUnit.Framework;
using System.Threading.Tasks;
using PurelySharp.Analyzer;
using VerifyCS = PurelySharp.Test.CSharpAnalyzerVerifier<
    PurelySharp.Analyzer.PurelySharpAnalyzer>;
using PurelySharp.Attributes;
using System.Collections.Generic;
using System;
using System.Linq;

namespace PurelySharp.Test
{
    [TestFixture]
    public class GenericsInteractionTests
    {
        [Test]
        public async Task GenericClassWithPureOperations_NoDiagnostic()
        {
            // This test also seemed to pass correctly when run individually.
            var test = @"
using PurelySharp.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;

public class Repository<T>
{
    private readonly List<T> _items;
    public Repository(IEnumerable<T> initialItems) { _items = new List<T>(initialItems ?? Enumerable.Empty<T>()); }

    [EnforcePure]
    public T FindItem(Predicate<T> match) => _items.Find(match);

    [EnforcePure]
    public int GetCount() => _items.Count;

    [EnforcePure]
    public IEnumerable<T> GetAll() => _items.ToList();

    [EnforcePure] // Analyzer considers List<T>.Contains pure
    public bool ContainsItem(T item) => _items.Contains(item);
}

public class GenericTestManager
{
    private readonly Repository<string> _stringRepo = new Repository<string>(new[] { ""apple"", ""banana"", ""cherry"" });
    private readonly Repository<int> _intRepo = new Repository<int>(new[] { 1, 2, 3, 5, 8 });

    [EnforcePure]
    public string FindStringStartingWithB() => _stringRepo.FindItem(s => s.StartsWith(""b""));

    [EnforcePure]
    public int GetTotalItemCount() => _stringRepo.GetCount() + _intRepo.GetCount();

    [EnforcePure]
    public bool HasBanana()
    {
        var allStrings = _stringRepo.GetAll();
        return allStrings.Contains(""banana"");
    }
}
";
            // UPDATE: Expect PS0002 on GetAll, HasBanana and .ctor
            var expectedGetAll = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                        .WithSpan(19, 27, 19, 33) // Span of 'GetAll'
                                        .WithArguments("GetAll");
            var expectedHasBanana = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedRule)
                                          .WithSpan(37, 17, 37, 26) // Span of 'HasBanana'
                                          .WithArguments("HasBanana");

            await VerifyCS.VerifyAnalyzerAsync(test, expectedGetAll, expectedHasBanana);
        }

        [Test]
        public async Task GenericRepositoryWithImpureAction_Diagnostic()
        {
            var test = @"
using PurelySharp.Attributes;
using System;
using System.Collections.Generic;

public class Repository<T>
{
    private readonly List<T> _items = new List<T>();

    [EnforcePure] // Assumed pure previously
    public IEnumerable<T> GetAll() => _items; // Previously flagged, assume still flagged

    [EnforcePure] // Assumed pure previously
    public bool ContainsItem(T item) => _items.Contains(item); // Previously flagged, assume still flagged

    [EnforcePure] // New method with impurity
    public void AddAndLog(T item)
    {
        _items.Add(item); // Impure list modification
        Console.WriteLine($""Added item: {item}""); // Impure logging
    }
}
";
            // Expect diagnostics on:
            // 1. GetAll (consistent with previous findings for _items.ToList() or similar)
            // 2. ContainsItem (consistent with previous findings for _items.Contains())
            // 3. AddAndLog (due to List.Add and Console.WriteLine)
            var expectedAddAndLog = VerifyCS.Diagnostic(PurelySharpDiagnostics.PurityNotVerifiedId)
                                            .WithSpan(17, 17, 17, 26) // Span of 'AddAndLog'
                                            .WithArguments("AddAndLog");

            await VerifyCS.VerifyAnalyzerAsync(test,
                                             expectedAddAndLog);
        }
    }
}