using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using PurelySharp.Analyzer.Engine;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

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
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(
                    invocationOperation.Syntax,
                    PurityAnalysisEngine.PurityEvidence.Create(
                        "unsupported_operation",
                        nameof(MethodInvocationPurityRule),
                        invocationOperation));
            }

            if (IsCompilerGeneratedArrayForeachInvocation(invocationOperation, context))
            {
                PurityAnalysisEngine.LogDebug("  [MIR] Compiler-generated array foreach member is treated as pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            if (invocationOperation.Instance != null && IsDynamicInvocationReceiver(invocationOperation.Instance))
            {
                PurityAnalysisEngine.LogDebug("  [MIR] Invocation on dynamic instance is treated as conservative impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(
                    invocationOperation.Syntax,
                    PurityAnalysisEngine.PurityEvidence.Create(
                        "dynamic_dispatch",
                        nameof(MethodInvocationPurityRule),
                        invocationOperation,
                        symbol: invokedMethodSymbol));
            }


            if (invokedMethodSymbol.Name == "Invoke" && invokedMethodSymbol.ContainingType?.TypeKind == TypeKind.Delegate)
            {

                PurityAnalysisEngine.LogDebug("  [MIR-DEL-S] === Simplified Delegate Invocation Check Start ===");
                PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Invoked Symbol: {invokedMethodSymbol.ContainingType.Name}.Invoke()");

                if (invocationOperation.Instance == null)
                {
                    PurityAnalysisEngine.LogDebug("  [MIR-DEL-S] Instance is NULL (static delegate?). Assuming impure.");
                    return PurityAnalysisEngine.ImpureResult(
                        invocationOperation.Syntax,
                        PurityAnalysisEngine.PurityEvidence.Create(
                            "unresolved_delegate_target",
                            nameof(MethodInvocationPurityRule),
                            invocationOperation,
                            symbol: invokedMethodSymbol));
                }

                PurityAnalysisEngine.PurityAnalysisResult result = PurityAnalysisEngine.PurityAnalysisResult.Pure;
                IOperation delegateInstanceOp = invocationOperation.Instance;
                PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Analyzing Delegate Instance Op: {delegateInstanceOp.Kind} | Syntax: {delegateInstanceOp.Syntax}");

                var potentialTargets = PurityAnalysisEngine.ResolvePotentialTargets(delegateInstanceOp, currentState, context.SemanticModel);
                if (potentialTargets != null)
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Resolved {potentialTargets.Value.MethodSymbols.Count} target(s) for delegate invocation.");
                    if (potentialTargets.Value.IsUnresolved || potentialTargets.Value.MethodSymbols.IsEmpty)
                    {
                        PurityAnalysisEngine.LogDebug("  [MIR-DEL-S] --> Resolved target set is empty or explicitly unresolved. Treating as unresolved delegate target.");
                        result = PurityAnalysisEngine.ImpureResult(
                            delegateInstanceOp.Syntax,
                            PurityAnalysisEngine.PurityEvidence.Create(
                                "unresolved_delegate_target",
                                nameof(MethodInvocationPurityRule),
                                delegateInstanceOp,
                                symbol: invokedMethodSymbol));
                    }
                    else
                    {
                        result = PurityAnalysisEngine.PurityAnalysisResult.Pure;
                        foreach (var targetMethod in potentialTargets.Value.MethodSymbols)
                        {
                            PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Checking Potential Target: {targetMethod.ToDisplayString()}");
                            var targetPurity = PurityAnalysisEngine.GetCalleePurity(targetMethod, context);
                            PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Potential Target Purity Result: IsPure={targetPurity.IsPure}");
                            if (!targetPurity.IsPure)
                            {
                                PurityAnalysisEngine.LogDebug("  [MIR-DEL-S] --> IMPURE target found. Invocation is impure.");
                                result = targetPurity;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] --> IMPURE (Could not resolve delegate targets for {delegateInstanceOp.Kind}). Fallback to PS0002 at instance op.");
                    result = PurityAnalysisEngine.ImpureResult(
                        delegateInstanceOp.Syntax,
                        PurityAnalysisEngine.PurityEvidence.Create(
                            "unresolved_delegate_target",
                            nameof(MethodInvocationPurityRule),
                            delegateInstanceOp,
                            symbol: invokedMethodSymbol));
                }

                PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Final Result for Delegate Invocation: IsPure={result.IsPure}");
                PurityAnalysisEngine.LogDebug("  [MIR-DEL-S] === Simplified Delegate Invocation Check End ===");
                if (result.IsPure)
                {
                    foreach (var argument in invocationOperation.Arguments)
                    {
                        var argumentResult = PurityAnalysisEngine.CheckSingleOperation(argument.Value, context, currentState);
                        if (!argumentResult.IsPure)
                        {
                            PurityAnalysisEngine.LogDebug("  [MIR-DEL-S] --> IMPURE (Delegate invocation argument is impure)");
                            return PurityAnalysisEngine.PurityAnalysisResult.Impure(
                                argumentResult.ImpureSyntaxNode ?? argument.Value.Syntax,
                                argumentResult.Evidence);
                        }
                    }
                }

                return result;
            }



            if (invokedMethodSymbol.IsExtensionMethod &&
                invocationOperation.Arguments.Length > 0 &&
                IsDynamicInvocationReceiver(invocationOperation.Arguments[0].Value))
            {
                PurityAnalysisEngine.LogDebug("  [MIR] Extension invocation on dynamic receiver is treated as conservative impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(
                    invocationOperation.Syntax,
                    PurityAnalysisEngine.PurityEvidence.Create(
                        "dynamic_dispatch",
                        nameof(MethodInvocationPurityRule),
                        invocationOperation,
                        symbol: invokedMethodSymbol));
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

                    var sourceEnumeratorResult = CheckLinqSourceEnumeratorPurity(sourceArgument.Value, context);
                    if (!sourceEnumeratorResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug("  [MIR] --> IMPURE (LINQ source GetEnumerator was impure)");
                        return sourceEnumeratorResult;
                    }
                }
                else
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR]   WARNING: LINQ method {invokedMethodSymbol.Name} called with no arguments? Assuming impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(
                        invocationOperation.Syntax,
                        PurityAnalysisEngine.PurityEvidence.Create(
                            "unsupported_operation",
                            nameof(MethodInvocationPurityRule),
                            invocationOperation,
                            symbol: invokedMethodSymbol));
                }


                PurityAnalysisEngine.LogDebug("  [MIR]   LINQ source was pure. Checking remaining arguments...");
                for (int argumentIndex = 1; argumentIndex < invocationOperation.Arguments.Length; argumentIndex++)
                {
                    var argument = invocationOperation.Arguments[argumentIndex];
                    var parameter = argument.Parameter;
                    var argumentKind = parameter?.Type?.TypeKind == TypeKind.Delegate ? "delegate" : "non-delegate";
                    PurityAnalysisEngine.LogDebug($"  [MIR]   Checking LINQ {argumentKind} argument '{parameter?.Name ?? "<unknown>"}' (Arg Index {argumentIndex}) for operation: {argument.Value.Kind}");

                    var argumentResult = PurityAnalysisEngine.CheckSingleOperation(argument.Value, context, currentState);
                    PurityAnalysisEngine.LogDebug($"  [MIR]   LINQ argument '{parameter?.Name ?? "<unknown>"}' result: IsPure={argumentResult.IsPure}");
                    if (!argumentResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug("  [MIR] --> IMPURE (LINQ method, impure argument detected)");
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(
                            argumentResult.ImpureSyntaxNode ?? argument.Value.Syntax,
                            argumentResult.Evidence);
                    }
                }

                PurityAnalysisEngine.LogDebug("  [MIR] LINQ source and all remaining arguments determined to be pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }


            if (IsPotentiallyDispatchedMethod(invokedMethodSymbol)
                && (invokedMethodSymbol.IsStatic
                    ? invocationOperation.Instance == null
                    : invocationOperation.Instance != null
                        && !IsBaseReference(invocationOperation.Instance)))
            {
                var knownReceiverType = GetKnownReceiverType(invocationOperation.Instance);
                if (knownReceiverType == null)
                {
                    knownReceiverType = GetKnownStaticInterfaceReceiverType(invokedMethodSymbol);
                }

                PurityAnalysisEngine.LogDebug($"  [MIR] Checking potential dispatch candidates for {invokedMethodSymbol.Name}.");
                var dispatchResult = CheckDispatchedInvocationPurity(
                    invocationOperation,
                    context,
                    knownReceiverType);
                if (!dispatchResult.IsPure)
                {
                    return dispatchResult;
                }
            }


            if (invokedMethodSymbol.IsStatic && invokedMethodSymbol.ContainingType != null)
            {
                var staticOriginalDefinitionSymbol = invokedMethodSymbol.OriginalDefinition;
                if (PurityAnalysisEngine.HasImpureAttribute(staticOriginalDefinitionSymbol))
                {
                    PurityAnalysisEngine.LogDebug("  [MIR] --> IMPURE ([Impure] boundary attribute)");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(
                        invocationOperation.Syntax,
                        PurityAnalysisEngine.PurityEvidence.Create(
                            "impure_boundary_attribute",
                            nameof(MethodInvocationPurityRule),
                            invocationOperation,
                            symbol: staticOriginalDefinitionSymbol,
                            catalogSource: "attribute"));
                }

            }

            if (invokedMethodSymbol.IsStatic && invokedMethodSymbol.ContainingType != null)
            {
                var cctorResult = PurityAnalysisEngine.CheckStaticConstructorPurity(invokedMethodSymbol.ContainingType, context, currentState);
                if (!cctorResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR] Static method call '{invokedMethodSymbol.Name}' IMPURE due to impure static constructor in {invokedMethodSymbol.ContainingType.Name}.");

                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(
                        cctorResult.ImpureSyntaxNode ?? invocationOperation.Syntax,
                        cctorResult.Evidence);
                }
            }


            if (invocationOperation.Instance != null
                && !IsBaseReference(invocationOperation.Instance)
                && invocationOperation.Instance is not IConditionalAccessInstanceOperation)
            {
                PurityAnalysisEngine.LogDebug($"  [MIR] Checking instance purity for {invocationOperation.Instance.Kind}: {invocationOperation.Instance.Syntax.ToString().Trim()}");
                var instanceResult = PurityAnalysisEngine.CheckSingleOperation(invocationOperation.Instance, context, currentState);
                PurityAnalysisEngine.LogDebug($"  [MIR] Instance check result: IsPure={instanceResult.IsPure}, Node Type={instanceResult.ImpureSyntaxNode?.GetType().Name ?? "NULL"}");
                if (!instanceResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug("  [MIR] --> IMPURE (Instance is impure)");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(
                        instanceResult.ImpureSyntaxNode ?? invocationOperation.Instance.Syntax,
                        instanceResult.Evidence);
                }
            }


            var originalDefinitionSymbol = invokedMethodSymbol.OriginalDefinition;
            if (PurityAnalysisEngine.HasImpureAttribute(originalDefinitionSymbol))
            {
                PurityAnalysisEngine.LogDebug("  [MIR] --> IMPURE ([Impure] boundary attribute)");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(
                    invocationOperation.Syntax,
                    PurityAnalysisEngine.PurityEvidence.Create(
                        "impure_boundary_attribute",
                        nameof(MethodInvocationPurityRule),
                        invocationOperation,
                        symbol: originalDefinitionSymbol,
                        catalogSource: "attribute"));
            }

            PurityAnalysisEngine.LogDebug($"  [MIR] Checking purity of {invocationOperation.Arguments.Length} arguments for {originalDefinitionSymbol.Name}.");
            foreach (var argument in invocationOperation.Arguments)
            {
                if (argument.Parameter?.RefKind == RefKind.Out &&
                    PurityAnalysisEngine.IsKnownPureBCLMember(originalDefinitionSymbol))
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR]   Skipping purity check for out argument '{argument.Syntax}' on known pure member {originalDefinitionSymbol.ToDisplayString()}.");
                    continue;
                }

                PurityAnalysisEngine.LogDebug($"  [MIR]   Checking argument: {argument.Value.Kind} | Syntax: {argument.Value.Syntax.ToString().Trim()}");
                var argumentResult = PurityAnalysisEngine.CheckSingleOperation(argument.Value, context, currentState);
                PurityAnalysisEngine.LogDebug($"  [MIR]   Argument check result: IsPure={argumentResult.IsPure}");
                if (!argumentResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug("  [MIR] --> IMPURE (Argument is impure)");

                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(
                        argumentResult.ImpureSyntaxNode ?? argument.Value.Syntax,
                        argumentResult.Evidence);
                }
            }

            if (PurityAnalysisEngine.HasPureExternalAttribute(originalDefinitionSymbol))
            {
                PurityAnalysisEngine.LogDebug("  [MIR] --> PURE ([PureExternal] boundary attribute)");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }



            string methodDisplayString = originalDefinitionSymbol.ToDisplayString();
            PurityAnalysisEngine.LogDebug($"  [MIR] Analyzing regular call to: {methodDisplayString} | Syntax: {invocationOperation.Syntax}");



            PurityAnalysisEngine.LogDebug($"  [MIR] Checking IsKnownImpure with signature: '{originalDefinitionSymbol.ToDisplayString()}'");
            if (PurityAnalysisEngine.IsKnownImpure(originalDefinitionSymbol))
            {
                PurityAnalysisEngine.LogDebug("  [MIR] --> IMPURE (Known Impure)");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(
                    invocationOperation.Syntax,
                    PurityAnalysisEngine.PurityEvidence.Create(
                        GetCatalogHitCategory(originalDefinitionSymbol),
                        nameof(MethodInvocationPurityRule),
                        invocationOperation,
                        symbol: originalDefinitionSymbol,
                        catalogSource: PurityAnalysisEngine.GetKnownImpureMemberSource(originalDefinitionSymbol) ?? "known_impure"));
            }


            PurityAnalysisEngine.LogDebug($"  [MIR] Checking IsKnownPureBCLMember with signature: '{originalDefinitionSymbol.ToDisplayString()}'");
            if (PurityAnalysisEngine.IsKnownPureBCLMember(originalDefinitionSymbol))
            {
                PurityAnalysisEngine.LogDebug("  [MIR] --> PURE (Known Pure BCL)");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }


            bool isExplicitlyPure = PurityAnalysisEngine.IsPureEnforced(
                invokedMethodSymbol,
                context.EnforcePureAttributeSymbol,
                context.PureAttributeSymbol);
            if (PurityAnalysisEngine.IsInImpureNamespaceOrType(originalDefinitionSymbol) && !isExplicitlyPure)
            {
                PurityAnalysisEngine.LogDebug("  [MIR] --> IMPURE (In Impure NS/Type and not explicitly Pure)");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(
                    invocationOperation.Syntax,
                    PurityAnalysisEngine.PurityEvidence.Create(
                        GetCatalogHitCategory(originalDefinitionSymbol),
                        nameof(MethodInvocationPurityRule),
                        invocationOperation,
                        symbol: originalDefinitionSymbol,
                        catalogSource: "known_impure_namespace_or_type"));
            }


            PurityAnalysisEngine.LogDebug($"  [MIR] Performing purity check for: {methodDisplayString}");

            var calleePurity = PurityAnalysisEngine.GetCalleePurity(originalDefinitionSymbol, context);

            PurityAnalysisEngine.LogDebug($"  [MIR] Callee purity result for {methodDisplayString}: IsPure={calleePurity.IsPure}");

            return calleePurity.IsPure
                ? PurityAnalysisEngine.PurityAnalysisResult.Pure
                : calleePurity.WithCallee(originalDefinitionSymbol, invocationOperation.Syntax);
        }

        private static bool IsPotentiallyDispatchedMethod(IMethodSymbol methodSymbol)
        {
            return methodSymbol.ContainingType?.TypeKind == TypeKind.Interface
                || methodSymbol.IsVirtual
                || methodSymbol.IsAbstract
                || methodSymbol.IsOverride;
        }

        private static bool IsCompilerGeneratedArrayForeachInvocation(
            IInvocationOperation invocationOperation,
            PurityAnalysisContext context)
        {
            if (invocationOperation.TargetMethod.Parameters.Length != 0 ||
                !IsArrayForeachSyntax(invocationOperation.Syntax, context))
            {
                return false;
            }

            return invocationOperation.TargetMethod.Name switch
            {
                nameof(IDisposable.Dispose) => invocationOperation.TargetMethod.ContainingType?.SpecialType == SpecialType.System_IDisposable,
                "GetEnumerator" => invocationOperation.TargetMethod.ContainingType?.ToDisplayString() == "System.Collections.IEnumerable",
                "MoveNext" => invocationOperation.TargetMethod.ContainingType?.ToDisplayString() == "System.Collections.IEnumerator",
                _ => false,
            };
        }

        private static bool IsArrayForeachSyntax(SyntaxNode syntax, PurityAnalysisContext context)
        {
            if (!syntax.IsKind(SyntaxKind.IdentifierName) &&
                !syntax.IsKind(SyntaxKind.SimpleMemberAccessExpression) &&
                !syntax.IsKind(SyntaxKind.ElementAccessExpression))
            {
                return false;
            }

            return TryGetForeachCollectionType(syntax.Parent, context.SemanticModel) is IArrayTypeSymbol;
        }

        private static ITypeSymbol? TryGetForeachCollectionType(SyntaxNode? syntaxNode, SemanticModel semanticModel)
        {
            return syntaxNode switch
            {
                Microsoft.CodeAnalysis.CSharp.Syntax.ForEachStatementSyntax forEachStatement =>
                    semanticModel.GetTypeInfo(forEachStatement.Expression).Type,
                Microsoft.CodeAnalysis.CSharp.Syntax.ForEachVariableStatementSyntax forEachVariableStatement =>
                    semanticModel.GetTypeInfo(forEachVariableStatement.Expression).Type,
                _ => null,
            };
        }

        private static PurityAnalysisEngine.PurityAnalysisResult CheckDispatchedInvocationPurity(
            IInvocationOperation invocationOperation,
            PurityAnalysisContext context,
            INamedTypeSymbol? knownReceiverType)
        {
            var invokedMethodSymbol = invocationOperation.TargetMethod;
            if (invokedMethodSymbol == null)
            {
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax);
            }

            var candidateMethods = ResolvePotentialDispatchTargets(
                invokedMethodSymbol,
                context.SemanticModel,
                knownReceiverType,
                invocationOperation.Instance)
                .Where(method => !method.IsAbstract && !method.IsExtern)
                .ToImmutableHashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

            if (CanHaveExternalDispatchTargets(invokedMethodSymbol, invocationOperation, knownReceiverType))
            {
                var hasConcreteImplementationCandidate =
                    invokedMethodSymbol.ContainingType?.TypeKind == TypeKind.Interface &&
                    candidateMethods.Any(method => method.ContainingType?.TypeKind != TypeKind.Interface);

                if (!hasConcreteImplementationCandidate)
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR] Method {invokedMethodSymbol.ContainingType?.Name}.{invokedMethodSymbol.Name} can dispatch to unknown external targets; treating as impure conservatively.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(
                        invocationOperation.Syntax,
                        PurityAnalysisEngine.PurityEvidence.Create(
                            "unknown_external_call",
                            nameof(MethodInvocationPurityRule),
                            invocationOperation,
                            symbol: invokedMethodSymbol));
                }
            }

            if (candidateMethods.Count == 0)
            {
                PurityAnalysisEngine.LogDebug($"  [MIR] No concrete dispatch candidates found for {invokedMethodSymbol.Name}; assuming pure when external dispatch is impossible.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            foreach (var candidateMethod in candidateMethods)
            {
                PurityAnalysisEngine.LogDebug($"  [MIR]   Evaluating dispatch candidate: {candidateMethod.ToDisplayString()}");
                var candidatePurity = PurityAnalysisEngine.GetCalleePurity(candidateMethod, context);
                if (!candidatePurity.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR] --> IMPURE dispatch candidate found: {candidateMethod.ToDisplayString()}");
                    return candidatePurity.WithCallee(candidateMethod, invocationOperation.Syntax);
                }
            }

            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }

        private static bool CanHaveExternalOverrides(IMethodSymbol methodSymbol, INamedTypeSymbol? knownReceiverType)
        {
            if (methodSymbol.IsSealed)
            {
                return false;
            }

            if (!methodSymbol.IsVirtual)
            {
                return false;
            }

            if (methodSymbol.DeclaredAccessibility == Accessibility.Private ||
                methodSymbol.DeclaredAccessibility == Accessibility.Internal ||
                methodSymbol.DeclaredAccessibility == Accessibility.ProtectedAndInternal)
            {
                return false;
            }

            if (methodSymbol.ContainingType == null || methodSymbol.ContainingType.TypeKind != TypeKind.Class)
            {
                return false;
            }

            if (methodSymbol.ContainingType.IsSealed)
            {
                return false;
            }

            if (knownReceiverType != null &&
                knownReceiverType.IsSealed &&
                (SymbolEqualityComparer.Default.Equals(knownReceiverType.OriginalDefinition, methodSymbol.ContainingType.OriginalDefinition) ||
                 DerivesFrom(knownReceiverType, methodSymbol.ContainingType)))
            {
                return false;
            }

            return IsTypeEffectivelyExternallyAccessible(methodSymbol.ContainingType);
        }

        private static bool CanHaveExternalDispatchTargets(
            IMethodSymbol methodSymbol,
            IInvocationOperation invocationOperation,
            INamedTypeSymbol? knownReceiverType)
        {
            if (methodSymbol.ContainingType?.TypeKind == TypeKind.Interface)
            {
                return CanHaveExternalInterfaceImplementations(
                    methodSymbol.ContainingType,
                    invocationOperation.Instance,
                    knownReceiverType);
            }

            return CanHaveExternalOverrides(methodSymbol, knownReceiverType);
        }

        private static bool CanHaveExternalInterfaceImplementations(
            INamedTypeSymbol interfaceSymbol,
            IOperation? invocationInstance,
            INamedTypeSymbol? knownReceiverType)
        {
            if (!CanInterfaceHaveExternalImplementations(interfaceSymbol))
            {
                return false;
            }

            var concreteReceiverType = GetKnownReceiverType(invocationInstance) ?? knownReceiverType;
            if (concreteReceiverType == null)
            {
                return true;
            }

            if (IsAllocationOnlyInterfaceReceiver(invocationInstance))
            {
                return false;
            }

            if (!IsTypeEffectivelyExternallyAccessible(concreteReceiverType))
            {
                return false;
            }

            if (concreteReceiverType.TypeKind == TypeKind.Interface &&
                SymbolEqualityComparer.Default.Equals(
                    concreteReceiverType.OriginalDefinition,
                    interfaceSymbol.OriginalDefinition))
            {
                return true;
            }

            if (concreteReceiverType.TypeKind == TypeKind.Struct)
            {
                return false;
            }

            if (concreteReceiverType.TypeKind == TypeKind.Class && concreteReceiverType.IsSealed)
            {
                return false;
            }

            return true;
        }

        private static bool CanInterfaceHaveExternalImplementations(INamedTypeSymbol interfaceSymbol)
        {
            if (!IsTypeEffectivelyExternallyAccessible(interfaceSymbol))
            {
                return false;
            }

            foreach (var baseInterface in interfaceSymbol.AllInterfaces)
            {
                if (!IsTypeEffectivelyExternallyAccessible(baseInterface))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsDynamicInvocationReceiver(IOperation? operation)
        {
            var current = operation;

            while (current != null)
            {
                current = NormalizeReceiverOperation(current);
                if (current == null)
                {
                    return false;
                }

                if (current.Type?.TypeKind == TypeKind.Dynamic)
                {
                    return true;
                }

                if (current is IConditionalAccessOperation conditionalAccess)
                {
                    current = conditionalAccess.Operation;
                    continue;
                }

                if (TryGetAsConversion(current, out var asOperand, out _))
                {
                    if (asOperand?.Type?.TypeKind == TypeKind.Dynamic)
                    {
                        return true;
                    }

                    current = asOperand;
                    continue;
                }

                if (current is IConversionOperation conversion)
                {
                    current = conversion.Operand;
                    continue;
                }

                if (current is IParenthesizedOperation parenthesized)
                {
                    current = parenthesized.Operand;
                    continue;
                }

                break;
            }

            return false;
        }

        private static INamedTypeSymbol? GetKnownReceiverType(IOperation? invocationInstance)
        {
            var current = invocationInstance;

            while (true)
            {
                current = NormalizeReceiverOperation(current);

                if (current == null)
                {
                    return null;
                }

                if (current is IConditionalAccessOperation conditionalAccess)
                {
                    current = conditionalAccess.Operation;
                    continue;
                }

                if (current is IConditionalOperation conditional)
                {
                    var whenTrueType = GetKnownReceiverType(conditional.WhenTrue);
                    var whenFalseType = GetKnownReceiverType(conditional.WhenFalse);

                    if (whenTrueType != null &&
                        whenFalseType != null &&
                        SymbolEqualityComparer.Default.Equals(whenTrueType, whenFalseType))
                    {
                        return whenTrueType;
                    }

                    return current.Type as INamedTypeSymbol;
                }

                if (TryGetAsConversion(current, out var asOperand, out var asTargetType))
                {
                    if (asTargetType != null)
                    {
                        var operandType = asOperand?.Type as INamedTypeSymbol;
                        if (operandType != null &&
                            ImplementsInterface(operandType, asTargetType))
                        {
                            current = asOperand;
                            continue;
                        }

                        if (asOperand?.Type is ITypeParameterSymbol typeParameter)
                        {
                            var constrainedType = ResolveConstrainedSealedType(typeParameter);
                            if (constrainedType != null &&
                                ImplementsInterface(constrainedType, asTargetType))
                            {
                                current = asOperand;
                                continue;
                            }
                        }
                    }

                    return asTargetType;
                }

                if (current is IConversionOperation conversion)
                {
                    current = conversion.Operand;
                    continue;
                }

                if (current is IParenthesizedOperation parenthesized)
                {
                    current = parenthesized.Operand;
                    continue;
                }

                if (current.Type is ITypeParameterSymbol typeParameterSymbol)
                {
                    var constrainedSealedType = ResolveConstrainedSealedType(typeParameterSymbol);
                    if (constrainedSealedType != null)
                    {
                        return constrainedSealedType;
                    }

                    return null;
                }

                break;
            }

            return current?.Type as INamedTypeSymbol;
        }

        private static INamedTypeSymbol? GetKnownStaticInterfaceReceiverType(IMethodSymbol invokedMethodSymbol)
        {
            if (!invokedMethodSymbol.IsStatic ||
                invokedMethodSymbol.ContainingType?.TypeKind != TypeKind.Interface ||
                invokedMethodSymbol.ContainingType is not INamedTypeSymbol interfaceType ||
                interfaceType.TypeArguments.IsEmpty)
            {
                return null;
            }

            var interfaceArg = interfaceType.TypeArguments[0];

            if (interfaceArg is INamedTypeSymbol namedType)
            {
                return namedType.TypeKind is TypeKind.Class or TypeKind.Struct
                    ? namedType
                    : null;
            }

            if (interfaceArg is ITypeParameterSymbol typeParameter)
            {
                return ResolveConstrainedSealedType(typeParameter);
            }

            return null;
        }

        private static INamedTypeSymbol? ResolveConstrainedSealedType(ITypeParameterSymbol typeParameter)
        {
            return ResolveConstrainedSealedType(typeParameter, new HashSet<ITypeParameterSymbol>(SymbolEqualityComparer.Default));
        }

        private static INamedTypeSymbol? ResolveConstrainedSealedType(
            ITypeParameterSymbol typeParameter,
            HashSet<ITypeParameterSymbol> visitedTypeParameters)
        {
            if (!visitedTypeParameters.Add(typeParameter))
            {
                return null;
            }

            INamedTypeSymbol? constrainedType = null;

            foreach (var constraintType in typeParameter.ConstraintTypes)
            {
                INamedTypeSymbol? resolvedConstraintType = null;

                if (constraintType is ITypeParameterSymbol nestedTypeParameter)
                {
                    resolvedConstraintType = ResolveConstrainedSealedType(nestedTypeParameter, visitedTypeParameters);
                }
                else if (constraintType is INamedTypeSymbol namedType)
                {
                    if (namedType.TypeKind == TypeKind.Interface)
                    {
                        continue;
                    }

                    if (namedType.TypeKind != TypeKind.Class &&
                        constraintType.TypeKind != TypeKind.Struct ||
                        !namedType.IsSealed)
                    {
                        return null;
                    }

                    resolvedConstraintType = namedType;
                }

                if (resolvedConstraintType == null)
                {
                    continue;
                }

                if (constrainedType != null &&
                    !SymbolEqualityComparer.Default.Equals(constrainedType, resolvedConstraintType))
                {
                    return null;
                }

                constrainedType = resolvedConstraintType;
            }

            return constrainedType;
        }

        private static bool IsTypeEffectivelyExternallyAccessible(INamedTypeSymbol typeSymbol)
        {
            for (var current = typeSymbol; current != null; current = current.ContainingType)
            {
                if (current.DeclaredAccessibility == Accessibility.Private ||
                    current.DeclaredAccessibility == Accessibility.Internal)
                {
                    return false;
                }
            }

            return true;
        }

        private static IEnumerable<IMethodSymbol> ResolvePotentialDispatchTargets(
            IMethodSymbol invokedMethodSymbol,
            SemanticModel semanticModel,
            INamedTypeSymbol? knownReceiverType,
            IOperation? invocationInstance)
        {
            var compilation = semanticModel.Compilation;
            var target = invokedMethodSymbol.OriginalDefinition;
            var targets = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

            if (target.ContainingType?.TypeKind == TypeKind.Interface)
            {
                if (knownReceiverType != null && ImplementsInterface(knownReceiverType, target.ContainingType))
                {
                    if (IsAllocationOnlyInterfaceReceiver(invocationInstance))
                    {
                        var implementation = knownReceiverType.FindImplementationForInterfaceMember(target) as IMethodSymbol;
                        if (implementation != null)
                        {
                            targets.Add(implementation.OriginalDefinition);
                        }
                        else if (!target.IsAbstract || HasMethodBody(target))
                        {
                            targets.Add(target.OriginalDefinition);
                        }

                        return targets;
                    }

                    if (knownReceiverType.TypeKind == TypeKind.Struct ||
                        (knownReceiverType.TypeKind == TypeKind.Class && knownReceiverType.IsSealed))
                    {
                        var implementation = knownReceiverType.FindImplementationForInterfaceMember(target) as IMethodSymbol;
                        if (implementation != null)
                        {
                            targets.Add(implementation.OriginalDefinition);
                        }
                        else if (!target.IsAbstract || HasMethodBody(target))
                        {
                            targets.Add(target.OriginalDefinition);
                        }

                        return targets;
                    }
                    var requiresInterfaceReceiverConstraint = knownReceiverType.TypeKind == TypeKind.Interface;

                    foreach (var type in EnumerateAllNamedTypes(compilation.Assembly.GlobalNamespace))
                    {
                        if (requiresInterfaceReceiverConstraint)
                        {
                            if (!ImplementsInterface(type, knownReceiverType))
                            {
                                continue;
                            }
                        }
                        else
                        {
                            if (!SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, knownReceiverType.OriginalDefinition) &&
                                !DerivesFrom(type, knownReceiverType))
                            {
                                continue;
                            }
                        }

                        if (!ImplementsInterface(type, target.ContainingType))
                        {
                            continue;
                        }

                        if (type.Kind == SymbolKind.NamedType &&
                            (type.TypeKind == TypeKind.Interface ||
                             type.TypeKind == TypeKind.Struct ||
                             type.TypeKind == TypeKind.Class))
                        {
                            var implementation = type.FindImplementationForInterfaceMember(target) as IMethodSymbol;
                            if (implementation != null)
                            {
                                targets.Add(implementation.OriginalDefinition);
                            }
                        }
                    }

                    if (!target.IsAbstract || HasMethodBody(target))
                    {
                        targets.Add(target);
                    }

                    return targets;
                }

                foreach (var type in EnumerateAllNamedTypes(compilation.Assembly.GlobalNamespace))
                {
                    if (!ImplementsInterface(type, target.ContainingType))
                    {
                        continue;
                    }

                    if (type.Kind == SymbolKind.NamedType && (type.TypeKind == TypeKind.Interface || type.TypeKind == TypeKind.Struct || type.TypeKind == TypeKind.Class))
                    {
                        var implementation = type.FindImplementationForInterfaceMember(target) as IMethodSymbol;
                        if (implementation != null)
                        {
                            targets.Add(implementation.OriginalDefinition);
                        }
                    }
                }

                if (!target.IsAbstract || HasMethodBody(target))
                {
                    targets.Add(target);
                }

                return targets;
            }

            if (target.IsVirtual || target.IsAbstract || target.IsOverride)
            {
                var baseType = target.ContainingType;
                if (baseType != null)
                {
                    if (knownReceiverType != null &&
                        knownReceiverType.IsSealed &&
                        (SymbolEqualityComparer.Default.Equals(knownReceiverType.OriginalDefinition, baseType.OriginalDefinition) ||
                         DerivesFrom(knownReceiverType, baseType)))
                    {
                        var sealedReceiverTarget = ResolveDispatchTargetForSealedReceiver(target, knownReceiverType);
                        if (sealedReceiverTarget != null)
                        {
                            targets.Add(sealedReceiverTarget.OriginalDefinition);
                        }

                        return targets;
                    }

                    foreach (var type in EnumerateAllNamedTypes(compilation.Assembly.GlobalNamespace))
                    {
                        if (!DerivesFrom(type, baseType))
                        {
                            continue;
                        }

                        foreach (var member in type.GetMembers())
                        {
                            if (member is IMethodSymbol method &&
                                OverridesTargetMethod(method, target))
                            {
                                targets.Add(method.OriginalDefinition);
                            }
                        }
                    }
                }

                if (!target.IsAbstract)
                {
                    targets.Add(target);
                }

                return targets;
            }

            targets.Add(target);
            return targets;
        }

        private static IMethodSymbol? ResolveDispatchTargetForSealedReceiver(IMethodSymbol targetMethod, INamedTypeSymbol sealedReceiverType)
        {
            for (var type = sealedReceiverType; type != null; type = type.BaseType)
            {
                foreach (var member in type.GetMembers())
                {
                    if (member is IMethodSymbol method &&
                        (SymbolEqualityComparer.Default.Equals(method.OriginalDefinition, targetMethod.OriginalDefinition) ||
                         OverridesTargetMethod(method, targetMethod) ||
                         ExplicitlyImplements(method, targetMethod)))
                    {
                        return method;
                    }
                }
            }

            if (!targetMethod.IsAbstract)
            {
                return targetMethod;
            }

            return null;
        }

        private static bool ExplicitlyImplements(IMethodSymbol methodSymbol, IMethodSymbol interfaceMethod)
        {
            foreach (var implemented in methodSymbol.ExplicitInterfaceImplementations)
            {
                if (SymbolEqualityComparer.Default.Equals(implemented.OriginalDefinition, interfaceMethod.OriginalDefinition))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ImplementsInterface(INamedTypeSymbol type, INamedTypeSymbol interfaceSymbol)
        {
            if (interfaceSymbol == null)
            {
                return false;
            }

            return type.AllInterfaces.Any(
                i => SymbolEqualityComparer.Default.Equals(
                    i.OriginalDefinition,
                    interfaceSymbol.OriginalDefinition));
        }

        private static bool IsAllocationOnlyInterfaceReceiver(IOperation? invocationInstance)
        {
            var current = invocationInstance;

            while (current != null)
            {
                current = NormalizeReceiverOperation(current);

                if (current is IConditionalAccessOperation conditionalAccess)
                {
                    current = conditionalAccess.Operation;
                    continue;
                }

                if (current is IConversionOperation conversion)
                {
                    current = conversion.Operand;
                    continue;
                }

                if (current is IParenthesizedOperation parenthesized)
                {
                    current = parenthesized.Operand;
                    continue;
                }

                if (TryGetAsConversion(current, out var asOperand, out _))
                {
                    current = asOperand;
                    continue;
                }

                return current is IObjectCreationOperation;
            }

            return false;
        }

        private static IOperation? NormalizeReceiverOperation(IOperation? operation)
        {
            if (operation is not IConditionalAccessInstanceOperation)
            {
                return operation;
            }

            for (var current = operation.Parent; current != null; current = current.Parent)
            {
                if (current is IConditionalAccessOperation conditionalAccess)
                {
                    return conditionalAccess.Operation;
                }
            }

            return operation;
        }

        private static bool HasMethodBody(IMethodSymbol methodSymbol)
        {
            if (methodSymbol.DeclaringSyntaxReferences.Length == 0)
            {
                return false;
            }

            foreach (var syntaxReference in methodSymbol.DeclaringSyntaxReferences)
            {
                var methodSyntax = syntaxReference.GetSyntax();
                if (methodSyntax is Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax methodDeclaration &&
                    (methodDeclaration.Body != null || methodDeclaration.ExpressionBody != null))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsBaseReference(IOperation? operation)
        {
            return operation is IInstanceReferenceOperation instanceReference &&
                instanceReference.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance &&
                operation.Syntax.IsKind(SyntaxKind.BaseExpression);
        }

        private static bool TryGetAsConversion(
            IOperation? operation,
            out IOperation? operand,
            out INamedTypeSymbol? targetType)
        {
            if (operation is IConversionOperation conversion &&
                conversion.Syntax.IsKind(SyntaxKind.AsExpression))
            {
                operand = conversion.Operand;
                targetType = conversion.Type as INamedTypeSymbol;
                return true;
            }

            operand = null;
            targetType = null;
            return false;
        }

        private static string GetCatalogHitCategory(ISymbol symbol)
        {
            var containingType = symbol.ContainingType?.ToDisplayString() ?? string.Empty;
            var containingNamespace = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;

            if (containingType == "System.Threading.Interlocked" ||
                containingType == "System.Threading.Monitor" ||
                containingType == "System.Threading.Mutex" ||
                containingType == "System.Threading.Semaphore" ||
                containingType == "System.Threading.SemaphoreSlim" ||
                containingType == "System.Collections.Immutable.ImmutableInterlocked")
            {
                return "synchronization";
            }

            if (containingNamespace.StartsWith("System.Reflection", StringComparison.Ordinal) ||
                containingType.StartsWith("System.Reflection.", StringComparison.Ordinal) ||
                containingType == "System.Type" ||
                containingType == "System.Runtime.Loader.AssemblyLoadContext" ||
                containingType == "System.Environment" ||
                containingType == "System.DateTime" ||
                containingType == "System.DateTimeOffset" ||
                containingType == "System.TimeProvider" ||
                containingType == "System.TimeZoneInfo" ||
                containingType == "System.Diagnostics.Stopwatch")
            {
                return "reflection_environment_source";
            }

            return "catalog_hit";
        }

        private static PurityAnalysisEngine.PurityAnalysisResult CheckLinqSourceEnumeratorPurity(
            IOperation sourceOperation,
            PurityAnalysisContext context)
        {
            var unwrappedSource = PurityAnalysisEngine.SkipImplicitConversions(sourceOperation) ?? sourceOperation;
            if (unwrappedSource.Type == null)
            {
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            foreach (var getEnumerator in EnumerateSourceGetEnumeratorImplementations(unwrappedSource.Type))
            {
                var enumeratorPurity = PurityAnalysisEngine.GetCalleePurity(getEnumerator.OriginalDefinition, context);
                if (!enumeratorPurity.IsPure)
                {
                    return enumeratorPurity.WithCallee(getEnumerator, unwrappedSource.Syntax);
                }
            }

            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }

        private static IEnumerable<IMethodSymbol> EnumerateSourceGetEnumeratorImplementations(ITypeSymbol sourceType)
        {
            var seen = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

            foreach (var getEnumerator in sourceType
                         .GetMembers("GetEnumerator")
                         .OfType<IMethodSymbol>()
                         .Where(method => method.Parameters.Length == 0 && method.DeclaringSyntaxReferences.Length > 0))
            {
                if (seen.Add(getEnumerator.OriginalDefinition))
                {
                    yield return getEnumerator;
                }
            }

            if (sourceType is not INamedTypeSymbol namedSourceType)
            {
                yield break;
            }

            foreach (var interfaceType in namedSourceType.AllInterfaces)
            {
                if (!IsEnumerableInterface(interfaceType))
                {
                    continue;
                }

                foreach (var interfaceGetEnumerator in interfaceType
                             .GetMembers("GetEnumerator")
                             .OfType<IMethodSymbol>()
                             .Where(method => method.Parameters.Length == 0))
                {
                    var implementation = namedSourceType.FindImplementationForInterfaceMember(interfaceGetEnumerator) as IMethodSymbol;
                    if (implementation == null || implementation.DeclaringSyntaxReferences.Length == 0)
                    {
                        continue;
                    }

                    if (seen.Add(implementation.OriginalDefinition))
                    {
                        yield return implementation;
                    }
                }
            }
        }

        private static bool IsEnumerableInterface(INamedTypeSymbol typeSymbol)
        {
            var originalDefinition = typeSymbol.OriginalDefinition;
            return originalDefinition.SpecialType == SpecialType.System_Collections_IEnumerable ||
                originalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T;
        }

        private static IEnumerable<INamedTypeSymbol> EnumerateAllNamedTypes(INamespaceSymbol root)
        {
            foreach (var member in root.GetMembers())
            {
                if (member is INamespaceSymbol ns)
                {
                    foreach (var inner in EnumerateAllNamedTypes(ns))
                    {
                        yield return inner;
                    }
                }
                else if (member is INamedTypeSymbol type)
                {
                    yield return type;
                    foreach (var nested in EnumerateNestedTypes(type))
                    {
                        yield return nested;
                    }
                }
            }
        }

        private static IEnumerable<INamedTypeSymbol> EnumerateNestedTypes(INamedTypeSymbol type)
        {
            foreach (var member in type.GetTypeMembers())
            {
                yield return member;
                foreach (var nested in EnumerateNestedTypes(member))
                {
                    yield return nested;
                }
            }
        }

        private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol potentialBase)
        {
            for (var t = type.BaseType; t != null; t = t.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, potentialBase.OriginalDefinition))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool OverridesTargetMethod(IMethodSymbol method, IMethodSymbol target)
        {
            var current = method.OverriddenMethod;
            while (current != null)
            {
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, target.OriginalDefinition))
                {
                    return true;
                }

                current = current.OverriddenMethod;
            }

            return false;
        }

    }
}
