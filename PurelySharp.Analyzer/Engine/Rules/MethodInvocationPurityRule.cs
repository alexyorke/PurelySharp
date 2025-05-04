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

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IInvocationOperation invocationOperation))
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

            // *** Check for Delegate Invocation (target is Delegate.Invoke) ***
            if (invokedMethodSymbol.Name == "Invoke" && invokedMethodSymbol.ContainingType?.TypeKind == TypeKind.Delegate)
            {
                // --- Original Delegate Logic (UNCOMMENTED) ---
                PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] === Simplified Delegate Invocation Check Start ==="); // Revert Log Prefix
                PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Invoked Symbol: {invokedMethodSymbol.ContainingType.Name}.Invoke()"); // Revert Log Prefix

                if (invocationOperation.Instance == null)
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Instance is NULL (static delegate?). Assuming impure."); // Revert Log Prefix
                    return PurityAnalysisEngine.ImpureResult(invocationOperation.Syntax);
                }

                PurityAnalysisEngine.PurityAnalysisResult result = PurityAnalysisEngine.PurityAnalysisResult.Pure; // Assume pure initially
                IOperation delegateInstanceOp = invocationOperation.Instance;
                PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Analyzing Delegate Instance Op: {delegateInstanceOp.Kind} | Syntax: {delegateInstanceOp.Syntax}"); // Revert Log Prefix

                // --- Revert to always using DFA state map --- 
                ISymbol? delegateInstanceSymbol = TryResolveSymbol(delegateInstanceOp);
                if (delegateInstanceSymbol != null)
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Resolved Instance Symbol: {delegateInstanceSymbol.ToDisplayString()} ({delegateInstanceSymbol.Kind})"); // Revert Log Prefix
                    if (currentState.DelegateTargetMap.TryGetValue(delegateInstanceSymbol, out var potentialTargets))
                    {
                        PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S-MAP] Found entry for {delegateInstanceSymbol.Name} in map."); // *** ADDED LOG ***
                        PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S-MAP]   Targets: [{string.Join(", ", potentialTargets.MethodSymbols.Select(m => m.Name))}]"); // *** ADDED LOG ***
                        PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Found {potentialTargets.MethodSymbols.Count} potential target(s) in DFA map for {delegateInstanceSymbol.Name}."); // Revert Log Prefix
                        if (potentialTargets.MethodSymbols.IsEmpty)
                        {
                            PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] --> Map entry is empty. Assuming PURE."); // Revert Log Prefix
                            result = PurityAnalysisEngine.PurityAnalysisResult.Pure;
                        }
                        else
                        {
                            result = PurityAnalysisEngine.PurityAnalysisResult.Pure; // Assume pure until proven otherwise
                            foreach (var targetMethod in potentialTargets.MethodSymbols)
                            {
                                PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Checking Potential Target from Map: {targetMethod.ToDisplayString()}"); // Revert Log Prefix
                                var targetPurity = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                                    targetMethod.OriginalDefinition, context.SemanticModel, context.EnforcePureAttributeSymbol, context.AllowSynchronizationAttributeSymbol,
                                    context.VisitedMethods, context.PurityCache);
                                PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Potential Target Purity Result: IsPure={targetPurity.IsPure}"); // Revert Log Prefix
                                if (!targetPurity.IsPure)
                                {
                                    PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] --> IMPURE target found in map. Invocation is impure."); // Revert Log Prefix
                                    result = targetPurity;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S-MAP] *** NO entry found for {delegateInstanceSymbol.Name} in map. Assuming impure. ***"); // *** ADDED LOG ***
                        PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] --> IMPURE (Symbol {delegateInstanceSymbol.Name} NOT FOUND in DFA state map - untracked source). Fallback to PS0002 at invocation."); // Revert Log Prefix
                        result = PurityAnalysisEngine.ImpureResult(invocationOperation.Syntax); // Fallback impurity
                    }
                }
                else
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] --> IMPURE (Could not resolve instance {delegateInstanceOp.Kind} to a trackable symbol). Fallback to PS0002 at instance op."); // Revert Log Prefix
                    result = PurityAnalysisEngine.ImpureResult(delegateInstanceOp.Syntax); // Fallback impurity
                }

                PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Final Result for Delegate Invocation: IsPure={result.IsPure}"); // Revert Log Prefix
                PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] === Simplified Delegate Invocation Check End ==="); // Revert Log Prefix
                return result;
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
                    var sourceResult = PurityAnalysisEngine.CheckSingleOperation(sourceArgument.Value, context, currentState);
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
                            var delegateArgResult = PurityAnalysisEngine.CheckSingleOperation(argument.Value, context, currentState);
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
                var cctorResult = PurityAnalysisEngine.CheckStaticConstructorPurity(invokedMethodSymbol.ContainingType, context, currentState);
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
                var instanceResult = PurityAnalysisEngine.CheckSingleOperation(invocationOperation.Instance, context, currentState);
                PurityAnalysisEngine.LogDebug($"  [MIR] Instance check result: IsPure={instanceResult.IsPure}, Node Type={instanceResult.ImpureSyntaxNode?.GetType().Name ?? "NULL"}");
                if (!instanceResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR] --> IMPURE (Instance is impure)");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(instanceResult.ImpureSyntaxNode ?? invocationOperation.Instance.Syntax);
                }
            }

            // *** ADDED: Check Argument Purity ***
            PurityAnalysisEngine.LogDebug($"  [MIR] Checking purity of {invocationOperation.Arguments.Length} arguments for {invokedMethodSymbol.OriginalDefinition.Name}.");
            foreach (var argument in invocationOperation.Arguments)
            {
                PurityAnalysisEngine.LogDebug($"  [MIR]   Checking argument: {argument.Value.Kind} | Syntax: {argument.Value.Syntax.ToString().Trim()}");
                var argumentResult = PurityAnalysisEngine.CheckSingleOperation(argument.Value, context, currentState);
                PurityAnalysisEngine.LogDebug($"  [MIR]   Argument check result: IsPure={argumentResult.IsPure}");
                if (!argumentResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR] --> IMPURE (Argument is impure)");
                    // Return the result from the impure argument analysis.
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(argumentResult.ImpureSyntaxNode ?? argument.Value.Syntax);
                }
            }
            // *** END Argument Check ***

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

        // Helper to resolve the ISymbol from an IOperation representing a variable, parameter, field, etc.
        private static ISymbol? TryResolveSymbol(IOperation? operation)
        {
            if (operation == null) return null;
            switch (operation.Kind)
            {
                case OperationKind.LocalReference:
                    return ((ILocalReferenceOperation)operation).Local;
                case OperationKind.ParameterReference:
                    return ((IParameterReferenceOperation)operation).Parameter;
                case OperationKind.FieldReference:
                    return ((IFieldReferenceOperation)operation).Field;
                // Add other kinds as needed (e.g., PropertyReference?)
                default:
                    // Log or handle cases where the symbol cannot be directly resolved from the operation
                    PurityAnalysisEngine.LogDebug($"  [TryResolveSymbol] Could not resolve symbol for operation kind: {operation.Kind}");
                    return null;
            }
        }

        // *** REMOVED CheckStaticConstructorPurity HELPER (Moved to PurityAnalysisEngine) *** 

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