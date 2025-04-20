using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PurelySharp.Analyzer.Rules
{
    /// <summary>
    /// Provides a base implementation for purity rules.
    /// </summary>
    internal abstract class BasePurityRule : IPurityRule
    {
        protected CompilationState? CompilationState { get; private set; }

        public abstract DiagnosticDescriptor Descriptor { get; }

        public virtual void InitializeRule(CompilationState compilationState)
        {
            CompilationState = compilationState;
        }

        public abstract void RegisterActions(CompilationStartAnalysisContext context);
    }
} 