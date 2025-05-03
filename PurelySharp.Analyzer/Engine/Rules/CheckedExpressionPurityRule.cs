using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Rule to flag 'checked' context operations. The actual purity check is deferred.
    /// </summary>
    internal class CheckedExpressionPurityRule : IPurityRule
    {
        // Note: There isn't a specific OperationKind for CheckedExpression itself in older Roslyn versions.
        // We might need to rely on syntax analysis or find how CheckedExpression is represented in IOperation tree.
        // Let's assume OperationKind.UnaryOperator might cover some checked cases or it's handled implicitly.
        // Adding OperationKind.Conversion as sometimes checked involves implicit conversions.
        // A more robust approach might involve syntax node analysis if IOperation doesn't expose it directly.
        // For now, let's target unary and conversion as potential hosts.
        // Update: Roslyn seems to represent checked/unchecked expressions via IUnaryOperation (for +/-)
        // and potentially IConversionOperation or others depending on context. Let's stick with Unary for now.
        // It seems `checked()` around binary ops might not directly yield a distinct IOperation kind easily accessible?
        // Let's refine ApplicableOperationKinds later if needed.
        // Let's assume for now CheckedExpression might be part of Unary or Binary directly.
        // Re-evaluating: Checked/Unchecked are distinct OperationKinds in later Roslyn, but not netstandard2.0's Microsoft.CodeAnalysis 3.3.1.
        // Checked *expressions* (like `checked(a+b)`) are represented by IBinaryOperation/IUnaryOperation with IsChecked=true.
        // Checked *statements* (`checked { ... }`) are OperationKind.CheckedStatement.
        // Let's make this rule target Binary and Unary and check the IsChecked flag.

        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(
            OperationKind.Binary,
            OperationKind.Unary
            // Add Conversion if needed
            );

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            // This rule ONLY flags the presence of 'checked'. Purity is handled by Binary/Unary rules.
            bool isChecked = false;
            if (operation is IBinaryOperation binaryOp && binaryOp.IsChecked)
            {
                PurityAnalysisEngine.LogDebug($"    [CheckedRule] Found Binary Operation with IsChecked=true: {operation.Syntax}");
                isChecked = true;
                // Purity determined by BinaryOperationPurityRule, considering operands and operator method (once fixed).
            }
            else if (operation is IUnaryOperation unaryOp && unaryOp.IsChecked)
            {
                PurityAnalysisEngine.LogDebug($"    [CheckedRule] Found Unary Operation with IsChecked=true: {operation.Syntax}");
                isChecked = true;
                // Purity determined by UnaryOperationPurityRule checking the operand.
            }
            // Add Conversion check if needed: else if (operation is IConversionOperation convOp && convOp.IsChecked) ...

            if (isChecked)
            {
                PurityAnalysisEngine.LogDebug($"    [CheckedRule] Operation kind {operation.Kind} uses checked context: {operation.Syntax}. Deferring purity check.");
                // This rule doesn't determine purity itself.
            }

            // Allow other rules (Binary/Unary) to determine purity.
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}