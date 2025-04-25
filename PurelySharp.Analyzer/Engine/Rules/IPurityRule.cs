using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic; // Required for IEnumerable
using System.Collections.Immutable; // Potentially needed later for immutable collections in rules
using PurelySharp.Analyzer.Engine; // Required for PurityAnalysisResult
using System; // Required for HashSet, Dictionary

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Represents a rule that checks if a specific operation violates method purity.
    /// Rules are executed within the context of a Control Flow Graph analysis.
    /// </summary>
    internal interface IPurityRule
    {
        /// <summary>
        /// Checks if the given operation violates purity according to this rule.
        /// </summary>
        /// <param name="operation">The operation to check.</param>
        /// <param name="context">The analysis context containing relevant semantic information and state.</param>
        /// <returns>
        /// A PurityAnalysisResult indicating whether the operation is pure according to this rule.
        /// Returns PurityAnalysisResult.Pure if pure, or PurityAnalysisResult.Impure(syntaxNode) if impure.
        /// </returns>
        PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context);

        /// <summary>
        /// Gets the specific kinds of operations this rule applies to.
        /// The analysis engine will only invoke this rule for operations matching these kinds.
        /// </summary>
        IEnumerable<OperationKind> ApplicableOperationKinds { get; }
    }
}