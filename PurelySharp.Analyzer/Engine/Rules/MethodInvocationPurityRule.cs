using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using PurelySharp.Analyzer.Engine; // Namespace for PurityAnalysisEngine
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax; // For SyntaxNode

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes method invocations for potential side effects.
    /// </summary>
    internal class MethodInvocationPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Invocation);

        // Pre-compile a list of known collection mutating methods that are OK on local instances
        // Format: TypeName.MethodName (without generics or parameters for simple check)
        private static readonly ImmutableHashSet<string> _safeLocalMutationMethods = ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "System.Collections.Generic.List.Add",
            "System.Collections.Generic.List.Insert",
            "System.Collections.Generic.List.AddRange",
            "System.Collections.Generic.Dictionary.Add",
            "System.Collections.Generic.HashSet.Add"
        // Add others? Clear? Remove? Indexer Set? (Indexer set handled by AssignmentRule?)
        // StringBuilder.Append? - No, StringBuilder is often used for return values.
        );

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (operation is not IInvocationOperation invocationOperation)
            {
                PurityAnalysisEngine.LogDebug("  [MIR] WARNING: Called with non-invocation.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure; // Should not happen
            }

            var invokedMethodSymbol = invocationOperation.TargetMethod;
            if (invokedMethodSymbol == null)
            {
                PurityAnalysisEngine.LogDebug("  [MIR] Cannot resolve target method. Assuming impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax); // Cannot resolve method
            }

            // *** NEW: Check for Delegate Invocation (target is Delegate.Invoke) ***
            if (invokedMethodSymbol.Name == "Invoke" && invokedMethodSymbol.ContainingType?.TypeKind == TypeKind.Delegate)
            {
                PurityAnalysisEngine.LogDebug($"  [MIR] Detected delegate invocation via {invokedMethodSymbol.ContainingType.Name}.Invoke(). Analyzing instance.");
                // The 'Instance' of the InvocationOperation is the delegate being invoked.
                if (invocationOperation.Instance != null)
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR] Checking delegate instance purity for {invocationOperation.Instance.Kind}: {invocationOperation.Instance.Syntax.ToString().Trim()}");
                    var delegateInstanceResult = PurityAnalysisEngine.CheckSingleOperation(invocationOperation.Instance, context);
                    PurityAnalysisEngine.LogDebug($"  [MIR] Delegate instance check result: IsPure={delegateInstanceResult.IsPure}, Node Type={delegateInstanceResult.ImpureSyntaxNode?.GetType().Name ?? "NULL"}");

                    // If the delegate instance itself (e.g., its creation) was impure, the invocation is impure.
                    // Report the impurity at the invocation site, but potentially point to the delegate creation node.
                    if (!delegateInstanceResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"  [MIR] --> IMPURE (Delegate instance expression was impure)");
                        // If the creation was impure, report it, potentially pointing to the creation syntax.
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(delegateInstanceResult.ImpureSyntaxNode ?? invocationOperation.Syntax);
                    }
                    else
                    {
                        // Try to resolve the actual target method from the delegate instance
                        IMethodSymbol? actualTarget = null;
                        if (invocationOperation.Instance is IDelegateCreationOperation creationOp)
                        {
                            if (creationOp.Target is IMethodReferenceOperation methodRef)
                            {
                                actualTarget = methodRef.Method;
                                PurityAnalysisEngine.LogDebug($"  [MIR] Resolved delegate target from DelegateCreation/MethodReference: {actualTarget?.ToDisplayString() ?? "NULL"}");
                            }
                            else if (creationOp.Target is IAnonymousFunctionOperation lambdaOp)
                            {
                                actualTarget = lambdaOp.Symbol;
                                PurityAnalysisEngine.LogDebug($"  [MIR] Resolved delegate target from DelegateCreation/AnonymousFunction: {actualTarget?.ToDisplayString() ?? "NULL"}");
                            }
                            else
                            {
                                PurityAnalysisEngine.LogDebug($"  [MIR] DelegateCreation target is neither MethodReference nor AnonymousFunction: {creationOp.Target?.Kind}");
                            }
                        }
                        else if (invocationOperation.Instance is IFieldReferenceOperation fieldRefOp)
                        {
                            // Try to find the initializer for the field
                            var fieldSymbol = fieldRefOp.Field;
                            if (fieldSymbol?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken) is VariableDeclaratorSyntax declarator &&
                                declarator.Initializer?.Value is IOperation fieldInitializerOp)
                            {
                                PurityAnalysisEngine.LogDebug($"  [MIR] Delegate instance is FieldReference '{fieldSymbol.Name}'. Analyzing field initializer...");
                                if (fieldInitializerOp is IDelegateCreationOperation fieldCreationOp)
                                {
                                    actualTarget = ResolveDelegateCreationTarget(fieldCreationOp);
                                    PurityAnalysisEngine.LogDebug($"  [MIR] Resolved delegate target from FieldReference/Initializer/DelegateCreation: {actualTarget?.ToDisplayString() ?? "NULL"}");
                                }
                                else
                                {
                                    PurityAnalysisEngine.LogDebug($"  [MIR] Field initializer is not DelegateCreation: {fieldInitializerOp.Kind}");
                                }
                            }
                            else
                            {
                                PurityAnalysisEngine.LogDebug($"  [MIR] Could not find initializer for field: {fieldSymbol?.Name ?? "Unknown"}");
                            }
                        }
                        else
                        {
                            // TODO: Handle other delegate instance kinds (local refs, params, field refs, method returns)
                            PurityAnalysisEngine.LogDebug($"  [MIR] Could not resolve delegate target directly from instance kind: {invocationOperation.Instance?.Kind}. Assuming PURE (Simplification).");
                        }

                        if (actualTarget != null)
                        {
                            PurityAnalysisEngine.LogDebug($"  [MIR] Performing recursive check on resolved delegate target: {actualTarget.ToDisplayString()}");
                            // Analyze the actual target method
                            var targetPurity = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                                actualTarget.OriginalDefinition,
                                context.SemanticModel,
                                context.EnforcePureAttributeSymbol,
                                context.AllowSynchronizationAttributeSymbol,
                                context.VisitedMethods,
                                context.PurityCache);

                            PurityAnalysisEngine.LogDebug($"  [MIR] Recursive check result for delegate target {actualTarget.ToDisplayString()}: IsPure={targetPurity.IsPure}");
                            return targetPurity.IsPure
                                ? PurityAnalysisEngine.PurityAnalysisResult.Pure
                                : PurityAnalysisEngine.PurityAnalysisResult.Impure(targetPurity.ImpureSyntaxNode ?? invocationOperation.Syntax); // Report impurity at invocation site
                        }
                        else
                        {
                            // If target couldn't be resolved, fallback to assuming pure (current simplification)
                            PurityAnalysisEngine.LogDebug($"  [MIR] --> PURE (Delegate instance expression pure, target not resolved, assuming pure - simplification)");
                            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                        }
                    }
                }
                else
                {
                    // Invoking a null instance or static delegate? This case might need refinement.
                    PurityAnalysisEngine.LogDebug($"  [MIR] Delegate invocation has null instance. Assuming pure for now.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                }
            }
            // *** END Delegate Invocation Check ***

            // *** NEW: Check for LINQ Extension Methods ***
            if (invokedMethodSymbol.IsExtensionMethod &&
                SymbolEqualityComparer.Default.Equals(invokedMethodSymbol.ContainingType?.OriginalDefinition, context.SemanticModel.Compilation.GetTypeByMetadataName("System.Linq.Enumerable")))
            {
                PurityAnalysisEngine.LogDebug($"  [MIR] Detected LINQ Enumerable extension method: {invokedMethodSymbol.Name}. Checking source and delegate arguments.");

                // *** ADDED: First, check the purity of the source collection being operated on ***
                // LINQ extension methods are called like: source.Method(args...)
                // The source is the first argument to the underlying static method.
                if (invocationOperation.Arguments.Length > 0)
                {
                    var sourceArgument = invocationOperation.Arguments[0]; // The instance/source is the first argument
                    PurityAnalysisEngine.LogDebug($"  [MIR]   Checking LINQ source argument purity: {sourceArgument.Value.Kind}");
                    var sourceResult = PurityAnalysisEngine.CheckSingleOperation(sourceArgument.Value, context);
                    // PurityAnalysisEngine.LogDebug($"  [MIR]   LINQ source argument result: IsPure={sourceResult.IsPure}, Node={sourceResult.ImpureSyntaxNode?.Kind()}"); // Use Kind() extension method
                    if (!sourceResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"  [MIR] --> IMPURE (LINQ source argument was impure)");
                        // *** RETURN THE SOURCE RESULT DIRECTLY ***
                        return sourceResult;
                    }
                }
                else
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR]   WARNING: LINQ method {invokedMethodSymbol.Name} called with no arguments? Assuming impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax); // Should not happen
                }

                // If source is pure, check delegate arguments
                PurityAnalysisEngine.LogDebug($"  [MIR]   LINQ source was pure. Checking delegate arguments...");
                bool allDelegatesPure = true;
                PurityAnalysisEngine.PurityAnalysisResult firstImpureDelegateResult = PurityAnalysisEngine.PurityAnalysisResult.Pure;

                for (int i = 0; i < invokedMethodSymbol.Parameters.Length; i++)
                {
                    IParameterSymbol parameter = invokedMethodSymbol.Parameters[i];
                    if (parameter.Type?.TypeKind == TypeKind.Delegate)
                    {
                        int argumentIndex = -1;
                        for (int argIdx = 0; argIdx < invocationOperation.Arguments.Length; ++argIdx)
                        {
                            if (SymbolEqualityComparer.Default.Equals(invocationOperation.Arguments[argIdx].Parameter, parameter))
                            {
                                argumentIndex = argIdx;
                                break;
                            }
                        }

                        if (argumentIndex != -1)
                        {
                            IArgumentOperation argument = invocationOperation.Arguments[argumentIndex];
                            PurityAnalysisEngine.LogDebug($"  [MIR]   Checking LINQ delegate argument '{parameter.Name}' (Param Index {i}, Arg Index {argumentIndex}) for operation: {argument.Value.Kind}");
                            var delegateArgResult = PurityAnalysisEngine.CheckSingleOperation(argument.Value, context);
                            PurityAnalysisEngine.LogDebug($"  [MIR]   Delegate argument '{parameter.Name}' result: IsPure={delegateArgResult.IsPure}");
                            if (!delegateArgResult.IsPure)
                            {
                                allDelegatesPure = false;
                                firstImpureDelegateResult = delegateArgResult; // Store the first impure result
                                break; // Stop checking arguments if one is impure
                            }
                        }
                        else
                        {
                            PurityAnalysisEngine.LogDebug($"  [MIR]   WARNING: Could not find argument corresponding to LINQ delegate parameter {parameter.Name}. Assuming impure.");
                            allDelegatesPure = false;
                            firstImpureDelegateResult = PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax); // Report at invocation site
                            break;
                        }
                    }
                }

                if (allDelegatesPure)
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR] LINQ method source and all relevant delegate arguments determined to be pure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                }
                else
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR] --> IMPURE (LINQ method, impure delegate argument detected)");
                    // Return the result from the impure delegate, pointing to the delegate's node if possible.
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(firstImpureDelegateResult.ImpureSyntaxNode ?? invocationOperation.Syntax);
                }
            }
            // *** END LINQ Check ***

            // *** REGULAR METHOD INVOCATION ANALYSIS ***

            // *** Check Static Constructor Purity First (for static calls) ***
            if (invokedMethodSymbol.IsStatic && invokedMethodSymbol.ContainingType != null)
            {
                var cctorResult = CheckStaticConstructorPurity(invokedMethodSymbol.ContainingType, context);
                if (!cctorResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR] Static method call '{invokedMethodSymbol.Name}' IMPURE due to impure static constructor in {invokedMethodSymbol.ContainingType.Name}.");
                    // Report impurity at the invocation site
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax);
                }
            }

            // *** Check Instance Purity First (for regular method calls) ***
            if (invocationOperation.Instance != null)
            {
                PurityAnalysisEngine.LogDebug($"  [MIR] Checking instance purity for {invocationOperation.Instance.Kind}: {invocationOperation.Instance.Syntax.ToString().Trim()}"); // Log trimmed syntax
                var instanceResult = PurityAnalysisEngine.CheckSingleOperation(invocationOperation.Instance, context);
                PurityAnalysisEngine.LogDebug($"  [MIR] Instance check result: IsPure={instanceResult.IsPure}, Node Type={instanceResult.ImpureSyntaxNode?.GetType().Name ?? "NULL"}");
                if (!instanceResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR] --> IMPURE (Instance is impure)");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(instanceResult.ImpureSyntaxNode ?? invocationOperation.Instance.Syntax);
                }
            }

            // Use OriginalDefinition for checks
            var originalDefinitionSymbol = invokedMethodSymbol.OriginalDefinition;

            // ADDED: Explicit check for JsonSerializer.Deserialize
            string containingTypeName = originalDefinitionSymbol.ContainingType?.ToDisplayString();
            string methodName = originalDefinitionSymbol.Name;
            if (methodName == "Deserialize" && containingTypeName == "System.Text.Json.JsonSerializer")
            {
                PurityAnalysisEngine.LogDebug($"  [MIR] --> IMPURE (Explicit check: JsonSerializer.Deserialize)");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax);
            }
            // END ADDED Check

            string methodDisplayString = originalDefinitionSymbol.ToDisplayString(); // For logging
            PurityAnalysisEngine.LogDebug($"  [MIR] Analyzing regular call to: {methodDisplayString} | Syntax: {invocationOperation.Syntax}");

            // --- SIMPLIFIED CHECKS --- 

            // 1. Check known impure methods
            string signatureForKnownImpure = originalDefinitionSymbol.ToDisplayString();
            PurityAnalysisEngine.LogDebug($"  [MIR] Checking IsKnownImpure with signature: '{signatureForKnownImpure}'");
            if (PurityAnalysisEngine.IsKnownImpure(originalDefinitionSymbol))
            {
                PurityAnalysisEngine.LogDebug($"  [MIR] --> IMPURE (Known Impure)");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax);
            }

            // 2. Check known pure BCL methods
            string signatureForKnownPure = originalDefinitionSymbol.ToDisplayString();
            PurityAnalysisEngine.LogDebug($"  [MIR] Checking IsKnownPureBCLMember with signature: '{signatureForKnownPure}'");
            if (PurityAnalysisEngine.IsKnownPureBCLMember(originalDefinitionSymbol))
            {
                PurityAnalysisEngine.LogDebug($"  [MIR] --> PURE (Known Pure BCL)");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            // 3. Check if method is in known impure namespace/type (unless explicitly pure)
            bool isExplicitlyPure = PurityAnalysisEngine.IsPureEnforced(invokedMethodSymbol, context.EnforcePureAttributeSymbol);
            if (PurityAnalysisEngine.IsInImpureNamespaceOrType(originalDefinitionSymbol) && !isExplicitlyPure)
            {
                PurityAnalysisEngine.LogDebug($"  [MIR] --> IMPURE (In Impure NS/Type and not explicitly Pure)");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax);
            }

            // 4. Perform recursive check on the target method
            PurityAnalysisEngine.LogDebug($"  [MIR] Performing recursive check for: {methodDisplayString}");
            var recursiveResult = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                originalDefinitionSymbol, // Use original definition
                context.SemanticModel,
                context.EnforcePureAttributeSymbol,
                context.AllowSynchronizationAttributeSymbol,
                context.VisitedMethods,
                context.PurityCache);

            PurityAnalysisEngine.LogDebug($"  [MIR] Recursive check result for {methodDisplayString}: IsPure={recursiveResult.IsPure}");
            // Return the result from the recursive check
            // If impure, try to use the node from the deeper analysis, otherwise use the invocation node.
            return recursiveResult.IsPure
                ? PurityAnalysisEngine.PurityAnalysisResult.Pure
                : PurityAnalysisEngine.PurityAnalysisResult.Impure(recursiveResult.ImpureSyntaxNode ?? invocationOperation.Syntax);
        }

        // Helper to resolve target from DelegateCreationOperation
        private static IMethodSymbol? ResolveDelegateCreationTarget(IDelegateCreationOperation creationOp)
        {
            if (creationOp.Target is IMethodReferenceOperation methodRef)
            {
                return methodRef.Method;
            }
            else if (creationOp.Target is IAnonymousFunctionOperation lambdaOp)
            {
                return lambdaOp.Symbol;
            }
            return null;
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

        // *** REMOVED HELPER METHODS for delegate resolution *** 
        /*
                // Helper to resolve the target method symbol from various delegate instance representations.
                private static IMethodSymbol? ResolveDelegateInstanceTarget(IOperation delegateInstance, PurityAnalysisContext context)
                {
                    // ... removed ...
                }

                // Helper to resolve target from DelegateCreationOperation
                private static IMethodSymbol? ResolveDelegateCreationTarget(IDelegateCreationOperation creationOp)
                {
                     // ... removed ...
               }

                // Placeholder helper to find assignment (requires flow analysis)
                private static IOperation? FindAssignmentToLocal(ILocalReferenceOperation localRef, PurityAnalysisContext context)
                {
                    // ... removed ...
                }

                // Placeholder helper to find returned delegate target (requires analysis of source method)
                private static IMethodSymbol? FindReturnedDelegateTarget(IMethodSymbol sourceMethod, PurityAnalysisContext context)
                {
                    // ... removed ...
                }
        */
    }
}