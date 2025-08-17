using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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


            if (IsPartOfAssignmentTarget(propertyReferenceOperation))
            {
                PurityAnalysisEngine.LogDebug($"    [PropRefRule] Skipping property read {propertySymbol.Name} as it's an assignment target.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }


            if (PurityAnalysisEngine.IsPureEnforced(propertySymbol, context.EnforcePureAttributeSymbol))
            {
                PurityAnalysisEngine.LogDebug($"    [PropRefRule] Property {propertySymbol.Name} has [EnforcePure]. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }



            string impureSig = propertySymbol.OriginalDefinition.ToDisplayString();
            PurityAnalysisEngine.LogDebug($"      [PropRefRule] Checking IsKnownImpure for property: '{impureSig}'");
            if (PurityAnalysisEngine.IsKnownImpure(propertySymbol))
            {
                PurityAnalysisEngine.LogDebug($"    [PropRefRule] Property {propertySymbol.Name} is known impure. Impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(propertyReferenceOperation.Syntax);
            }


            if (propertySymbol.IsStatic)
            {

                PurityAnalysisEngine.LogDebug($"    [PropRefRule] Static property access: {propertySymbol.Name}");

                var cctorResult = PurityAnalysisEngine.CheckStaticConstructorPurity(propertySymbol.ContainingType, context, currentState);
                if (!cctorResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [PropRefRule] Static property '{propertySymbol.Name}' access IMPURE due to impure static constructor in {propertySymbol.ContainingType?.Name}.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(propertyReferenceOperation.Syntax);
                }


                string staticPureSig = propertySymbol.OriginalDefinition.ToDisplayString();
                bool staticKnownPure = PurityAnalysisEngine.IsKnownPureBCLMember(propertySymbol);
                PurityAnalysisEngine.LogDebug($"      [PropRefRule] Checking IsKnownPureBCLMember for static property: '{staticPureSig}' -> {staticKnownPure}");

                if (propertySymbol.IsReadOnly || staticKnownPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [PropRefRule] Static property '{propertySymbol.Name}' is readonly or known pure BCL. Read is Pure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                }
                else
                {

                    PurityAnalysisEngine.LogDebug($"    [PropRefRule] Static property '{propertySymbol.Name}' is not readonly or known pure BCL. Read is Impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(propertyReferenceOperation.Syntax);
                }
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
                    (paramRef.Parameter.RefKind == RefKind.In || paramRef.Parameter.RefKind == (RefKind)4))
                {
                    bool isValueStruct = paramRef.Parameter.Type.IsValueType && !paramRef.Parameter.Type.IsReferenceType;
                    PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is ParameterReference '{paramRef.Parameter.Name}', RefKind={paramRef.Parameter.RefKind}, IsValueStruct={isValueStruct}");


                    if (propertySymbol.GetMethod != null &&
                        context.PurityCache.TryGetValue(propertySymbol.GetMethod.OriginalDefinition, out var cachedGetterResult) &&
                        !cachedGetterResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is readonly ref/in param, but getter {propertySymbol.GetMethod.Name} is known impure from cache. Returning Impure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(cachedGetterResult.ImpureSyntaxNode ?? propertyReferenceOperation.Syntax);
                    }


                    PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance '{paramRef.Parameter.Name}' is value struct or readonly ref. Property read is Pure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                }
                else if (instanceOperation is IInstanceReferenceOperation instanceRef && instanceRef.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance)
                {

                    bool isReadonlyStruct = context.ContainingMethodSymbol?.ContainingType is { IsReadOnly: true, IsValueType: true };

                    if (isReadonlyStruct)
                    {

                        if (propertySymbol.GetMethod != null &&
                            context.PurityCache.TryGetValue(propertySymbol.GetMethod.OriginalDefinition, out var cachedGetterResult) &&
                            !cachedGetterResult.IsPure)
                        {
                            PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is 'this' in readonly struct, but getter {propertySymbol.GetMethod.Name} is known impure from cache. Returning Impure.");
                            return PurityAnalysisEngine.PurityAnalysisResult.Impure(cachedGetterResult.ImpureSyntaxNode ?? propertyReferenceOperation.Syntax);
                        }


                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is 'this' within a readonly struct. Read is Pure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }
                    else if (propertySymbol.IsReadOnly)
                    {
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is 'this', property '{propertySymbol.Name}' is readonly (get/init-only). Read is Pure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }
                    else if (propertySymbol.GetMethod != null)
                    {
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is 'this', property '{propertySymbol.Name}' has a getter. Recursively checking getter purity.");
                        var thisGetterResult = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                            propertySymbol.GetMethod.OriginalDefinition,
                            context.SemanticModel,
                            context.EnforcePureAttributeSymbol,
                            context.AllowSynchronizationAttributeSymbol,
                            context.VisitedMethods,
                            context.PurityCache
                        );
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Getter purity result for '{propertySymbol.Name}': IsPure={thisGetterResult.IsPure}");

                        return thisGetterResult;
                    }
                    else
                    {
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is 'this', property '{propertySymbol.Name}' is not readonly and has no accessible getter to analyze. Read is Impure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(propertyReferenceOperation.Syntax);
                    }
                }
                else
                {



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

                        if (context.PurityCache.TryGetValue(propertySymbol.GetMethod.OriginalDefinition, out var cachedGetterResult) &&
                            !cachedGetterResult.IsPure)
                        {
                            PurityAnalysisEngine.LogDebug($"    [PropRefRule] Property '{propertySymbol.Name}' getter has [Pure] attribute, but getter is known impure from cache. Returning Impure.");
                            return PurityAnalysisEngine.PurityAnalysisResult.Impure(cachedGetterResult.ImpureSyntaxNode ?? propertyReferenceOperation.Syntax);
                        }


                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Property '{propertySymbol.Name}' getter has [Pure] attribute. Read is Pure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }

                    else if (propertySymbol.GetMethod != null)
                    {
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is complex ({instanceKind}), property '{propertySymbol.Name}' has getter. Checking getter purity.");
                        var complexGetterResult = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                            propertySymbol.GetMethod.OriginalDefinition,
                            context.SemanticModel,
                            context.EnforcePureAttributeSymbol,
                            context.AllowSynchronizationAttributeSymbol,
                            context.VisitedMethods,
                            context.PurityCache
                        );
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Getter purity result for complex instance access to '{propertySymbol.Name}': IsPure={complexGetterResult.IsPure}");
                        return complexGetterResult;
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


    }
}