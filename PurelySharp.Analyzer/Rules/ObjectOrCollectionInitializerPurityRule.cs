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
                // Should not happen
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"[ObjInitRule] Checking Initializer: {initializer.Syntax}");

            // In the context of a 'with' expression, the initializer sets properties on the *new* object.
            // We only need to ensure the *values* being assigned are pure.
            // For standalone initializers (like new List<int> { 1, 2 }), ObjectCreationPurityRule should handle the 'new' part.

            foreach (var initOp in initializer.Initializers)
            {
                IOperation? valueToCheck = null;

                // Common case: Simple assignment like { Property = Value }
                if (initOp is ISimpleAssignmentOperation assignment)
                {
                    valueToCheck = assignment.Value;
                    PurityAnalysisEngine.LogDebug($"[ObjInitRule]  - Checking Assignment Value: {valueToCheck?.Syntax}");
                }
                // Case: Collection initializer like new List<int> { Value1, Value2 }
                else if (initOp is IInvocationOperation invocation && invocation.TargetMethod.MethodKind == MethodKind.Constructor)
                {
                    // This branch might not be strictly necessary if ObjectCreation handles the 'new List' part,
                    // but let's handle values directly added to collections within the initializer just in case.
                    // Example: new List<MyObj> { new MyObj(1) }; // We need to check 'new MyObj(1)'
                    valueToCheck = initOp; // Check the entire constructor invocation used for the element
                    PurityAnalysisEngine.LogDebug($"[ObjInitRule]  - Checking Collection Element Constructor: {valueToCheck?.Syntax}");
                }
                // Other potential initializer kinds (e.g., complex assignments) might need handling
                else
                {
                    // If it's not an assignment or known collection init pattern, maybe it's just the value?
                    // Example: new List<int> { 1, MethodCall() }
                    valueToCheck = initOp;
                    PurityAnalysisEngine.LogDebug($"[ObjInitRule]  - Checking Other Initializer Op: {valueToCheck?.Syntax}");
                }

                if (valueToCheck != null)
                {
                    var valueResult = PurityAnalysisEngine.CheckSingleOperation(valueToCheck, context, currentState);
                    if (!valueResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"[ObjInitRule]  -> Initializer value IMPURE: {valueToCheck.Syntax}");
                        return valueResult; // Propagate the impure result
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