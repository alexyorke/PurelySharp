using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes assignments for potential side effects.
    /// </summary>
    internal class AssignmentPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.SimpleAssignment, OperationKind.CompoundAssignment, OperationKind.Increment, OperationKind.Decrement);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context) // Context is available if needed later
        {
            IOperation? targetOperation = null;
            IOperation? valueOperation = null; // Initialize to null
            SyntaxNode diagnosticNode = operation.Syntax; // Default diagnostic node to the operation itself

            if (operation is IAssignmentOperation assignmentOperation)
            {
                targetOperation = assignmentOperation.Target;
                valueOperation = assignmentOperation.Value; // Get the value operation ONLY for assignment
                // Keep diagnosticNode as operation.Syntax for assignments initially
            }
            else if (operation is IIncrementOrDecrementOperation incrementDecrementOperation)
            {
                targetOperation = incrementDecrementOperation.Target;
                // For increment/decrement, the whole operation is the side effect location
                // diagnosticNode remains operation.Syntax
            }
            else
            {
                PurityAnalysisEngine.LogDebug($"AssignmentPurityRule: Unexpected operation type {operation.Kind}. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"AssignmentPurityRule: Analyzing Target {targetOperation?.Kind} in operation {operation.Kind}");

            if (targetOperation == null)
            {
                PurityAnalysisEngine.LogDebug("AssignmentPurityRule: Target operation is null. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure; // Cannot determine target
            }

            // --- Step 1: Check Purity of the Target ---
            PurityAnalysisEngine.PurityAnalysisResult targetPurityResult;
            switch (targetOperation.Kind)
            {
                case OperationKind.LocalReference:
                    PurityAnalysisEngine.LogDebug(" Assignment Target: LocalReference - Pure");
                    targetPurityResult = PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    break;

                case OperationKind.ParameterReference:
                    if (targetOperation is IParameterReferenceOperation paramRef)
                    {
                        // Check for modification of ref/out/in/ref readonly parameters
                        if (paramRef.Parameter.RefKind == RefKind.Ref ||
                            paramRef.Parameter.RefKind == RefKind.Out ||
                            paramRef.Parameter.RefKind == RefKind.In || // Treat 'in' modification as impure target
                            paramRef.Parameter.RefKind == RefKind.RefReadOnly) // Treat 'ref readonly' modification as impure target
                        {
                            PurityAnalysisEngine.LogDebug($" Assignment Target: ParameterReference ({paramRef.Parameter.RefKind}) modification attempt - Impure Target");
                            // Mark target check as impure, but the diagnostic location will be handled below
                            targetPurityResult = PurityAnalysisEngine.PurityAnalysisResult.Impure(targetOperation.Syntax);
                        }
                        else // Value parameters
                        {
                            PurityAnalysisEngine.LogDebug(" Assignment Target: ParameterReference (value) - Pure Target");
                            targetPurityResult = PurityAnalysisEngine.PurityAnalysisResult.Pure;
                        }
                    }
                    else
                    {
                        // Should not happen if Kind is ParameterReference, but handle defensively
                        targetPurityResult = PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }
                    break;

                case OperationKind.FieldReference:
                    if (targetOperation is IFieldReferenceOperation fieldRef &&
                        fieldRef.Instance is IInstanceReferenceOperation instanceRef &&
                        instanceRef.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance &&
                        context.ContainingMethodSymbol.MethodKind == MethodKind.Constructor)
                    {
                        PurityAnalysisEngine.LogDebug(" Assignment Target: Instance FieldReference within Constructor - Allowed (Target is Pure)");
                        targetPurityResult = PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }
                    // --- REVERTED SPECIAL CASE --- 
                    else // Strict check for all other non-constructor field mods
                    {
                        string fieldName = (targetOperation as IFieldReferenceOperation)?.Field?.Name ?? "Unknown Field";
                        string methodKind = context.ContainingMethodSymbol.MethodKind.ToString();
                        PurityAnalysisEngine.LogDebug($" Assignment Target: FieldReference '{fieldName}' outside Constructor - Impure"); // Simplified log
                        targetPurityResult = PurityAnalysisEngine.PurityAnalysisResult.Impure(targetOperation.Syntax);
                    }
                    // --- END REVERT --- 
                    break;

                case OperationKind.PropertyReference:
                    // Allow assigning to instance properties ONLY within a constructor.
                    if (targetOperation is IPropertyReferenceOperation propRef &&
                        propRef.Instance is IInstanceReferenceOperation instanceRef2 &&
                        instanceRef2.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance &&
                        context.ContainingMethodSymbol.MethodKind == MethodKind.Constructor)
                    {
                        PurityAnalysisEngine.LogDebug(" Assignment Target: Instance PropertyReference within Constructor - Allowed (Target is Pure)");
                        targetPurityResult = PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }
                    // RE-ADD: Allow assignments to 'this' properties within ANY method of a record struct
                    // This covers compiler-generated 'with' methods and user-defined methods.
                    else if (targetOperation is IPropertyReferenceOperation propRef2 &&
                             propRef2.Instance != null && // Ensure it's an instance property assignment (RELAXED CHECK)
                             context.ContainingMethodSymbol.ContainingType.IsRecord &&
                             context.ContainingMethodSymbol.ContainingType.IsValueType)
                    {
                        PurityAnalysisEngine.LogDebug(" Assignment Target: Instance PropertyReference within Record Struct Method - Allowed (Target is Pure)");
                        targetPurityResult = PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }
                    else
                    {
                        PurityAnalysisEngine.LogDebug(" Assignment Target: PropertyReference (Non-Constructor / Non-RecordStruct Method) - Impure");
                        // Report on the specific property reference
                        targetPurityResult = PurityAnalysisEngine.PurityAnalysisResult.Impure(targetOperation.Syntax);
                    }
                    break;

                case OperationKind.ArrayElementReference:
                    // Treat all array element assignments as impure for simplicity now.
                    // Distinguishing local vs non-local arrays is tricky and often requires flow analysis beyond this rule.
                    PurityAnalysisEngine.LogDebug(" Assignment Target: ArrayElementReference - Impure");
                    // Report on the array element access expression
                    targetPurityResult = PurityAnalysisEngine.PurityAnalysisResult.Impure(targetOperation.Syntax);
                    break;
                /* // Previous more complex logic:
                if (targetOperation is IArrayElementReferenceOperation arrayElementRef &&
                    arrayElementRef.ArrayReference is ILocalReferenceOperation)
                {
                    PurityAnalysisEngine.LogDebug(" Assignment Target: ArrayElementReference (Local Array) - Assuming Pure");
                    targetPurityResult = PurityAnalysisEngine.PurityAnalysisResult.Pure;
                }
                else
                {
                    PurityAnalysisEngine.LogDebug(" Assignment Target: ArrayElementReference (Non-Local Array) - Impure");
                    targetPurityResult = PurityAnalysisEngine.PurityAnalysisResult.Impure(syntaxNode);
                }
                break;
                */

                default:
                    PurityAnalysisEngine.LogDebug($" Assignment Target: Unhandled Kind {targetOperation.Kind} - Assuming Impure");
                    // Report on the unknown target kind's syntax
                    targetPurityResult = PurityAnalysisEngine.PurityAnalysisResult.Impure(targetOperation.Syntax);
                    break;
            }

            // If the target itself is impure, we can return immediately.
            if (!targetPurityResult.IsPure)
            {
                // If the target is impure because it's an invalid modification target (ref/out/in/readonly ref param, static field, etc.)
                // report the diagnostic on the entire assignment/increment/decrement operation's syntax.
                PurityAnalysisEngine.LogDebug($"AssignmentPurityRule: Target check failed (Kind: {targetOperation.Kind}, RefKind: {(targetOperation as IParameterReferenceOperation)?.Parameter.RefKind}). Reporting impurity on the whole operation: {operation.Syntax}");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
            }

            // --- Step 2: Check Purity of the Assigned Value ---
            if (valueOperation != null)
            {
                PurityAnalysisEngine.LogDebug($"AssignmentPurityRule: Target was pure. Analyzing Value operation {valueOperation.Kind}");

                // Recursively check the purity of the assigned value using CheckSingleOperation
                var valueResult = PurityAnalysisEngine.CheckSingleOperation(valueOperation, context);
                if (!valueResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"AssignmentPurityRule: Value operation {valueOperation.Kind} is impure.");
                    // Report impure, pointing the diagnostic at the value's syntax if possible
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(valueResult.ImpureSyntaxNode ?? valueOperation.Syntax);
                }
                else
                {
                    PurityAnalysisEngine.LogDebug($"AssignmentPurityRule: Value operation {valueOperation.Kind} is pure.");
                }
            }
            else
            {
                // This might happen for simple increment/decrement (e.g., i++).
                // Target was already checked and found pure in Step 1.
                PurityAnalysisEngine.LogDebug("AssignmentPurityRule: Target was pure and Value operation is null/implicit (e.g., simple increment/decrement).");
            }

            // If both target and value (if applicable) are pure, the assignment is pure.
            PurityAnalysisEngine.LogDebug("AssignmentPurityRule: Both target and value (if applicable) are pure. Result: Pure");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }

        // Removed GetTargetSymbol helper as it's not used in the new logic
    }
}