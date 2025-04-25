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
            SyntaxNode syntaxNode = operation.Syntax;

            if (operation is IAssignmentOperation assignmentOperation)
            {
                targetOperation = assignmentOperation.Target;
                valueOperation = assignmentOperation.Value; // Get the value operation ONLY for assignment
                syntaxNode = assignmentOperation.Target.Syntax; // Point diagnostic at the target
            }
            else if (operation is IIncrementOrDecrementOperation incrementDecrementOperation)
            {
                targetOperation = incrementDecrementOperation.Target;
                // DO NOT access .Value here, it doesn't exist.
                // For simple inc/dec (i++), the target check is sufficient.
                // Compound assignments are usually ICompoundAssignmentOperation, handled separately.
                syntaxNode = incrementDecrementOperation.Target.Syntax; // Point diagnostic at the target
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
                    if (targetOperation is IParameterReferenceOperation paramRef && (paramRef.Parameter.RefKind == RefKind.Ref || paramRef.Parameter.RefKind == RefKind.Out))
                    {
                        PurityAnalysisEngine.LogDebug(" Assignment Target: ParameterReference (ref/out) - Impure");
                        targetPurityResult = PurityAnalysisEngine.PurityAnalysisResult.Impure(syntaxNode);
                    }
                    else
                    {
                        PurityAnalysisEngine.LogDebug(" Assignment Target: ParameterReference (value/in) - Pure");
                        targetPurityResult = PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }
                    break;

                case OperationKind.FieldReference:
                    // Allow assigning to instance fields within a constructor
                    if (targetOperation is IFieldReferenceOperation fieldRef &&
                        fieldRef.Instance is IInstanceReferenceOperation instanceRef &&
                        instanceRef.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance &&
                        context.ContainingMethodSymbol.MethodKind == MethodKind.Constructor)
                    {
                        PurityAnalysisEngine.LogDebug(" Assignment Target: Instance FieldReference within Constructor - Allowed (Target is Pure)");
                        targetPurityResult = PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }
                    else
                    {
                        string fieldName = (targetOperation as IFieldReferenceOperation)?.Field?.Name ?? "Unknown Field";
                        string methodKind = context.ContainingMethodSymbol.MethodKind.ToString();
                        PurityAnalysisEngine.LogDebug($" Assignment Target: FieldReference '{fieldName}' outside Constructor (MethodKind={methodKind}) - Impure");
                        targetPurityResult = PurityAnalysisEngine.PurityAnalysisResult.Impure(syntaxNode);
                    }
                    break;

                case OperationKind.PropertyReference:
                    // Allow assigning to instance properties within a constructor (assuming setter is simple).
                    // Note: Analyzing the setter's purity is complex and not done here.
                    // Use a different variable name (instanceRef2) to avoid scope collision.
                    if (targetOperation is IPropertyReferenceOperation propRef &&
                        propRef.Instance is IInstanceReferenceOperation instanceRef2 && // Renamed variable
                        instanceRef2.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance &&
                        context.ContainingMethodSymbol.MethodKind == MethodKind.Constructor)
                    {
                        PurityAnalysisEngine.LogDebug(" Assignment Target: Instance PropertyReference within Constructor - Allowed (Target is Pure)");
                        targetPurityResult = PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }
                    // Also allow assignments to 'this' properties within record structs (handling 'with' expressions)
                    else if (targetOperation is IPropertyReferenceOperation propRef2 && // Renamed variable
                             propRef2.Instance is IInstanceReferenceOperation instanceRef3 && // Renamed variable
                             instanceRef3.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance &&
                             context.ContainingMethodSymbol.ContainingType.IsRecord &&
                             context.ContainingMethodSymbol.ContainingType.IsValueType &&
                             // Refined check: Target only implicitly declared methods (like $Clone) or 'With...' methods
                             (context.ContainingMethodSymbol.IsImplicitlyDeclared || context.ContainingMethodSymbol.Name.StartsWith("With")))
                    {
                        PurityAnalysisEngine.LogDebug(" Assignment Target: Instance PropertyReference within Compiler-Generated Record Struct Method - Allowed (Target is Pure for 'with')");
                        targetPurityResult = PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }
                    else
                    {
                        PurityAnalysisEngine.LogDebug(" Assignment Target: PropertyReference (Non-Constructor, Non-RecordStruct 'with'-related, or Static) - Impure");
                        targetPurityResult = PurityAnalysisEngine.PurityAnalysisResult.Impure(syntaxNode);
                    }
                    break;

                case OperationKind.ArrayElementReference:
                    // Treat all array element assignments as impure for simplicity now.
                    // Distinguishing local vs non-local arrays is tricky and often requires flow analysis beyond this rule.
                    PurityAnalysisEngine.LogDebug(" Assignment Target: ArrayElementReference - Impure");
                    targetPurityResult = PurityAnalysisEngine.PurityAnalysisResult.Impure(syntaxNode);
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
                    targetPurityResult = PurityAnalysisEngine.PurityAnalysisResult.Impure(syntaxNode);
                    break;
            }

            // If the target itself is impure, we can return immediately.
            if (!targetPurityResult.IsPure)
            {
                return targetPurityResult;
            }

            // --- Step 2: Check Purity of the Assigned Value ---
            // If the target was pure, now check the value being assigned (if it exists).
            if (valueOperation != null)
            {
                PurityAnalysisEngine.LogDebug($"AssignmentPurityRule: Target was pure. Analyzing Value operation {valueOperation.Kind}");

                // --- FIX: Handle FlowCaptureReference explicitly --- 
                if (valueOperation.Kind == OperationKind.FlowCaptureReference)
                {
                    PurityAnalysisEngine.LogDebug($"AssignmentPurityRule: Value is FlowCaptureReference. Treating as Pure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                }
                // --- END FIX --- 

                // Recursively check the purity of the assigned value using CheckSingleOperation
                var valueResult = PurityAnalysisEngine.CheckSingleOperation(valueOperation, context);
                if (!valueResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"AssignmentPurityRule: Value operation {valueOperation.Kind} is impure.");
                    // Return impure, pointing the diagnostic at the value's syntax if possible
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(valueResult.ImpureSyntaxNode ?? valueOperation.Syntax);
                }
                else
                {
                    PurityAnalysisEngine.LogDebug($"AssignmentPurityRule: Value operation {valueOperation.Kind} is pure.");
                }
            }
            else
            {
                // This might happen for simple increment/decrement (e.g., i++) where Value is implicit/null.
                // Target was already checked and found pure.
                PurityAnalysisEngine.LogDebug("AssignmentPurityRule: Target was pure and Value operation is null/implicit (e.g., simple increment/decrement).");
            }

            // If both target and value (if applicable) are pure, the assignment is pure.
            PurityAnalysisEngine.LogDebug("AssignmentPurityRule: Both target and value (if applicable) are pure. Result: Pure");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }

        // Removed GetTargetSymbol helper as it's not used in the new logic
    }
}