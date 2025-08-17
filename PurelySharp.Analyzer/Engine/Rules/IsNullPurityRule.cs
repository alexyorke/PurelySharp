﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using static PurelySharp.Analyzer.Engine.PurityAnalysisEngine;

namespace PurelySharp.Analyzer.Engine.Rules
{

    internal class IsNullPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(
            OperationKind.Binary
            );

        public PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisState currentState)
        {
            if (operation is IBinaryOperation binaryOperation &&
                (binaryOperation.OperatorKind == BinaryOperatorKind.Equals || binaryOperation.OperatorKind == BinaryOperatorKind.NotEquals))
            {
                IOperation? operandToCheck = null;
                if (IsNullLiteral(binaryOperation.LeftOperand))
                {
                    operandToCheck = binaryOperation.RightOperand;
                }
                else if (IsNullLiteral(binaryOperation.RightOperand))
                {
                    operandToCheck = binaryOperation.LeftOperand;
                }

                if (operandToCheck != null)
                {
                    LogDebug($"  [IsNullRule] Checking binary null comparison: {operation.Syntax}");
                    var operandResult = CheckSingleOperation(operandToCheck, context, currentState);
                    if (!operandResult.IsPure)
                    {
                        LogDebug($"    [IsNullRule] Operand is Impure: {operandToCheck.Syntax}");
                        return operandResult;
                    }

                    LogDebug($"    [IsNullRule] Operand was pure. Operation is pure. Syntax: '{operation.Syntax?.ToString() ?? "N/A"}'");
                    return PurityAnalysisResult.Pure;
                }
            }



            return PurityAnalysisResult.Pure;
        }

        private bool IsNullLiteral(IOperation operation)
        {
            return operation is ILiteralOperation literal && literal.ConstantValue.HasValue && literal.ConstantValue.Value == null;
        }
    }
}