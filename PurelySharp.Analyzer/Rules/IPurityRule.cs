using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PurelySharp.Analyzer.Rules
{
    /// <summary>
    /// Defines the contract for an individual, self-contained purity rule.
    /// </summary>
    internal interface IPurityRule
    {
        /// <summary>
        /// Gets the diagnostic descriptor supported by this rule.
        /// </summary>
        DiagnosticDescriptor Descriptor { get; }

        /// <summary>
        /// Initializes the rule with shared compilation state.
        /// Called once per rule instance per compilation.
        /// </summary>
        /// <param name="compilationState">The shared compilation state.</param>
        void InitializeRule(CompilationState compilationState);

        /// <summary>
        /// Registers the Roslyn analysis actions relevant to this rule.
        /// Called once per rule instance per compilation.
        /// </summary>
        /// <param name="context">The compilation start analysis context.</param>
        void RegisterActions(CompilationStartAnalysisContext context);
    }
} 