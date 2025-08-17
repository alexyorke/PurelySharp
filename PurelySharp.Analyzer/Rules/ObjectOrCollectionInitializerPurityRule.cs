using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using PurelySharp.Analyzer.Engine.Rules;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace PurelySharp.Analyzer.Engine.Rules
{
    internal class ObjectOrCollectionInitializerPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.ObjectOrCollectionInitializer);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (operation is not IObjectOrCollectionInitializerOperation initializer)
            {

                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"[ObjInitRule] Checking Initializer: {initializer.Syntax}");





            foreach (var initOp in initializer.Initializers)
            {
                IOperation? valueToCheck = null;


                if (initOp is ISimpleAssignmentOperation assignment)
                {
                    valueToCheck = assignment.Value;
                    PurityAnalysisEngine.LogDebug($"[ObjInitRule]  - Checking Assignment Value: {valueToCheck?.Syntax}");
                }

                else if (initOp is IInvocationOperation invocation && invocation.TargetMethod.MethodKind == MethodKind.Constructor)
                {



                    valueToCheck = initOp;
                    PurityAnalysisEngine.LogDebug($"[ObjInitRule]  - Checking Collection Element Constructor: {valueToCheck?.Syntax}");
                }

                else
                {


                    valueToCheck = initOp;
                    PurityAnalysisEngine.LogDebug($"[ObjInitRule]  - Checking Other Initializer Op: {valueToCheck?.Syntax}");
                }

                if (valueToCheck != null)
                {
                    var valueResult = PurityAnalysisEngine.CheckSingleOperation(valueToCheck, context, currentState);
                    if (!valueResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"[ObjInitRule]  -> Initializer value IMPURE: {valueToCheck.Syntax}");
                        return valueResult;
                    }
                    PurityAnalysisEngine.LogDebug($"[ObjInitRule]  -> Initializer value PURE.");
                }
                else
                {
                    PurityAnalysisEngine.LogDebug($"[ObjInitRule]  - Could not determine value to check for initializer: {initOp.Syntax}. Assuming impure for safety.");
                    return PurityAnalysisEngine.ImpureResult(initOp.Syntax);
                }
            }

            PurityAnalysisEngine.LogDebug($"[ObjInitRule] All initializer values pure for: {initializer.Syntax}");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }
    }
}