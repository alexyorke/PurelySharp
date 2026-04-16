using Microsoft.CodeAnalysis;
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
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax);
            }


            if (invokedMethodSymbol.Name == "Invoke" && invokedMethodSymbol.ContainingType?.TypeKind == TypeKind.Delegate)
            {

                PurityAnalysisEngine.LogDebug("  [MIR-DEL-S] === Simplified Delegate Invocation Check Start ===");
                PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Invoked Symbol: {invokedMethodSymbol.ContainingType.Name}.Invoke()");

                if (invocationOperation.Instance == null)
                {
                    PurityAnalysisEngine.LogDebug("  [MIR-DEL-S] Instance is NULL (static delegate?). Assuming impure.");
                    return PurityAnalysisEngine.ImpureResult(invocationOperation.Syntax);
                }

                PurityAnalysisEngine.PurityAnalysisResult result = PurityAnalysisEngine.PurityAnalysisResult.Pure;
                IOperation delegateInstanceOp = invocationOperation.Instance;
                PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Analyzing Delegate Instance Op: {delegateInstanceOp.Kind} | Syntax: {delegateInstanceOp.Syntax}");

                var potentialTargets = PurityAnalysisEngine.ResolvePotentialTargets(delegateInstanceOp, currentState, context.SemanticModel);
                if (potentialTargets != null)
                {
                    PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Resolved {potentialTargets.Value.MethodSymbols.Count} target(s) for delegate invocation.");
                    if (potentialTargets.Value.MethodSymbols.IsEmpty)
                    {
                        PurityAnalysisEngine.LogDebug("  [MIR-DEL-S] --> Resolved target set is empty. Assuming PURE.");
                        result = PurityAnalysisEngine.PurityAnalysisResult.Pure;
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
                    result = PurityAnalysisEngine.ImpureResult(delegateInstanceOp.Syntax);
                }

                PurityAnalysisEngine.LogDebug($"  [MIR-DEL-S] Final Result for Delegate Invocation: IsPure={result.IsPure}");
                PurityAnalysisEngine.LogDebug("  [MIR-DEL-S] === Simplified Delegate Invocation Check End ===");
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


                PurityAnalysisEngine.LogDebug("  [MIR]   LINQ source was pure. Checking delegate arguments...");
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
                    PurityAnalysisEngine.LogDebug("  [MIR] LINQ source and all relevant delegate arguments determined to be pure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                }
                else
                {
                    PurityAnalysisEngine.LogDebug("  [MIR] --> IMPURE (LINQ method, impure delegate argument detected)");

                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(firstImpureDelegateResult.ImpureSyntaxNode ?? invocationOperation.Syntax);
                }
            }


            if (IsPotentiallyDispatchedMethod(invokedMethodSymbol)
                && (invokedMethodSymbol.IsStatic
                    ? invocationOperation.Instance == null
                    : invocationOperation.Instance != null
                        && invocationOperation.Instance.Kind != OperationKind.BaseReference))
            {
                PurityAnalysisEngine.LogDebug($"  [MIR] Checking potential dispatch candidates for {invokedMethodSymbol.Name}.");
                var dispatchResult = CheckDispatchedInvocationPurity(invocationOperation, context);
                if (!dispatchResult.IsPure)
                {
                    return dispatchResult;
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


            if (invocationOperation.Instance != null
                && invocationOperation.Instance.Kind != OperationKind.BaseReference)
            {
                PurityAnalysisEngine.LogDebug($"  [MIR] Checking instance purity for {invocationOperation.Instance.Kind}: {invocationOperation.Instance.Syntax.ToString().Trim()}");
                var instanceResult = PurityAnalysisEngine.CheckSingleOperation(invocationOperation.Instance, context, currentState);
                PurityAnalysisEngine.LogDebug($"  [MIR] Instance check result: IsPure={instanceResult.IsPure}, Node Type={instanceResult.ImpureSyntaxNode?.GetType().Name ?? "NULL"}");
                if (!instanceResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug("  [MIR] --> IMPURE (Instance is impure)");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(instanceResult.ImpureSyntaxNode ?? invocationOperation.Instance.Syntax);
                }
            }


            var originalDefinitionSymbol = invokedMethodSymbol.OriginalDefinition;

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

                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(argumentResult.ImpureSyntaxNode ?? argument.Value.Syntax);
                }
            }



            string methodDisplayString = originalDefinitionSymbol.ToDisplayString();
            PurityAnalysisEngine.LogDebug($"  [MIR] Analyzing regular call to: {methodDisplayString} | Syntax: {invocationOperation.Syntax}");



            PurityAnalysisEngine.LogDebug($"  [MIR] Checking IsKnownImpure with signature: '{originalDefinitionSymbol.ToDisplayString()}'");
            if (PurityAnalysisEngine.IsKnownImpure(originalDefinitionSymbol))
            {
                PurityAnalysisEngine.LogDebug("  [MIR] --> IMPURE (Known Impure)");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax);
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
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax);
            }


            PurityAnalysisEngine.LogDebug($"  [MIR] Performing purity check for: {methodDisplayString}");

            var calleePurity = PurityAnalysisEngine.GetCalleePurity(originalDefinitionSymbol, context);

            PurityAnalysisEngine.LogDebug($"  [MIR] Callee purity result for {methodDisplayString}: IsPure={calleePurity.IsPure}");

            return calleePurity.IsPure
                ? PurityAnalysisEngine.PurityAnalysisResult.Pure
                : PurityAnalysisEngine.PurityAnalysisResult.Impure(calleePurity.ImpureSyntaxNode ?? invocationOperation.Syntax);
        }

        private static bool IsPotentiallyDispatchedMethod(IMethodSymbol methodSymbol)
        {
            return methodSymbol.ContainingType?.TypeKind == TypeKind.Interface
                || methodSymbol.IsVirtual
                || methodSymbol.IsAbstract
                || methodSymbol.IsOverride;
        }

        private static PurityAnalysisEngine.PurityAnalysisResult CheckDispatchedInvocationPurity(
            IInvocationOperation invocationOperation,
            PurityAnalysisContext context)
        {
            var invokedMethodSymbol = invocationOperation.TargetMethod;
            if (invokedMethodSymbol == null)
            {
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax);
            }

            if (CanHaveExternalDispatchTargets(invokedMethodSymbol, invocationOperation))
            {
                PurityAnalysisEngine.LogDebug($"  [MIR] Method {invokedMethodSymbol.ContainingType?.Name}.{invokedMethodSymbol.Name} can be overridden in external assemblies; treating as impure conservatively.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(invocationOperation.Syntax);
            }

            var candidateMethods = ResolvePotentialDispatchTargets(invokedMethodSymbol, context.SemanticModel)
                .Where(method => method != null && !method.IsAbstract && !method.IsExtern)
                .ToImmutableHashSet(SymbolEqualityComparer.Default);

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
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(candidatePurity.ImpureSyntaxNode ?? invocationOperation.Syntax);
                }
            }

            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }

        private static bool CanHaveExternalOverrides(IMethodSymbol methodSymbol)
        {
            if (!methodSymbol.IsVirtual || methodSymbol.IsSealed)
            {
                return false;
            }

            if (methodSymbol.DeclaredAccessibility == Accessibility.Private ||
                methodSymbol.DeclaredAccessibility == Accessibility.PrivateProtected ||
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

            return IsTypeEffectivelyExternallyAccessible(methodSymbol.ContainingType);
        }

        private static bool CanHaveExternalDispatchTargets(IMethodSymbol methodSymbol, IInvocationOperation invocationOperation)
        {
            if (methodSymbol.ContainingType?.TypeKind == TypeKind.Interface)
            {
                return CanHaveExternalInterfaceImplementations(methodSymbol.ContainingType, invocationOperation.Instance);
            }

            return CanHaveExternalOverrides(methodSymbol);
        }

        private static bool CanHaveExternalInterfaceImplementations(
            INamedTypeSymbol interfaceSymbol,
            IOperation? invocationInstance)
        {
            if (!IsTypeEffectivelyExternallyAccessible(interfaceSymbol))
            {
                return false;
            }

            var concreteReceiverType = GetKnownReceiverType(invocationInstance);
            if (concreteReceiverType == null)
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

        private static INamedTypeSymbol? GetKnownReceiverType(IOperation? invocationInstance)
        {
            var current = invocationInstance;

            while (true)
            {
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

                if (current is IAsOperation asOperation)
                {
                    var operandType = asOperation.Operand?.Type as INamedTypeSymbol;
                    var targetInterfaceType = asOperation.Type as INamedTypeSymbol;
                    if (operandType != null &&
                        targetInterfaceType != null &&
                        operandType.TypeKind != TypeKind.Interface &&
                        ImplementsInterface(operandType, targetInterfaceType))
                    {
                        current = asOperation.Operand;
                        continue;
                    }

                    return asOperation.Type as INamedTypeSymbol;
                }

                break;
            }

            return current?.Type as INamedTypeSymbol;
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
            SemanticModel semanticModel)
        {
            var compilation = semanticModel.Compilation;
            var target = invokedMethodSymbol.OriginalDefinition;
            var targets = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);

            if (target.ContainingType?.TypeKind == TypeKind.Interface)
            {
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
