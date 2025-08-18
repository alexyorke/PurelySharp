using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using PurelySharp.Analyzer.Engine;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PurelySharp.Analyzer.Engine.Rules
{

    internal class MethodInvocationPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Invocation);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IInvocationOperation invocationOperation))
            {
                PurityAnalysisEngine.LogDebug("  [MIR] WARNING: Called with non-invocation.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            var invokedMethodSymbol = invocationOperation.TargetMethod;
            if (invokedMethodSymbol == null)
            {
                PurityAnalysisEngine.LogDebug("  [MIR] Cannot resolve target method. Assuming impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax);
            }


            if (invokedMethodSymbol.Name == "Invoke" && invokedMethodSymbol.ContainingType?.TypeKind == TypeKind.Delegate)
            {

                PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] === Simplified Delegate Invocation Check Start ===");
                PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Invoked Symbol: {invokedMethodSymbol.ContainingType.Name}.Invoke()");

                if (invocationOperation.Instance == null)
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Instance is NULL (static delegate?). Assuming impure.");
                    return PurityAnalysisEngine.ImpureResult(invocationOperation.Syntax);
                }

                PurityAnalysisEngine.PurityAnalysisResult result = PurityAnalysisEngine.PurityAnalysisResult.Pure;
                IOperation delegateInstanceOp = invocationOperation.Instance;
                PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Analyzing Delegate Instance Op: {delegateInstanceOp.Kind} | Syntax: {delegateInstanceOp.Syntax}");


                ISymbol? delegateInstanceSymbol = TryResolveSymbol(delegateInstanceOp);
                if (delegateInstanceSymbol != null)
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Resolved Instance Symbol: {delegateInstanceSymbol.ToDisplayString()} ({delegateInstanceSymbol.Kind})");
                    if (currentState.DelegateTargetMap.TryGetValue(delegateInstanceSymbol, out var potentialTargets))
                    {
                        PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S-MAP] Found entry for {delegateInstanceSymbol.Name} in map.");
                        PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S-MAP]   Targets: [{string.Join(", ", potentialTargets.MethodSymbols.Select(m => m.Name))}]");
                        PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Found {potentialTargets.MethodSymbols.Count} potential target(s) in DFA map for {delegateInstanceSymbol.Name}.");
                        if (potentialTargets.MethodSymbols.IsEmpty)
                        {
                            PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] --> Map entry is empty. Assuming PURE.");
                            result = PurityAnalysisEngine.PurityAnalysisResult.Pure;
                        }
                        else
                        {
                            result = PurityAnalysisEngine.PurityAnalysisResult.Pure;
                            foreach (var targetMethod in potentialTargets.MethodSymbols)
                            {
                                PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Checking Potential Target from Map: {targetMethod.ToDisplayString()}");
                                var targetPurity = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                                    targetMethod.OriginalDefinition, context.SemanticModel, context.EnforcePureAttributeSymbol, context.AllowSynchronizationAttributeSymbol,
                                    context.VisitedMethods, context.PurityCache);
                                PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Potential Target Purity Result: IsPure={targetPurity.IsPure}");
                                if (!targetPurity.IsPure)
                                {
                                    PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] --> IMPURE target found in map. Invocation is impure.");
                                    result = targetPurity;
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S-MAP] *** NO entry found for {delegateInstanceSymbol.Name} in map. Assuming impure. ***");
                        PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] --> IMPURE (Symbol {delegateInstanceSymbol.Name} NOT FOUND in DFA state map - untracked source). Fallback to PS0002 at invocation.");
                        result = PurityAnalysisEngine.ImpureResult(invocationOperation.Syntax);
                    }
                }
                else
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] --> IMPURE (Could not resolve instance {delegateInstanceOp.Kind} to a trackable symbol). Fallback to PS0002 at instance op.");
                    result = PurityAnalysisEngine.ImpureResult(delegateInstanceOp.Syntax);
                }

                PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Final Result for Delegate Invocation: IsPure={result.IsPure}");
                PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] === Simplified Delegate Invocation Check End ===");
                return result;
            }



            if (invokedMethodSymbol.IsExtensionMethod &&
                SymbolEqualityComparer.Default.Equals(invokedMethodSymbol.ContainingType?.OriginalDefinition, context.SemanticModel.Compilation.GetTypeByMetadataName("System.Linq.Enumerable")))
            {
                PurityAnalysisEngine.LogDebug($"  [MIR] Detected LINQ Enumerable extension method: {invokedMethodSymbol.Name}. Checking source and delegate arguments.");




                if (invocationOperation.Arguments.Length > 0)
                {
                    var sourceArgument = invocationOperation.Arguments[0];
                    PurityAnalysisEngine.LogDebug($"  [MIR]   Checking LINQ source argument purity: {sourceArgument.Value.Kind}");
                    var sourceResult = PurityAnalysisEngine.CheckSingleOperation(sourceArgument.Value, context, currentState);

                    if (!sourceResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"  [MIR] --> IMPURE (LINQ source argument was impure)");

                        return sourceResult;
                    }
                }
                else
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR]   WARNING: LINQ method {invokedMethodSymbol.Name} called with no arguments? Assuming impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax);
                }


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
                                firstImpureDelegateResult = delegateArgResult;
                                break;
                            }
                        }
                        else
                        {
                            PurityAnalysisEngine.LogDebug($"  [MIR]   WARNING: Could not find argument corresponding to LINQ delegate parameter {parameter.Name}. Assuming impure.");
                            allDelegatesPure = false;
                            firstImpureDelegateResult = PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax);
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

                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(firstImpureDelegateResult.ImpureSyntaxNode ?? invocationOperation.Syntax);
                }
            }





            if (invokedMethodSymbol.IsStatic && invokedMethodSymbol.ContainingType != null)
            {
                var cctorResult = PurityAnalysisEngine.CheckStaticConstructorPurity(invokedMethodSymbol.ContainingType, context, currentState);
                if (!cctorResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR] Static method call '{invokedMethodSymbol.Name}' IMPURE due to impure static constructor in {invokedMethodSymbol.ContainingType.Name}.");

                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax);
                }
            }


            if (invocationOperation.Instance != null)
            {
                PurityAnalysisEngine.LogDebug($"  [MIR] Checking instance purity for {invocationOperation.Instance.Kind}: {invocationOperation.Instance.Syntax.ToString().Trim()}");
                var instanceResult = PurityAnalysisEngine.CheckSingleOperation(invocationOperation.Instance, context, currentState);
                PurityAnalysisEngine.LogDebug($"  [MIR] Instance check result: IsPure={instanceResult.IsPure}, Node Type={instanceResult.ImpureSyntaxNode?.GetType().Name ?? "NULL"}");
                if (!instanceResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR] --> IMPURE (Instance is impure)");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(instanceResult.ImpureSyntaxNode ?? invocationOperation.Instance.Syntax);
                }
            }


            PurityAnalysisEngine.LogDebug($"  [MIR] Checking purity of {invocationOperation.Arguments.Length} arguments for {invokedMethodSymbol.OriginalDefinition.Name}.");
            foreach (var argument in invocationOperation.Arguments)
            {
                PurityAnalysisEngine.LogDebug($"  [MIR]   Checking argument: {argument.Value.Kind} | Syntax: {argument.Value.Syntax.ToString().Trim()}");
                var argumentResult = PurityAnalysisEngine.CheckSingleOperation(argument.Value, context, currentState);
                PurityAnalysisEngine.LogDebug($"  [MIR]   Argument check result: IsPure={argumentResult.IsPure}");
                if (!argumentResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR] --> IMPURE (Argument is impure)");

                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(argumentResult.ImpureSyntaxNode ?? argument.Value.Syntax);
                }
            }



            var originalDefinitionSymbol = invokedMethodSymbol.OriginalDefinition;
            

            string methodDisplayString = originalDefinitionSymbol.ToDisplayString();
            PurityAnalysisEngine.LogDebug($"  [MIR] Analyzing regular call to: {methodDisplayString} | Syntax: {invocationOperation.Syntax}");




            PurityAnalysisEngine.LogDebug($"  [MIR] Checking IsKnownImpure with signature: '{originalDefinitionSymbol.ToDisplayString()}'");
            if (PurityAnalysisEngine.IsKnownImpure(originalDefinitionSymbol))
            {
                PurityAnalysisEngine.LogDebug($"  [MIR] --> IMPURE (Known Impure)");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax);
            }


            PurityAnalysisEngine.LogDebug($"  [MIR] Checking IsKnownPureBCLMember with signature: '{originalDefinitionSymbol.ToDisplayString()}'");
            if (PurityAnalysisEngine.IsKnownPureBCLMember(originalDefinitionSymbol))
            {
                PurityAnalysisEngine.LogDebug($"  [MIR] --> PURE (Known Pure BCL)");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }


            bool isExplicitlyPure = PurityAnalysisEngine.IsPureEnforced(invokedMethodSymbol, context.EnforcePureAttributeSymbol);
            if (PurityAnalysisEngine.IsInImpureNamespaceOrType(originalDefinitionSymbol) && !isExplicitlyPure)
            {
                PurityAnalysisEngine.LogDebug($"  [MIR] --> IMPURE (In Impure NS/Type and not explicitly Pure)");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax);
            }


            PurityAnalysisEngine.LogDebug($"  [MIR] Performing recursive check for: {methodDisplayString}");
            var recursiveResult = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                originalDefinitionSymbol,
                context.SemanticModel,
                context.EnforcePureAttributeSymbol,
                context.AllowSynchronizationAttributeSymbol,
                context.VisitedMethods,
                context.PurityCache);

            PurityAnalysisEngine.LogDebug($"  [MIR] Recursive check result for {methodDisplayString}: IsPure={recursiveResult.IsPure}");


            return recursiveResult.IsPure
                ? PurityAnalysisEngine.PurityAnalysisResult.Pure
                : PurityAnalysisEngine.PurityAnalysisResult.Impure(recursiveResult.ImpureSyntaxNode ?? invocationOperation.Syntax);
        }




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

                default:

                    PurityAnalysisEngine.LogDebug($"  [TryResolveSymbol] Could not resolve symbol for operation kind: {operation.Kind}");
                    return null;
            }
        }




    }
}