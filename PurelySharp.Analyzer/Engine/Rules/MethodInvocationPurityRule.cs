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

            // Use OriginalDefinition for checks
            var originalDefinitionSymbol = invokedMethodSymbol.OriginalDefinition;
            string methodDisplayString = originalDefinitionSymbol.ToDisplayString(); // For logging
            PurityAnalysisEngine.LogDebug($"  [MIR] Analyzing call to: {methodDisplayString} | Syntax: {invocationOperation.Syntax}");

            // *** NEW: Handle Delegate Invocation ***
            if (invokedMethodSymbol.Name == "Invoke" && invocationOperation.Instance != null)
            {
                PurityAnalysisEngine.LogDebug($"  [MIR] Detected Delegate Invocation via .Invoke() on Instance: {invocationOperation.Instance.Syntax}");

                // Attempt to resolve the actual target method represented by the Instance
                var actualDelegateTarget = ResolveDelegateInstanceTarget(invocationOperation.Instance, context);

                if (actualDelegateTarget != null)
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR] Resolved Delegate Instance to actual target: {actualDelegateTarget.ToDisplayString()}. Recursively checking purity.");
                    // Check the *actual* target method
                    return PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                        actualDelegateTarget.OriginalDefinition,
                        context.SemanticModel,
                        context.EnforcePureAttributeSymbol,
                        context.AllowSynchronizationAttributeSymbol,
                        context.VisitedMethods,
                        context.PurityCache);
                }
                else
                {
                    // REVERTED CHANGE:
                    // If the helper returned null, it means it couldn't directly resolve the target
                    // (e.g., local variable, parameter). Assume impure for safety, as we cannot verify
                    // the source delegate's purity within this rule.
                    PurityAnalysisEngine.LogDebug($"  [MIR] Could not resolve Delegate Instance target directly via helper. Assuming IMPURE.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Instance.Syntax); // REVERTED: Assume impure
                    //return PurityAnalysisEngine.PurityAnalysisResult.Pure; // OLD (Incorrect): Assume pure, rely on CFG
                }
            }
            // *** END NEW ***

            // --- Direct Symbol Checks (More Robust) ---
            var containingType = originalDefinitionSymbol.ContainingType;
            if (containingType != null)
            {
                string typeName = containingType.Name;
                string namespaceName = containingType.ContainingNamespace?.ToDisplayString() ?? "";
                string symbolShortName = originalDefinitionSymbol.Name;

                // +++ Added Logging +++
                PurityAnalysisEngine.LogDebug($"  [MIR] Direct Check Values: Type='{typeName}', Namespace='{namespaceName}', Name='{symbolShortName}'");

                // Check for StringBuilder methods
                // +++ Added Logging +++
                PurityAnalysisEngine.LogDebug($"  [MIR] Checking direct rule for StringBuilder ('{typeName}' == 'StringBuilder' && '{namespaceName}' == 'System.Text')");
                if (typeName == "StringBuilder" && namespaceName == "System.Text")
                {
                    // --- Refinement: Exclude ToString() ---
                    if (symbolShortName == "ToString")
                    {
                        PurityAnalysisEngine.LogDebug($"  [MIR] --> PURE (Special case: StringBuilder.ToString())");
                        // Don't return yet, let known pure/impure lists handle it if necessary, or default
                        // Returning Pure here might be too strong if ToString() were ever impure.
                        // Let's allow further checks.
                    }
                    else
                    {
                        // Any OTHER StringBuilder method is considered impure by this direct check
                        PurityAnalysisEngine.LogDebug($"  [MIR] --> IMPURE (Direct check: StringBuilder method - Not ToString())");
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax);
                    }
                    // --- End Refinement ---
                }

                // Check for string.Format
                // +++ Added Logging +++
                PurityAnalysisEngine.LogDebug($"  [MIR] Checking direct rule for string.Format ('{typeName}' == 'String' && '{namespaceName}' == 'System' && '{symbolShortName}' == 'Format')");
                if (typeName == "String" && namespaceName == "System" && symbolShortName == "Format") // Use symbolShortName
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR] --> IMPURE (Direct check: string.Format)");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax);
                }

                // Add more direct checks if needed...
            }

            // --- Fallback Checks using Known Lists ---

            // 1. Check known impure methods
            // +++ Added Logging for signature passed +++
            string signatureForKnownImpure = invokedMethodSymbol.OriginalDefinition.ToDisplayString(); // Use the same format IsKnownImpure uses internally
            PurityAnalysisEngine.LogDebug($"  [MIR] Checking IsKnownImpure with signature: '{signatureForKnownImpure}'"); // Log signature BEFORE check
            bool isKnownImpureCheck = PurityAnalysisEngine.IsKnownImpure(invokedMethodSymbol);
            PurityAnalysisEngine.LogDebug($"  [MIR] IsKnownImpure result: {isKnownImpureCheck}");
            if (isKnownImpureCheck)
            {
                PurityAnalysisEngine.LogDebug($"  [MIR] --> IMPURE (Known Impure)");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax);
            }

            // 3. Check if method is in known impure namespace/type
            // Separate the checks for better logging
            PurityAnalysisEngine.LogDebug($"  [MIR] Checking IsInImpureNamespaceOrType for: {methodDisplayString}");
            bool isInImpureNsOrType = PurityAnalysisEngine.IsInImpureNamespaceOrType(invokedMethodSymbol);
            PurityAnalysisEngine.LogDebug($"  [MIR] IsInImpureNamespaceOrType result: {isInImpureNsOrType}");

            PurityAnalysisEngine.LogDebug($"  [MIR] Checking IsPureEnforced for {methodDisplayString}");
            bool isExplicitlyPure = PurityAnalysisEngine.IsPureEnforced(invokedMethodSymbol, context.EnforcePureAttributeSymbol);
            PurityAnalysisEngine.LogDebug($"  [MIR] IsPureEnforced result: {isExplicitlyPure}");

            if (isInImpureNsOrType && !isExplicitlyPure) // Allow if explicitly marked pure
            {
                PurityAnalysisEngine.LogDebug($"  [MIR] --> IMPURE (In Impure NS/Type and not explicitly Pure)");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax);
            }

            // Check for safe mutations on LOCAL collections
            bool isInstanceCall = invocationOperation.Instance != null && !invokedMethodSymbol.IsStatic;
            bool isLocalRef = invocationOperation.Instance is ILocalReferenceOperation;
            PurityAnalysisEngine.LogDebug($"  [MIR] Checking local mutation: IsInstanceCall={isInstanceCall}, IsLocalRef={isLocalRef}");

            if (isInstanceCall && isLocalRef)
            {
                var containingTypeName = invokedMethodSymbol.ContainingType?.ConstructedFrom.ToString() ?? ""; // Get base type name
                var methodIdentifier = $"{containingTypeName}.{invokedMethodSymbol.Name}";
                PurityAnalysisEngine.LogDebug($"  [MIR] Checking local mutation list for: {methodIdentifier}");

                if (_safeLocalMutationMethods.Contains(methodIdentifier))
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR] --> PURE (Safe local collection mutation: {methodDisplayString})");
                    return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                }
            }

            // *** START NEW: Analyze Delegate Arguments ***
            PurityAnalysisEngine.PurityAnalysisResult overallDelegateResult = PurityAnalysisEngine.PurityAnalysisResult.Pure;
            SyntaxNode firstImpureDelegateSyntax = null; // Store the syntax node of the first impure delegate

            PurityAnalysisEngine.LogDebug($"  [MIR] Analyzing arguments for delegates...");
            foreach (var argument in invocationOperation.Arguments)
            {
                PurityAnalysisEngine.LogDebug($"  [MIR] Checking argument: {argument.Syntax}");
                IOperation argumentValue = argument.Value;
                IMethodSymbol? delegateMethodSymbol = null; // Make nullable

                if (argumentValue is IAnonymousFunctionOperation anonFuncOp)
                {
                    // Check if Symbol is null
                    delegateMethodSymbol = anonFuncOp.Symbol;
                    if (delegateMethodSymbol != null)
                    {
                        PurityAnalysisEngine.LogDebug($"    [MIR] Argument is Anonymous Function: {delegateMethodSymbol.ToDisplayString()}");
                    }
                    else
                    {
                        PurityAnalysisEngine.LogDebug($"    [MIR] Argument is Anonymous Function but Symbol is NULL.");
                    }
                }
                else if (argumentValue is IDelegateCreationOperation delegateCreationOp)
                {
                    // Check if Target is null before accessing its members
                    if (delegateCreationOp.Target != null)
                    {
                        if (delegateCreationOp.Target is IMethodReferenceOperation methodRefOp)
                        {
                            // Check if Method is null
                            delegateMethodSymbol = methodRefOp.Method;
                            if (delegateMethodSymbol != null)
                            {
                                PurityAnalysisEngine.LogDebug($"    [MIR] Argument is Delegate Creation (Method Ref): {delegateMethodSymbol.ToDisplayString()}");
                            }
                            else
                            {
                                PurityAnalysisEngine.LogDebug($"    [MIR] Argument is Delegate Creation (Method Ref) but Method symbol is NULL.");
                            }
                        }
                        else if (delegateCreationOp.Target is IAnonymousFunctionOperation anonFuncOpTarget)
                        {
                            // Check if Symbol is null
                            delegateMethodSymbol = anonFuncOpTarget.Symbol;
                            if (delegateMethodSymbol != null)
                            {
                                PurityAnalysisEngine.LogDebug($"    [MIR] Argument is Delegate Creation (Anon Func): {delegateMethodSymbol.ToDisplayString()}");
                            }
                            else
                            {
                                PurityAnalysisEngine.LogDebug($"    [MIR] Argument is Delegate Creation (Anon Func) but Symbol is NULL.");
                            }
                        }
                        else
                        {
                            PurityAnalysisEngine.LogDebug($"    [MIR] Argument is Delegate Creation (Unknown Target): {delegateCreationOp.Target.Kind}");
                        }
                    }
                    else
                    {
                        PurityAnalysisEngine.LogDebug($"    [MIR] Argument is Delegate Creation but Target is NULL.");
                    }
                }
                else
                {
                    PurityAnalysisEngine.LogDebug($"    [MIR] Argument is not a delegate type ({argumentValue?.Kind}). Skipping.");
                }


                if (delegateMethodSymbol != null)
                {
                    PurityAnalysisEngine.LogDebug($"    [MIR] Recursively checking delegate: {delegateMethodSymbol.ToDisplayString()}");
                    // Use a *new* visited set for each delegate to allow separate recursive analysis paths
                    // var delegateVisitedMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default); // <-- REMOVED: Use context's visited set
                    var delegateResult = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                        delegateMethodSymbol.OriginalDefinition,
                        context.SemanticModel,
                        context.EnforcePureAttributeSymbol,
                        context.AllowSynchronizationAttributeSymbol,
                        context.VisitedMethods, // <-- Use the main visited set
                        context.PurityCache);

                    PurityAnalysisEngine.LogDebug($"    [MIR] Recursive delegate result for {delegateMethodSymbol.ToDisplayString()}: IsPure={delegateResult.IsPure}");
                    if (!delegateResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [MIR] --> Delegate IMPURE");
                        // Use the specific syntax node from the delegate's impurity if available
                        firstImpureDelegateSyntax = delegateResult.ImpureSyntaxNode ?? argument.Syntax;
                        overallDelegateResult = PurityAnalysisEngine.PurityAnalysisResult.Impure(firstImpureDelegateSyntax);
                        break; // Found impurity, no need to check other delegates
                    }
                    else
                    {
                        PurityAnalysisEngine.LogDebug($"    [MIR] --> Delegate PURE");
                    }
                }
            }

            if (!overallDelegateResult.IsPure)
            {
                PurityAnalysisEngine.LogDebug($"  [MIR] --> IMPURE (Impure delegate argument found)");
                // Return the result based on the first impure delegate found
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(firstImpureDelegateSyntax ?? invocationOperation.Syntax); // Use delegate node or fallback
            }
            PurityAnalysisEngine.LogDebug($"  [MIR] All delegate arguments analyzed and found pure (or no delegates). Proceeding to check target method.");
            // *** END NEW: Analyze Delegate Arguments ***

            // 2. Check known pure BCL methods (Moved after delegate check)
            // +++ Add Logging for signature passed +++
            string signatureForKnownPure = invokedMethodSymbol.OriginalDefinition.ToDisplayString(); // Use the same format
            PurityAnalysisEngine.LogDebug($"  [MIR] Checking IsKnownPureBCLMember with signature: '{signatureForKnownPure}'"); // Log signature BEFORE check
            bool isKnownPureCheck = PurityAnalysisEngine.IsKnownPureBCLMember(invokedMethodSymbol);
            PurityAnalysisEngine.LogDebug($"  [MIR] IsKnownPureBCLMember result: {isKnownPureCheck}");
            if (isKnownPureCheck)
            {
                // If the method is known pure AND delegates were pure, the call is pure.
                PurityAnalysisEngine.LogDebug($"  [MIR] --> PURE (Known Pure BCL and Delegates were Pure)");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            // 4. Check for recursive calls to methods marked with [EnforcePure]
            PurityAnalysisEngine.LogDebug($"  [MIR] Checking IsPureEnforced (again, for recursion) for: {methodDisplayString}");
            if (isExplicitlyPure) // Re-use check from above
            {
                PurityAnalysisEngine.LogDebug($"  [MIR] Recursively checking [EnforcePure] method: {methodDisplayString}");
                // Use the context's cache and visited set for recursion
                // Pass the *original* context's visited set here to detect recursion in the main call chain
                var recursiveResult = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                    invokedMethodSymbol.OriginalDefinition, // Analyze the original definition
                    context.SemanticModel, // Use same model
                    context.EnforcePureAttributeSymbol,
                    context.AllowSynchronizationAttributeSymbol,
                    context.VisitedMethods, // Use main call chain visited set
                    context.PurityCache); // Correct property name

                PurityAnalysisEngine.LogDebug($"  [MIR] Recursive result for {methodDisplayString}: IsPure={recursiveResult.IsPure}");
                // If the recursive call is impure, the current method is impure.
                // Use the ImpureSyntaxNode from the recursive call if available, otherwise use the current invocation syntax.
                if (!recursiveResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR] --> IMPURE (Recursive call was impure)");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(recursiveResult.ImpureSyntaxNode ?? invocationOperation.Syntax);
                }
                else
                {
                    // If the target method is pure AND delegates were pure, the call is pure.
                    PurityAnalysisEngine.LogDebug($"  [MIR] --> PURE (Recursive call was pure and Delegates were Pure)");
                    return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                }
            }

            // 5. Default: Assume Impure
            PurityAnalysisEngine.LogDebug($"  [MIR] No other rule applied. Defaulting to IMPURE for: {methodDisplayString}");
            return PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax);
        }

        // *** NEW HELPER METHOD ***
        private static IMethodSymbol? ResolveDelegateInstanceTarget(IOperation delegateInstance, PurityAnalysisContext context)
        {
            PurityAnalysisEngine.LogDebug($"    [RDI] Attempting to resolve target for Instance Kind: {delegateInstance.Kind}, Syntax: {delegateInstance.Syntax}");

            // 1. Direct Assignment or Direct Creation
            if (delegateInstance is IVariableDeclaratorOperation variableDeclarator && variableDeclarator.Initializer?.Value is IDelegateCreationOperation creationOp)
            {
                return ResolveDelegateCreationTarget(creationOp);
            }
            if (delegateInstance is ISimpleAssignmentOperation assignment && assignment.Value is IDelegateCreationOperation creationOpFromAssignment)
            {
                return ResolveDelegateCreationTarget(creationOpFromAssignment);
            }
            if (delegateInstance is IDelegateCreationOperation directCreationOp)
            {
                return ResolveDelegateCreationTarget(directCreationOp);
            }

            // 2. Instance is a Parameter Reference
            if (delegateInstance is IParameterReferenceOperation paramRef)
            {
                // Cannot resolve origin reliably within this rule.
                PurityAnalysisEngine.LogDebug($"    [RDI] Instance is Parameter Reference ({paramRef.Parameter.Name}). Cannot resolve origin within this rule. Returning null.");
                return null;
            }

            // 3. Instance is a Local Variable Reference
            if (delegateInstance is ILocalReferenceOperation localRef)
            {
                // Cannot find the assignment reliably within this rule.
                PurityAnalysisEngine.LogDebug($"    [RDI] Instance is Local Reference ({localRef.Local.Name}). Cannot resolve origin within this rule. Returning null.");
                // *** REMOVED Attempt to FindAssignmentToLocal ***
                return null;
            }

            // 4. Instance is a Method Invocation returning a delegate
            if (delegateInstance is IInvocationOperation invocationSource)
            {
                var sourceMethod = invocationSource.TargetMethod;
                PurityAnalysisEngine.LogDebug($"    [RDI] Instance is Invocation of '{sourceMethod.ToDisplayString()}' which returns a delegate type. Recursively checking source method.");
                // Check the purity of the method *returning* the delegate.
                // If the *source method* is pure, we assume the delegate it returns is also suitable for pure invocation.
                // This assumes the source method itself doesn't return an impure delegate based on its *own* logic.
                var sourceMethodPurity = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                    sourceMethod.OriginalDefinition,
                    context.SemanticModel,
                    context.EnforcePureAttributeSymbol,
                    context.AllowSynchronizationAttributeSymbol,
                    context.VisitedMethods,
                    context.PurityCache);

                if (!sourceMethodPurity.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [RDI] Source method '{sourceMethod.Name}' returning delegate is IMPURE. Assuming invocation is impure.");
                    return null; // Source method is impure, so delegate invocation is impure
                }
                else
                {
                    // The source method is pure, but what *is* the actual delegate target it returned?
                    // This requires analyzing the *body* of the source method, specifically its return statements.
                    // This is getting too complex for this rule.
                    // Let's make a simplifying assumption: If the method returning the delegate is pure,
                    // trust that the invocation of the returned delegate is also pure *unless* the delegate itself captures state.
                    // The recursive check on the lambda body should handle captures.
                    // We need to find the *actual* lambda/method group returned by the source method.
                    // Example: Find 'x => x * factor' inside 'CreateMultiplier'.
                    // This requires analyzing the return operations of 'sourceMethod'.

                    var returnedDelegateTarget = FindReturnedDelegateTarget(sourceMethod, context);
                    if (returnedDelegateTarget != null)
                    {
                        PurityAnalysisEngine.LogDebug($"    [RDI] Found actual delegate target returned by '{sourceMethod.Name}': {returnedDelegateTarget.ToDisplayString()}");
                        return returnedDelegateTarget;
                    }
                    else
                    {
                        PurityAnalysisEngine.LogDebug($"    [RDI] Source method '{sourceMethod.Name}' is PURE, but could not determine exact returned delegate target. Assuming impure.");
                        return null; // Could not find the specific returned delegate
                    }
                }
            }

            // Add more cases as needed (FieldReference, PropertyReference, etc.)

            PurityAnalysisEngine.LogDebug($"    [RDI] Could not resolve delegate instance target from operation kind {delegateInstance.Kind}.");
            return null; // Unhandled case
        }

        // *** NEW HELPER METHOD ***
        private static IMethodSymbol? ResolveDelegateCreationTarget(IDelegateCreationOperation creationOp)
        {
            PurityAnalysisEngine.LogDebug($"      [RDCT] Resolving DelegateCreationOperation. Target Kind: {creationOp.Target?.Kind}");
            if (creationOp.Target is IAnonymousFunctionOperation anonFunc)
            {
                PurityAnalysisEngine.LogDebug($"      [RDCT] --> Anonymous Function: {anonFunc.Symbol?.ToDisplayString()}");
                return anonFunc.Symbol;
            }
            if (creationOp.Target is IMethodReferenceOperation methodRef)
            {
                PurityAnalysisEngine.LogDebug($"      [RDCT] --> Method Reference: {methodRef.Method?.ToDisplayString()}");
                return methodRef.Method;
            }
            PurityAnalysisEngine.LogDebug($"      [RDCT] --> Could not resolve target from DelegateCreationOperation.");
            return null;
        }

        // *** PLACEHOLDER HELPER *** (Needs proper implementation)
        private static IOperation? FindAssignmentToLocal(ILocalReferenceOperation localRef, PurityAnalysisContext context)
        {
            // This is a placeholder. A real implementation would require walking the
            // operation tree backwards or using flow analysis data if available.
            // For now, return null as we can't reliably find the assignment here.
            PurityAnalysisEngine.LogDebug($"      [FATL] Placeholder FindAssignmentToLocal for '{localRef.Local.Name}'. Cannot resolve reliably.");
            return null;
        }

        // *** PLACEHOLDER HELPER *** (Needs proper implementation)
        private static IMethodSymbol? FindReturnedDelegateTarget(IMethodSymbol sourceMethod, PurityAnalysisContext context)
        {
            // This is complex. Need to get the IOperation for the sourceMethod's body,
            // find all IReturnOperation nodes, and analyze their ReturnedValue.
            // If the ReturnedValue is an IDelegateCreationOperation, resolve its target.
            PurityAnalysisEngine.LogDebug($"      [FRDT] Placeholder FindReturnedDelegateTarget for '{sourceMethod.Name}'. Complex analysis needed.");

            // Simplistic Example: Try getting body and finding first return
            var bodySyntax = sourceMethod.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken);
            if (bodySyntax != null)
            {
                var sourceMethodModel = context.SemanticModel.Compilation.GetSemanticModel(bodySyntax.SyntaxTree); // Need model for this tree
                var bodyOperation = sourceMethodModel.GetOperation(bodySyntax, context.CancellationToken);
                if (bodyOperation != null)
                {
                    var returnOperation = bodyOperation.DescendantsAndSelf().OfType<IReturnOperation>().FirstOrDefault();
                    if (returnOperation?.ReturnedValue is IDelegateCreationOperation creationOp)
                    {
                        var target = ResolveDelegateCreationTarget(creationOp);
                        PurityAnalysisEngine.LogDebug($"      [FRDT] Found target in first return: {target?.ToDisplayString()}");
                        return target;
                    }
                }
            }

            return null; // Placeholder
        }
    }
}