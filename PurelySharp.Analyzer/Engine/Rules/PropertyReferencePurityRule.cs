using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Analyzer.Engine.Rules
{

    internal class PropertyReferencePurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.PropertyReference);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IPropertyReferenceOperation propertyReferenceOperation))
            {
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            IPropertySymbol propertySymbol = propertyReferenceOperation.Property;
            PurityAnalysisEngine.LogDebug($"  [PropRefRule] Checking PropertyReference: {propertySymbol.Name} on Type: {propertySymbol.ContainingType?.ToDisplayString()}");

            if (IsCompilerGeneratedArrayForeachCurrent(propertyReferenceOperation, context))
            {
                PurityAnalysisEngine.LogDebug("    [PropRefRule] Compiler-generated array foreach Current is treated as pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }


            if (IsPartOfAssignmentTarget(propertyReferenceOperation))
            {
                PurityAnalysisEngine.LogDebug($"    [PropRefRule] Skipping property read {propertySymbol.Name} as it's an assignment target.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            if (PurityAnalysisEngine.IsPureEnforced(
                propertySymbol,
                context.EnforcePureAttributeSymbol,
                context.PureAttributeSymbol))
            {
                PurityAnalysisEngine.LogDebug($"    [PropRefRule] Property {propertySymbol.Name} has [EnforcePure]. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }



            string impureSig = propertySymbol.OriginalDefinition.ToDisplayString();
            PurityAnalysisEngine.LogDebug($"      [PropRefRule] Checking IsKnownImpure for property: '{impureSig}'");
            if (PurityAnalysisEngine.IsKnownImpure(propertySymbol))
            {
                PurityAnalysisEngine.LogDebug($"    [PropRefRule] Property {propertySymbol.Name} is known impure. Impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(
                    propertyReferenceOperation.Syntax,
                    PurityAnalysisEngine.PurityEvidence.Create(
                        GetCatalogHitCategory(propertySymbol),
                        ruleName: nameof(PropertyReferencePurityRule),
                        operation: propertyReferenceOperation,
                        syntaxNode: propertyReferenceOperation.Syntax,
                        symbol: propertySymbol,
                        catalogSource: "known_impure_member"));
            }

            if (propertySymbol.GetMethod != null &&
                IsPotentiallyDispatchedGetter(propertySymbol.GetMethod) &&
                !PurityAnalysisEngine.IsKnownPureBCLMember(propertySymbol))
            {
                PurityAnalysisEngine.LogDebug($"    [PropRefRule] Property {propertySymbol.Name} may dispatch. Checking getter candidates.");
                var dispatchResult = CheckDispatchedGetterPurity(propertyReferenceOperation, context);
                if (!dispatchResult.IsPure)
                {
                    return dispatchResult;
                }
            }


            if (propertySymbol.IsStatic)
            {

                PurityAnalysisEngine.LogDebug($"    [PropRefRule] Static property access: {propertySymbol.Name}");

                var cctorResult = PurityAnalysisEngine.CheckStaticConstructorPurity(propertySymbol.ContainingType, context, currentState);
                if (!cctorResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [PropRefRule] Static property '{propertySymbol.Name}' access IMPURE due to impure static constructor in {propertySymbol.ContainingType?.Name}.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(
                        cctorResult.ImpureSyntaxNode ?? propertyReferenceOperation.Syntax,
                        cctorResult.Evidence);
                }


                string staticPureSig = propertySymbol.OriginalDefinition.ToDisplayString();
                bool staticKnownPure = PurityAnalysisEngine.IsKnownPureBCLMember(propertySymbol);
                PurityAnalysisEngine.LogDebug($"      [PropRefRule] Checking IsKnownPureBCLMember for static property: '{staticPureSig}' -> {staticKnownPure}");

                if (staticKnownPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [PropRefRule] Static property '{propertySymbol.Name}' is a known pure BCL member. Read is Pure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                }

                if (propertySymbol.GetMethod != null)
                {
                    PurityAnalysisEngine.LogDebug($"    [PropRefRule] Static property '{propertySymbol.Name}' has a getter. Checking getter purity via service/recursion.");
                    var staticGetterResult = PurityAnalysisEngine.GetCalleePurity(propertySymbol.GetMethod, context);
                    PurityAnalysisEngine.LogDebug($"    [PropRefRule] Getter purity result for static property '{propertySymbol.Name}': IsPure={staticGetterResult.IsPure}");
                    return GetterResultOrPure(staticGetterResult, propertySymbol.GetMethod, propertyReferenceOperation);
                }

                PurityAnalysisEngine.LogDebug($"    [PropRefRule] Static property '{propertySymbol.Name}' has no accessible getter to analyze and is not a known pure BCL member. Read is Impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(propertyReferenceOperation.Syntax);
            }
            else
            {
                PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance property access: {propertySymbol.Name}");
                IOperation? instanceOperation = propertyReferenceOperation.Instance;


                string instanceKind = instanceOperation?.Kind.ToString() ?? "null";
                string instanceSyntax = instanceOperation?.Syntax.ToString() ?? "null";
                PurityAnalysisEngine.LogDebug($"      [PropRefRule] Instance Operation Kind: {instanceKind}, Syntax: {instanceSyntax}");

                if (instanceOperation == null)
                {

                    PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance operation is null for property '{propertySymbol.Name}'. Assuming Impure for safety.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(propertyReferenceOperation.Syntax);
                }


                if (instanceOperation is IParameterReferenceOperation paramRef &&
                    (paramRef.Parameter.RefKind == RefKind.In ||
                     paramRef.Parameter.RefKind == RefKind.RefReadOnly ||
                     paramRef.Parameter.RefKind == (RefKind)4))
                {
                    bool isValueStruct = paramRef.Parameter.Type.IsValueType && !paramRef.Parameter.Type.IsReferenceType;
                    PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is ParameterReference '{paramRef.Parameter.Name}', RefKind={paramRef.Parameter.RefKind}, IsValueStruct={isValueStruct}");


                    if (propertySymbol.GetMethod != null)
                    {
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance '{paramRef.Parameter.Name}' is value struct or readonly ref. Checking getter purity via service/recursion.");
                        var parameterGetterResult = PurityAnalysisEngine.GetCalleePurity(propertySymbol.GetMethod, context);
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Getter purity result for '{propertySymbol.Name}' on parameter '{paramRef.Parameter.Name}': IsPure={parameterGetterResult.IsPure}");
                        return GetterResultOrPure(parameterGetterResult, propertySymbol.GetMethod, propertyReferenceOperation);
                    }


                    PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance '{paramRef.Parameter.Name}' has no accessible getter to analyze. Read is Impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(propertyReferenceOperation.Syntax);
                }
                else if (instanceOperation is IInstanceReferenceOperation instanceRef && instanceRef.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance)
                {

                    bool isReadonlyStruct = context.ContainingMethodSymbol?.ContainingType is { IsReadOnly: true, IsValueType: true };

                    if (isReadonlyStruct)
                    {

                        if (propertySymbol.GetMethod != null)
                        {
                            PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is 'this' within a readonly struct. Checking getter purity via service/recursion.");
                            var readonlyStructGetterResult = PurityAnalysisEngine.GetCalleePurity(propertySymbol.GetMethod, context);
                            PurityAnalysisEngine.LogDebug($"    [PropRefRule] Getter purity result for readonly struct property '{propertySymbol.Name}': IsPure={readonlyStructGetterResult.IsPure}");
                            return GetterResultOrPure(readonlyStructGetterResult, propertySymbol.GetMethod, propertyReferenceOperation);
                        }


                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is 'this' within a readonly struct, but property '{propertySymbol.Name}' has no accessible getter to analyze. Read is Impure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(propertyReferenceOperation.Syntax);
                    }
                    else if (propertySymbol.IsReadOnly)
                    {
                        if (propertySymbol.GetMethod != null)
                        {
                            PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is 'this', property '{propertySymbol.Name}' is readonly (get/init-only). Checking getter purity via service/recursion.");
                            var readonlyGetterResult = PurityAnalysisEngine.GetCalleePurity(propertySymbol.GetMethod, context);
                            PurityAnalysisEngine.LogDebug($"    [PropRefRule] Getter purity result for readonly property '{propertySymbol.Name}': IsPure={readonlyGetterResult.IsPure}");
                            return GetterResultOrPure(readonlyGetterResult, propertySymbol.GetMethod, propertyReferenceOperation);
                        }

                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is 'this', property '{propertySymbol.Name}' has no accessible getter to analyze. Read is Impure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(propertyReferenceOperation.Syntax);
                    }
                    else if (propertySymbol.GetMethod != null)
                    {
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is 'this', property '{propertySymbol.Name}' has a getter. Checking getter purity via service/recursion.");
                        var thisGetterResult = PurityAnalysisEngine.GetCalleePurity(propertySymbol.GetMethod, context);
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Getter purity result for '{propertySymbol.Name}': IsPure={thisGetterResult.IsPure}");

                        return GetterResultOrPure(thisGetterResult, propertySymbol.GetMethod, propertyReferenceOperation);
                    }
                    else
                    {
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is 'this', property '{propertySymbol.Name}' is not readonly and has no accessible getter to analyze. Read is Impure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(propertyReferenceOperation.Syntax);
                    }
                }
                else
                {
                    var instanceExprResult = PurityAnalysisEngine.CheckSingleOperation(instanceOperation, context, currentState);
                    if (!instanceExprResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance expression for '{propertySymbol.Name}' is impure. Propagating.");
                        return instanceExprResult;
                    }

                    string instancePureSig = propertySymbol.OriginalDefinition.ToDisplayString();
                    bool instanceKnownPure = PurityAnalysisEngine.IsKnownPureBCLMember(propertySymbol);
                    PurityAnalysisEngine.LogDebug($"      [PropRefRule] Checking IsKnownPureBCLMember for instance property: '{instancePureSig}' -> {instanceKnownPure}");

                    if (instanceKnownPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance property '{propertySymbol.Name}' is known pure BCL. Read is Pure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }

                    else if (propertySymbol.GetMethod != null && context.PureAttributeSymbol != null &&
                             PurityAnalysisEngine.HasAttribute(propertySymbol.GetMethod, context.PureAttributeSymbol))
                    {
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Property '{propertySymbol.Name}' getter has [Pure] attribute. Checking getter purity via service/recursion.");
                        var attributedGetterResult = PurityAnalysisEngine.GetCalleePurity(propertySymbol.GetMethod, context);
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Getter purity result for [Pure] property '{propertySymbol.Name}': IsPure={attributedGetterResult.IsPure}");
                        return GetterResultOrPure(attributedGetterResult, propertySymbol.GetMethod, propertyReferenceOperation);
                    }

                    else if (propertySymbol.GetMethod != null)
                    {
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is complex ({instanceKind}), property '{propertySymbol.Name}' has getter. Checking getter purity via service/recursion.");
                        var complexGetterResult = PurityAnalysisEngine.GetCalleePurity(propertySymbol.GetMethod, context);
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Getter purity result for complex instance access to '{propertySymbol.Name}': IsPure={complexGetterResult.IsPure}");
                        return GetterResultOrPure(complexGetterResult, propertySymbol.GetMethod, propertyReferenceOperation);
                    }

                    else
                    {

                        if (propertySymbol.GetMethod != null &&
                            context.PurityCache.TryGetValue(propertySymbol.GetMethod.OriginalDefinition, out var cachedGetterResult) &&
                            !cachedGetterResult.IsPure)
                        {
                            PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is complex, property {propertySymbol.Name} known pure BCL, but getter is known impure from cache. Returning Impure.");
                            return PurityAnalysisEngine.PurityAnalysisResult.Impure(cachedGetterResult.ImpureSyntaxNode ?? propertyReferenceOperation.Syntax);
                        }


                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance property '{propertySymbol.Name}' is known pure BCL. Read is Pure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }
                }
            }


        }

        private static bool IsPartOfAssignmentTarget(IOperation operation)
        {


            if (operation.Parent is IAssignmentOperation assignment && assignment.Target == operation)
            {
                return true;
            }
            if (operation.Parent is ICompoundAssignmentOperation compoundAssignment && compoundAssignment.Target == operation)
            {
                return true;
            }
            if (operation.Parent is IIncrementOrDecrementOperation incrementOrDecrement && incrementOrDecrement.Target == operation)
            {
                return true;
            }
            return false;
        }

        private static bool IsPotentiallyDispatchedGetter(IMethodSymbol getterSymbol)
        {
            return getterSymbol.ContainingType?.TypeKind == TypeKind.Interface ||
                   getterSymbol.IsVirtual ||
                   getterSymbol.IsAbstract ||
                   getterSymbol.IsOverride;
        }

        private static PurityAnalysisEngine.PurityAnalysisResult CheckDispatchedGetterPurity(
            IPropertyReferenceOperation propertyReferenceOperation,
            PurityAnalysisContext context)
        {
            var candidates = ResolvePotentialGetterTargets(
                propertyReferenceOperation.Property,
                context.SemanticModel,
                GetKnownReceiverType(propertyReferenceOperation.Instance));

            if (candidates.IsDefaultOrEmpty)
            {
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(
                    propertyReferenceOperation.Syntax,
                    PurityAnalysisEngine.PurityEvidence.Create(
                        "dynamic_dispatch",
                        nameof(PropertyReferencePurityRule),
                        propertyReferenceOperation,
                        symbol: propertyReferenceOperation.Property.GetMethod));
            }

            foreach (var getter in candidates)
            {
                var getterResult = PurityAnalysisEngine.GetCalleePurity(getter, context);
                if (!getterResult.IsPure)
                {
                    return getterResult.WithCallee(getter, propertyReferenceOperation.Syntax);
                }
            }

            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }

        private static PurityAnalysisEngine.PurityAnalysisResult GetterResultOrPure(
            PurityAnalysisEngine.PurityAnalysisResult getterResult,
            IMethodSymbol getterSymbol,
            IPropertyReferenceOperation propertyReferenceOperation)
        {
            return getterResult.IsPure
                ? PurityAnalysisEngine.PurityAnalysisResult.Pure
                : getterResult.WithCallee(getterSymbol, propertyReferenceOperation.Syntax);
        }

        private static string GetCatalogHitCategory(ISymbol symbol)
        {
            var containingType = symbol.ContainingType?.ToDisplayString() ?? string.Empty;
            var containingNamespace = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;

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

        private static ImmutableArray<IMethodSymbol> ResolvePotentialGetterTargets(
            IPropertySymbol propertySymbol,
            SemanticModel semanticModel,
            INamedTypeSymbol? knownReceiverType)
        {
            var targets = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            var targetProperty = propertySymbol.OriginalDefinition;

            if (knownReceiverType != null &&
                (knownReceiverType.TypeKind == TypeKind.Struct || knownReceiverType.IsSealed))
            {
                AddGetterForReceiverType(knownReceiverType, targetProperty, targets);
                return targets.ToImmutableArray();
            }

            if (targetProperty.ContainingType?.TypeKind == TypeKind.Interface)
            {
                foreach (var type in EnumerateAllNamedTypes(semanticModel.Compilation.Assembly.GlobalNamespace))
                {
                    if (type.TypeKind != TypeKind.Class && type.TypeKind != TypeKind.Struct)
                    {
                        continue;
                    }

                    if (!ImplementsInterface(type, targetProperty.ContainingType))
                    {
                        continue;
                    }

                    AddGetterForReceiverType(type, targetProperty, targets);
                }

                if (targetProperty.GetMethod != null && !targetProperty.GetMethod.IsAbstract)
                {
                    targets.Add(targetProperty.GetMethod.OriginalDefinition);
                }

                return targets.ToImmutableArray();
            }

            var baseProperty = GetRootOverriddenProperty(targetProperty);
            var baseType = baseProperty.ContainingType;
            if (baseType != null)
            {
                foreach (var type in EnumerateAllNamedTypes(semanticModel.Compilation.Assembly.GlobalNamespace))
                {
                    if (!DerivesFrom(type, baseType))
                    {
                        continue;
                    }

                    foreach (var property in type.GetMembers(baseProperty.Name).OfType<IPropertySymbol>())
                    {
                        if (OverridesProperty(property, baseProperty) && property.GetMethod != null)
                        {
                            targets.Add(property.GetMethod.OriginalDefinition);
                        }
                    }
                }
            }

            if (baseProperty.GetMethod != null && !baseProperty.GetMethod.IsAbstract)
            {
                targets.Add(baseProperty.GetMethod.OriginalDefinition);
            }

            return targets.ToImmutableArray();
        }

        private static INamedTypeSymbol? GetKnownReceiverType(IOperation? instanceOperation)
        {
            var unwrapped = PurityAnalysisEngine.SkipImplicitConversions(instanceOperation);
            if (unwrapped is IObjectCreationOperation objectCreationOperation)
            {
                return objectCreationOperation.Type as INamedTypeSymbol;
            }

            return unwrapped?.Type as INamedTypeSymbol;
        }

        private static void AddGetterForReceiverType(
            INamedTypeSymbol receiverType,
            IPropertySymbol targetProperty,
            HashSet<IMethodSymbol> targets)
        {
            ISymbol? implementation = null;
            if (targetProperty.ContainingType?.TypeKind == TypeKind.Interface)
            {
                implementation = receiverType.FindImplementationForInterfaceMember(targetProperty);
            }
            else
            {
                for (INamedTypeSymbol? current = receiverType; current != null; current = current.BaseType)
                {
                    implementation = current
                        .GetMembers(targetProperty.Name)
                        .OfType<IPropertySymbol>()
                        .FirstOrDefault(property =>
                            SymbolEqualityComparer.Default.Equals(property.OriginalDefinition, targetProperty) ||
                            OverridesProperty(property, targetProperty));
                    if (implementation != null)
                    {
                        break;
                    }
                }
            }

            if (implementation is IPropertySymbol propertySymbol && propertySymbol.GetMethod != null)
            {
                targets.Add(propertySymbol.GetMethod.OriginalDefinition);
            }
            else if (implementation is IMethodSymbol methodSymbol)
            {
                targets.Add(methodSymbol.OriginalDefinition);
            }
        }

        private static IPropertySymbol GetRootOverriddenProperty(IPropertySymbol propertySymbol)
        {
            var current = propertySymbol;
            while (current.OverriddenProperty != null)
            {
                current = current.OverriddenProperty;
            }

            return current.OriginalDefinition;
        }

        private static bool OverridesProperty(IPropertySymbol property, IPropertySymbol target)
        {
            var current = property;
            while (current != null)
            {
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, target.OriginalDefinition))
                {
                    return true;
                }

                current = current.OverriddenProperty;
            }

            return false;
        }

        private static bool ImplementsInterface(INamedTypeSymbol type, INamedTypeSymbol interfaceSymbol)
        {
            return type.AllInterfaces.Any(
                candidate => SymbolEqualityComparer.Default.Equals(
                    candidate.OriginalDefinition,
                    interfaceSymbol.OriginalDefinition));
        }

        private static bool DerivesFrom(INamedTypeSymbol type, INamedTypeSymbol potentialBase)
        {
            for (var current = type.BaseType; current != null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, potentialBase.OriginalDefinition))
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
                if (member is INamespaceSymbol namespaceSymbol)
                {
                    foreach (var nested in EnumerateAllNamedTypes(namespaceSymbol))
                    {
                        yield return nested;
                    }
                }
                else if (member is INamedTypeSymbol typeSymbol)
                {
                    yield return typeSymbol;
                    foreach (var nested in EnumerateNestedTypes(typeSymbol))
                    {
                        yield return nested;
                    }
                }
            }
        }

        private static IEnumerable<INamedTypeSymbol> EnumerateNestedTypes(INamedTypeSymbol typeSymbol)
        {
            foreach (var member in typeSymbol.GetTypeMembers())
            {
                yield return member;
                foreach (var nested in EnumerateNestedTypes(member))
                {
                    yield return nested;
                }
            }
        }

        private static bool IsCompilerGeneratedArrayForeachCurrent(
            IPropertyReferenceOperation propertyReferenceOperation,
            PurityAnalysisContext context)
        {
            if (propertyReferenceOperation.Property.Name != "Current" ||
                propertyReferenceOperation.Property.ContainingType?.ToDisplayString() != "System.Collections.IEnumerator" ||
                propertyReferenceOperation.Syntax.Parent is not ForEachStatementSyntax forEachStatement)
            {
                return false;
            }

            return context.SemanticModel.GetTypeInfo(forEachStatement.Expression).Type is IArrayTypeSymbol;
        }


    }
}
