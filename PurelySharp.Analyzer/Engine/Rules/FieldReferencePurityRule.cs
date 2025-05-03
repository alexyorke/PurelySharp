using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes field reference operations for purity.
    /// </summary>
    internal class FieldReferencePurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.FieldReference);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (!(operation is IFieldReferenceOperation fieldReference))
            {
                PurityAnalysisEngine.LogDebug($"WARNING: FieldReferencePurityRule called with unexpected operation type: {operation.Kind}. Assuming Impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
            }

            IFieldSymbol fieldSymbol = fieldReference.Field;
            if (fieldSymbol == null)
            {
                PurityAnalysisEngine.LogDebug($"    [FieldRefRule] Impure due to null fieldSymbol for {fieldReference.Syntax.ToString()}");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(fieldReference.Syntax); // Cannot determine purity
            }

            // If this field read is the target of an assignment, let AssignmentPurityRule handle it.
            if (IsPartOfAssignmentTarget(fieldReference))
            {
                PurityAnalysisEngine.LogDebug($"    [FieldRefRule] Skipping field read {fieldSymbol.Name} as it's an assignment target.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            // Check for const fields (always pure)
            if (fieldSymbol.IsConst)
            {
                PurityAnalysisEngine.LogDebug($"    [FieldRefRule] Field '{fieldSymbol.Name}' is const - Pure");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            // Check for static fields
            if (fieldSymbol.IsStatic)
            {
                // *** ADDED: Check static constructor purity ***
                var cctorResult = CheckStaticConstructorPurity(fieldSymbol.ContainingType, context);
                if (!cctorResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [FieldRefRule] Static field '{fieldSymbol.Name}' access IMPURE due to impure static constructor in {fieldSymbol.ContainingType.Name}.");
                    // Report impurity at the field access site
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(fieldReference.Syntax);
                }
                // *** END ADDED ***

                if (fieldSymbol.IsReadOnly)
                {
                    PurityAnalysisEngine.LogDebug($"    [FieldRefRule] Static readonly field '{fieldSymbol.Name}' - Pure");
                    return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                }
                else
                {
                    PurityAnalysisEngine.LogDebug($"    [FieldRefRule] Static non-readonly field '{fieldSymbol.Name}' - Impure");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(fieldReference.Syntax);
                }
            }

            // Check instance fields
            if (fieldReference.Instance != null)
            {
                PurityAnalysisEngine.LogDebug($"    [FieldRefRule] Instance field '{fieldSymbol.Name}'. Checking instance...");
                // Check the instance through which the field is accessed
                IOperation instanceOperation = fieldReference.Instance;

                if (instanceOperation is IParameterReferenceOperation paramRef)
                {
                    // Allow reading instance fields via 'in' or 'ref readonly' parameters
                    // Also allow if it's a struct passed by value (effectively readonly copy)
                    bool isReadOnlyRef = paramRef.Parameter.RefKind == RefKind.In ||
                                         paramRef.Parameter.RefKind == (RefKind)4; // RefReadOnlyParameter
                    bool isValueStruct = paramRef.Parameter.RefKind == RefKind.None && paramRef.Parameter.Type.IsValueType;

                    if (isReadOnlyRef || isValueStruct)
                    {
                        PurityAnalysisEngine.LogDebug($"    [FieldRefRule] Instance '{paramRef.Parameter.Name}' is {(isValueStruct ? "value struct" : "readonly ref")}. Read is Pure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }
                    else
                    {
                        PurityAnalysisEngine.LogDebug($"    [FieldRefRule] Instance '{paramRef.Parameter.Name}' is mutable ref/out. Read is Impure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(fieldReference.Syntax);
                    }
                }
                else if (instanceOperation is IInstanceReferenceOperation instanceRef && instanceRef.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance)
                {
                    // Reading instance field via 'this'
                    bool isReadonlyStruct = context.ContainingMethodSymbol.ContainingType.IsReadOnly &&
                                            context.ContainingMethodSymbol.ContainingType.IsValueType;
                    PurityAnalysisEngine.LogDebug($"    [FieldRefRule] Checking 'this' instance. isReadonlyStruct={isReadonlyStruct}, fieldSymbol.IsReadOnly={fieldSymbol.IsReadOnly}");

                    if (isReadonlyStruct)
                    {
                        PurityAnalysisEngine.LogDebug($"    [FieldRefRule] Instance is 'this' within a readonly struct. Read is Pure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }
                    else if (fieldSymbol.IsReadOnly)
                    {
                        PurityAnalysisEngine.LogDebug($"    [FieldRefRule] Instance is 'this', field '{fieldSymbol.Name}' is readonly. Read is Pure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }
                    else
                    {
                        // Reading a non-readonly field via 'this' is still considered pure.
                        PurityAnalysisEngine.LogDebug($"    [FieldRefRule] Instance is 'this' within a non-readonly type and field '{fieldSymbol.Name}' is not readonly. Read is Pure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }
                }
                else
                {
                    // Instance is something else (local variable, method call result, etc.)

                    // Heuristic: Allow reading fields/props via a local variable IF the local's type is a readonly struct.
                    if (instanceOperation is ILocalReferenceOperation localRef && localRef.Local?.Type != null &&
                        localRef.Local.Type.IsValueType && localRef.Local.Type.IsReadOnly)
                    {
                        PurityAnalysisEngine.LogDebug($"    [FieldRefRule] Instance is local var '{localRef.Local.Name}' of readonly struct type. Read is Pure (heuristic).");
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }

                    // If the instance isn't special (this, readonly param, readonly local) AND the field isn't known pure BCL,
                    // then accessing it is impure.
                    // Check if the FIELD itself is known pure BCL (e.g., string.Length)
                    string fieldPureSig = fieldSymbol.OriginalDefinition.ToDisplayString();
                    bool fieldKnownPure = PurityAnalysisEngine.IsKnownPureBCLMember(fieldSymbol);
                    PurityAnalysisEngine.LogDebug($"      [FieldRefRule] Checking IsKnownPureBCLMember for instance field accessed via {instanceOperation.Kind}: '{fieldPureSig}' -> {fieldKnownPure}");

                    if (fieldKnownPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [FieldRefRule] Instance field '{fieldSymbol.Name}' is known pure BCL. Read is Pure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }
                    else
                    {
                        // Default: Instance is complex/non-readonly-local and field not known pure -> Impure
                        PurityAnalysisEngine.LogDebug($"    [FieldRefRule] Instance is complex ({instanceOperation.Kind})/non-readonly-local and field '{fieldSymbol.Name}' not known pure BCL. Assuming read is Impure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(fieldReference.Syntax);
                    }
                }
            }

            // Default case (shouldn't be reached ideally, but safety net)
            PurityAnalysisEngine.LogDebug($"    [FieldRefRule] Unhandled case for field '{fieldSymbol.Name}'. Assuming Impure.");
            return PurityAnalysisEngine.PurityAnalysisResult.Impure(fieldReference.Syntax);
        }

        /// <summary>
        /// Helper to check if an operation is the direct target of an assignment operation higher up the tree.
        /// </summary>
        private bool IsPartOfAssignmentTarget(IOperation operation)
        {
            IOperation? parent = operation.Parent;
            while (parent != null)
            {
                if (parent is IAssignmentOperation assignmentOperation && assignmentOperation.Target == operation)
                {
                    return true;
                }
                // Stop if we hit a statement level or block level, assignment target is usually direct child
                if (parent is IExpressionStatementOperation || parent is IBlockOperation)
                {
                    return false;
                }
                // Check compound assignments and increments/decrements too
                if (parent is ICompoundAssignmentOperation compoundAssignment && compoundAssignment.Target == operation)
                {
                    return true;
                }
                if (parent is IIncrementOrDecrementOperation incrementOrDecrement && incrementOrDecrement.Target == operation)
                {
                    return true;
                }

                operation = parent;
                parent = parent.Parent;
            }
            return false;
        }

        // *** ADDED HELPER ***
        private static PurityAnalysisEngine.PurityAnalysisResult CheckStaticConstructorPurity(INamedTypeSymbol typeSymbol, PurityAnalysisContext context)
        {
            if (typeSymbol == null) return PurityAnalysisEngine.PurityAnalysisResult.Pure;

            var staticConstructor = typeSymbol.StaticConstructors.FirstOrDefault();
            if (staticConstructor == null)
            {
                // No static constructor, trivially pure initialization
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"        [CheckStaticCtor] Found static constructor for {typeSymbol.Name}. Checking recursively...");
            // Use OriginalDefinition to avoid issues with constructed generics
            return PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                staticConstructor.OriginalDefinition,
                context.SemanticModel, // Use the context's model
                context.EnforcePureAttributeSymbol,
                context.AllowSynchronizationAttributeSymbol,
                context.VisitedMethods, // Critical for cycle detection
                context.PurityCache // Share cache
            );
        }
        // *** END ADDED HELPER ***
    }
}