using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using PurelySharp.Analyzer.Engine;
using static PurelySharp.Analyzer.Engine.PurityAnalysisEngine;

namespace PurelySharp.Analyzer.Engine.Rules
{

    internal class ArrayCreationPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.ArrayCreation);

        public PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisState currentState)
        {
            if (!(operation is IArrayCreationOperation arrayCreation))
            {
                return PurityAnalysisResult.Pure;
            }

            LogDebug($"ArrayCreationRule: Analyzing {arrayCreation.Syntax}");


            bool isParamsArray = arrayCreation.Parent is IArgumentOperation argumentOperation &&
                                argumentOperation.Parameter != null &&
                                argumentOperation.Parameter.IsParams;

            if (isParamsArray)
            {



                if (arrayCreation.Initializer != null)
                {
                    foreach (var elementValue in arrayCreation.Initializer.ElementValues)
                    {
                        var elementPurity = CheckSingleOperation(elementValue, context, currentState);
                        if (!elementPurity.IsPure)
                        {
                            LogDebug($"    [ArrCreateRule] 'params' array initializer element '{elementValue.Syntax}' is IMPURE. Operation is Impure.");
                            return PurityAnalysisResult.Impure(elementPurity.ImpureSyntaxNode ?? elementValue.Syntax);
                        }
                    }
                    LogDebug($"    [ArrCreateRule] All 'params' array initializer elements are Pure.");
                }
                else
                {
                    LogDebug($"    [ArrCreateRule] 'params' array has no initializer elements to check.");
                }

                LogDebug($"    [ArrCreateRule] 'params' array creation itself treated as PURE.");
                return PurityAnalysisResult.Pure;
            }
            else
            {

                LogDebug($"    [ArrCreateRule] Array creation '{arrayCreation.Syntax}' is IMPURE (mutable allocation, not for params).");
                return PurityAnalysisResult.Impure(arrayCreation.Syntax);
            }
        }


    }
}