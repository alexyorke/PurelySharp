namespace PurelySharp.Analyzer.Engine.Strategies
{
    /// <summary>
    /// Defines the interface for a purity analysis strategy.
    /// </summary>
    internal interface IPurityAnalysisStrategy
    {
        // TODO: Define methods relevant to the strategy, e.g.,
        // bool IsOperationAllowed(IOperation operation, PurityAnalysisContext context);
        // PurityLevel GetDefaultPurity(ISymbol symbol, PurityAnalysisContext context);
    }

    /// <summary>
    /// Implements the default purity analysis strategy.
    /// </summary>
    internal class DefaultPurityStrategy // : IPurityAnalysisStrategy // TODO: Implement interface
    {
        // TODO: Implement strategy logic
    }
} 