using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes checked/unchecked expressions for purity.
    /// The purity depends entirely on the operand.
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
            OperationKind.UnaryOperator // Covers unary plus/minus which can be checked
                                        // Add OperationKind.Conversion if checked conversions need explicit handling?
            );

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            // This rule currently focuses on identifying if the operation itself has the 'checked' flag.
            // The actual purity check (operands, user-defined methods) is deferred to the specific rules (Binary, Unary).
            // This rule itself doesn't determine purity, just logs if 'checked' is present.

            bool isChecked = false;
            IOperation? operandToCheck = null;

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
                operandToCheck = unaryOp.Operand;
                // Purity determined by UnaryOperationPurityRule checking the operand.
            }
            // Add checks for IConversionOperation.IsChecked if necessary

            if (isChecked)
            {
                PurityAnalysisEngine.LogDebug($"    [CheckedRule] Operation kind {operation.Kind} uses checked context. Deferring actual purity check to specific rule.");
                // We don't return Impure here. The check simply notes the context.
                // The specific rules (Binary, Unary) handle operand/method purity.
            }

            // This rule doesn't override the result; it lets the primary rule (Binary/Unary) decide.
            // We return Pure here to indicate this rule itself doesn't find impurity,
            // allowing the analysis engine to continue with other rules for this operation.
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}