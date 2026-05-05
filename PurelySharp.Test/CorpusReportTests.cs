using NUnit.Framework;
using PurelySharp.Tools.CorpusReport;

namespace PurelySharp.Test
{
    [TestFixture]
    public class CorpusReportTests
    {
        [Test]
        public void CreateFromSarifJson_AggregatesPurelySharpCountsAndEvidence()
        {
            var report = SarifCorpusReport.CreateFromSarifJson("sample.sarif", """
{
  "version": "2.1.0",
  "runs": [
    {
      "results": [
        {
          "ruleId": "PS0002",
          "message": { "text": "impure" },
          "properties": {
            "purelysharp.impurity.category": "catalog_hit",
            "purelysharp.impurity.operation_kind": "Invocation",
            "purelysharp.impurity.symbol": "System.Console.WriteLine(string)",
            "purelysharp.impurity.catalog_source": "known_impure_namespace_or_type"
          }
        },
        {
          "ruleId": "PS0002",
          "message": { "text": "unknown dispatch" },
          "properties": {
            "purelysharp.impurity.category": "unknown_external_call",
            "purelysharp.impurity.operation_kind": "Invocation",
            "purelysharp.impurity.symbol": "ITest.Run()"
          }
        },
        {
          "ruleId": "PS0004",
          "message": { "text": "missing purity" }
        },
        {
          "ruleId": "CS0168",
          "message": { "text": "compiler diagnostic ignored" }
        }
      ]
    }
  ]
}
""");

            Assert.That(report.Inputs, Is.EqualTo(new[] { "sample.sarif" }));
            Assert.That(report.Ps0002Count, Is.EqualTo(2));
            Assert.That(report.Ps0004Count, Is.EqualTo(1));
            Assert.That(report.TotalPurelySharpDiagnostics, Is.EqualTo(3));
            Assert.That(report.ImpurityCategories["catalog_hit"], Is.EqualTo(1));
            Assert.That(report.ImpurityCategories["unknown_external_call"], Is.EqualTo(1));
            Assert.That(report.OperationKinds["Invocation"], Is.EqualTo(2));
            Assert.That(report.TopImpureApis[0].Value, Is.EqualTo("ITest.Run()"));
        }

        [Test]
        public void CreateFromSarifJson_IdentifiesCatalogMissesAndFalsePositiveCandidates()
        {
            var report = SarifCorpusReport.CreateFromSarifJson("sample.sarif", """
{
  "version": "2.1.0",
  "runs": [
    {
      "results": [
        {
          "ruleId": "PS0002",
          "properties": {
            "purelysharp.impurity.category": "unknown_external_call",
            "purelysharp.impurity.operation_kind": "Invocation",
            "purelysharp.impurity.symbol": "ExternalLibrary.Hash(byte[])"
          }
        },
        {
          "ruleId": "PS0002",
          "properties": {
            "purelysharp.impurity.category": "dynamic_dispatch",
            "purelysharp.impurity.operation_kind": "Invocation",
            "purelysharp.impurity.symbol": "dynamic.ToString()"
          }
        }
      ]
    }
  ]
}
""");

            Assert.That(report.CatalogMisses, Has.Length.EqualTo(1));
            Assert.That(report.CatalogMisses[0].Value, Is.EqualTo("ExternalLibrary.Hash(byte[])"));
            Assert.That(report.FalsePositiveCandidates, Has.Length.EqualTo(2));
            Assert.That(report.FalsePositiveCandidates.Select(item => item.Category), Does.Contain("unknown_external_call"));
            Assert.That(report.FalsePositiveCandidates.Select(item => item.Category), Does.Contain("dynamic_dispatch"));
        }
    }
}
