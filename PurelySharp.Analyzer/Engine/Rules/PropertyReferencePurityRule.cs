using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
//using System.Linq; // REMOVED - Now using PurityAnalysisEngine.HasAttribute
using PurelySharp.Analyzer.Engine; // Ensure PurityAnalysisEngine is accessible

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Checks the purity of property reference operations (reading properties).
    /// </summary>
    internal class PropertyReferencePurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.PropertyReference);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IPropertyReferenceOperation propertyReferenceOperation))
            {
                return PurityAnalysisEngine.PurityAnalysisResult.Pure; // Should not happen
            }

            IPropertySymbol propertySymbol = propertyReferenceOperation.Property;
            PurityAnalysisEngine.LogDebug($"  [PropRefRule] Checking PropertyReference: {propertySymbol.Name} on Type: {propertySymbol.ContainingType?.ToDisplayString()}");

            // Skip checks if this property reference is the target of an assignment
            if (IsPartOfAssignmentTarget(propertyReferenceOperation))
            {
                PurityAnalysisEngine.LogDebug($"    [PropRefRule] Skipping property read {propertySymbol.Name} as it's an assignment target.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            // If the property itself is marked [EnforcePure], assume it's pure.
            if (PurityAnalysisEngine.IsPureEnforced(propertySymbol, context.EnforcePureAttributeSymbol))
            {
                PurityAnalysisEngine.LogDebug($"    [PropRefRule] Property {propertySymbol.Name} has [EnforcePure]. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            // Check if the property is known impure (e.g., DateTime.Now)
            // +++ Log signature check +++
            string impureSig = propertySymbol.OriginalDefinition.ToDisplayString();
            PurityAnalysisEngine.LogDebug($"      [PropRefRule] Checking IsKnownImpure for property: '{impureSig}'");
            if (PurityAnalysisEngine.IsKnownImpure(propertySymbol))
            {
                PurityAnalysisEngine.LogDebug($"    [PropRefRule] Property {propertySymbol.Name} is known impure. Impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(propertyReferenceOperation.Syntax);
            }

            // Handle property access based on whether it's static or instance
            if (propertySymbol.IsStatic)
            {
                // Static property access
                PurityAnalysisEngine.LogDebug($"    [PropRefRule] Static property access: {propertySymbol.Name}");

                var cctorResult = PurityAnalysisEngine.CheckStaticConstructorPurity(propertySymbol.ContainingType, context, currentState);
                if (!cctorResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [PropRefRule] Static property '{propertySymbol.Name}' access IMPURE due to impure static constructor in {propertySymbol.ContainingType.Name}.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(propertyReferenceOperation.Syntax);
                }

                // +++ Log signature check +++
                string staticPureSig = propertySymbol.OriginalDefinition.ToDisplayString();
                bool staticKnownPure = PurityAnalysisEngine.IsKnownPureBCLMember(propertySymbol);
                PurityAnalysisEngine.LogDebug($"      [PropRefRule] Checking IsKnownPureBCLMember for static property: '{staticPureSig}' -> {staticKnownPure}");

                if (propertySymbol.IsReadOnly || staticKnownPure) // Check if static readonly or known pure BCL
                {
                    PurityAnalysisEngine.LogDebug($"    [PropRefRule] Static property '{propertySymbol.Name}' is readonly or known pure BCL. Read is Pure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                }
                else
                {
                    // Accessing non-readonly static property is impure
                    PurityAnalysisEngine.LogDebug($"    [PropRefRule] Static property '{propertySymbol.Name}' is not readonly or known pure BCL. Read is Impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(propertyReferenceOperation.Syntax);
                }
            }
            else // Instance property access
            {
                PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance property access: {propertySymbol.Name}");
                IOperation? instanceOperation = propertyReferenceOperation.Instance;

                // +++ Log instance symbol info +++
                string instanceKind = instanceOperation?.Kind.ToString() ?? "null";
                string instanceSyntax = instanceOperation?.Syntax.ToString() ?? "null";
                PurityAnalysisEngine.LogDebug($"      [PropRefRule] Instance Operation Kind: {instanceKind}, Syntax: {instanceSyntax}");

                if (instanceOperation == null)
                {
                    // This can happen in some complex scenarios or invalid code.
                    PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance operation is null for property '{propertySymbol.Name}'. Assuming Impure for safety.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(propertyReferenceOperation.Syntax);
                }

                // Check if the instance itself is a parameter reference marked 'in' or 'ref readonly'
                if (instanceOperation is IParameterReferenceOperation paramRef &&
                    (paramRef.Parameter.RefKind == RefKind.In || paramRef.Parameter.RefKind == (RefKind)4 /* RefKind.RefReadOnlyParameter */))
                {
                    bool isValueStruct = paramRef.Parameter.Type.IsValueType && !paramRef.Parameter.Type.IsReferenceType;
                    PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is ParameterReference '{paramRef.Parameter.Name}', RefKind={paramRef.Parameter.RefKind}, IsValueStruct={isValueStruct}");

                    // *** ADDED: Check getter purity from cache before assuming pure ***
                    if (propertySymbol.GetMethod != null &&
                        context.PurityCache.TryGetValue(propertySymbol.GetMethod.OriginalDefinition, out var cachedGetterResult) &&
                        !cachedGetterResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is readonly ref/in param, but getter {propertySymbol.GetMethod.Name} is known impure from cache. Returning Impure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(cachedGetterResult.ImpureSyntaxNode ?? propertyReferenceOperation.Syntax);
                    }
                    // *** END ADDED ***

                    PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance '{paramRef.Parameter.Name}' is value struct or readonly ref. Property read is Pure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                }
                else if (instanceOperation is IInstanceReferenceOperation instanceRef && instanceRef.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance)
                {
                    // Reading instance property via 'this'
                    bool isReadonlyStruct = context.ContainingMethodSymbol.ContainingType.IsReadOnly &&
                                            context.ContainingMethodSymbol.ContainingType.IsValueType;

                    if (isReadonlyStruct)
                    {
                        // *** ADDED: Check getter purity from cache before assuming pure ***
                        if (propertySymbol.GetMethod != null &&
                            context.PurityCache.TryGetValue(propertySymbol.GetMethod.OriginalDefinition, out var cachedGetterResult) &&
                            !cachedGetterResult.IsPure)
                        {
                            PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is 'this' in readonly struct, but getter {propertySymbol.GetMethod.Name} is known impure from cache. Returning Impure.");
                            return PurityAnalysisEngine.PurityAnalysisResult.Impure(cachedGetterResult.ImpureSyntaxNode ?? propertyReferenceOperation.Syntax);
                        }
                        // *** END ADDED ***

                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is 'this' within a readonly struct. Read is Pure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }
                    else if (propertySymbol.IsReadOnly) // Checks for { get; } or { get; init; }
                    {
                        // *** REVERTED: Removed explicit cache check ***
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is 'this', property '{propertySymbol.Name}' is readonly (get/init-only). Read is Pure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }
                    // *** REVERTED: Restored Recursive call for 'this' instance ***
                    else if (propertySymbol.GetMethod != null)
                    {
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is 'this', property '{propertySymbol.Name}' has a getter. Recursively checking getter purity.");
                        var thisGetterResult = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                            propertySymbol.GetMethod.OriginalDefinition,
                            context.SemanticModel, // Use the current context's model
                            context.EnforcePureAttributeSymbol,
                            context.AllowSynchronizationAttributeSymbol,
                            context.VisitedMethods,
                            context.PurityCache
                        );
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Getter purity result for '{propertySymbol.Name}': IsPure={thisGetterResult.IsPure}");
                        // The property read is pure if the getter is pure
                        return thisGetterResult;
                    }
                    else // Property is not readonly, not in readonly struct, has no getter (should be rare)
                    {
                        // *** REVERTED: Removed explicit cache check ***
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is 'this', property '{propertySymbol.Name}' is not readonly and has no accessible getter to analyze. Read is Impure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(propertyReferenceOperation.Syntax);
                    }
                }
                else
                {
                    // Instance is something else (e.g., result of method call, another field, local variable)
                    // Check if the property *itself* is known pure (e.g., List<T>.Count)
                    // +++ Log signature check +++
                    string instancePureSig = propertySymbol.OriginalDefinition.ToDisplayString();
                    bool instanceKnownPure = PurityAnalysisEngine.IsKnownPureBCLMember(propertySymbol);
                    PurityAnalysisEngine.LogDebug($"      [PropRefRule] Checking IsKnownPureBCLMember for instance property: '{instancePureSig}' -> {instanceKnownPure}");

                    if (instanceKnownPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance property '{propertySymbol.Name}' is known pure BCL. Read is Pure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }
                    // +++ ADDED: Check for [Pure] attribute on the getter +++
                    else if (propertySymbol.GetMethod != null && context.PureAttributeSymbol != null &&
                             PurityAnalysisEngine.HasAttribute(propertySymbol.GetMethod, context.PureAttributeSymbol))
                    {
                        // *** ADDED: Check getter purity from cache before assuming pure ***
                        if (context.PurityCache.TryGetValue(propertySymbol.GetMethod.OriginalDefinition, out var cachedGetterResult) &&
                            !cachedGetterResult.IsPure)
                        {
                            PurityAnalysisEngine.LogDebug($"    [PropRefRule] Property '{propertySymbol.Name}' getter has [Pure] attribute, but getter is known impure from cache. Returning Impure.");
                            return PurityAnalysisEngine.PurityAnalysisResult.Impure(cachedGetterResult.ImpureSyntaxNode ?? propertyReferenceOperation.Syntax);
                        }
                        // *** END ADDED ***

                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Property '{propertySymbol.Name}' getter has [Pure] attribute. Read is Pure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }
                    // +++ NEW: Check getter purity for complex instance access +++
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
                        return complexGetterResult; // Purity depends on getter
                    }
                    // --- END NEW ---
                    else
                    {
                        // *** ADDED: Check getter purity from cache before assuming pure ***
                        if (propertySymbol.GetMethod != null &&
                            context.PurityCache.TryGetValue(propertySymbol.GetMethod.OriginalDefinition, out var cachedGetterResult) &&
                            !cachedGetterResult.IsPure)
                        {
                            PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance is complex, property {propertySymbol.Name} known pure BCL, but getter is known impure from cache. Returning Impure.");
                            return PurityAnalysisEngine.PurityAnalysisResult.Impure(cachedGetterResult.ImpureSyntaxNode ?? propertyReferenceOperation.Syntax);
                        }
                        // *** END ADDED ***

                        PurityAnalysisEngine.LogDebug($"    [PropRefRule] Instance property '{propertySymbol.Name}' is known pure BCL. Read is Pure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Pure;
                    }
                }
            }

            // 2. Check Indexer Argument Purity (if applicable)
            if (propertyReferenceOperation.Arguments.Any())
            {
                PurityAnalysisEngine.LogDebug($"    [PropRefRule] Checking {propertyReferenceOperation.Arguments.Length} Indexer Arguments...");
                foreach (var argument in propertyReferenceOperation.Arguments)
                {
                    PurityAnalysisEngine.LogDebug($"      [PropRefRule.Args] Checking Argument: {argument.Syntax} ({argument.Value?.Kind})");
                    var argumentResult = PurityAnalysisEngine.CheckSingleOperation(argument.Value, context, currentState); // Check the value of the argument
                    if (!argumentResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"      [PropRefRule.Args] Argument '{argument.Syntax}' is IMPURE. Property reference is Impure.");
                        return PurityAnalysisEngine.PurityAnalysisResult.Impure(argument.Syntax);
                    }
                }
            }

            // 4. Check Property Getter Purity
            IMethodSymbol? getter = propertySymbol.GetMethod;
            if (getter == null)
            {
                PurityAnalysisEngine.LogDebug($"    [PropRefRule] Property '{propertySymbol.Name}' has no getter. Read is Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }
            var getterResult = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                getter.OriginalDefinition,
                context.SemanticModel,
                context.EnforcePureAttributeSymbol,
                context.AllowSynchronizationAttributeSymbol,
                context.VisitedMethods,
                context.PurityCache
            );
            PurityAnalysisEngine.LogDebug($"    [PropRefRule] Getter purity result for '{propertySymbol.Name}': IsPure={getterResult.IsPure}");
            return getterResult;
        }

        private static bool IsPartOfAssignmentTarget(IOperation operation)
        {
            // Simpler check: Only look one level up for assignment.
            // More complex target checks are difficult to get right.
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

        // *** REMOVED CheckStaticConstructorPurity helper ***

        // +++ REMOVED HasAttribute HELPER (Moved to PurityAnalysisEngine) +++
        // private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeSymbol)
        // {
        //     return symbol.GetAttributes().Any(ad => SymbolEqualityComparer.Default.Equals(ad.AttributeClass, attributeSymbol));
        // }
        // --- END REMOVED HELPER ---
    }
}