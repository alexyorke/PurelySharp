using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using PurelySharp.Analyzer.Engine;

namespace PurelySharp.Analyzer.Engine.Rules
{

    internal class AssignmentPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.SimpleAssignment, OperationKind.CompoundAssignment, OperationKind.Increment, OperationKind.Decrement);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            IOperation targetOperation;
            IOperation? valueOperation = null;
            SyntaxNode diagnosticNode = operation.Syntax;

            if (operation is IAssignmentOperation assignmentOperation)
            {
                targetOperation = assignmentOperation.Target;
                valueOperation = assignmentOperation.Value;

            }
            else if (operation is IIncrementOrDecrementOperation incrementDecrementOperation)
            {
                targetOperation = incrementDecrementOperation.Target;


            }
            else
            {
                PurityAnalysisEngine.LogDebug($"AssignmentPurityRule: Unexpected operation type {operation.Kind}. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }

            PurityAnalysisEngine.LogDebug($"AssignmentPurityRule: Analyzing Target {targetOperation?.Kind} in operation {operation.Kind}");

            if (targetOperation == null)
            {
                PurityAnalysisEngine.LogDebug("AssignmentPurityRule: Target operation is null. Assuming Pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }


            if (valueOperation != null)
            {
                PurityAnalysisEngine.LogDebug($"    [AssignRule] Checking assignment value (RHS): {valueOperation.Syntax} ({valueOperation.Kind})");
                var valueResult = PurityAnalysisEngine.CheckSingleOperation(valueOperation, context, currentState);
                if (!valueResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [AssignRule] Assignment value (RHS) itself is IMPURE. Assignment is Impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(valueResult.ImpureSyntaxNode ?? valueOperation.Syntax);
                }



                ITypeSymbol? targetType = (targetOperation as ILocalReferenceOperation)?.Type ??
                                          (targetOperation as IParameterReferenceOperation)?.Type ??
                                          (targetOperation as IFieldReferenceOperation)?.Type ??
                                          (targetOperation as IPropertyReferenceOperation)?.Type;

                ITypeSymbol? valueType = valueOperation.Type;

                if (targetType != null && valueType != null && !SymbolEqualityComparer.Default.Equals(targetType, valueType))
                {
                    IConversionOperation? conversionOp = null;


                    if (valueOperation is IConversionOperation topLevelConv &&
                        topLevelConv.Conversion.IsImplicit &&
                        SymbolEqualityComparer.Default.Equals(topLevelConv.Type, targetType))
                    {
                        conversionOp = topLevelConv;
                        PurityAnalysisEngine.LogDebug("    [AssignRule] Found implicit conversion as top-level value operation.");
                    }
                    else
                    {

                        conversionOp = valueOperation.DescendantsAndSelf()
                                        .OfType<IConversionOperation>()
                                        .FirstOrDefault(conv => conv.Conversion.IsImplicit &&
                                                               SymbolEqualityComparer.Default.Equals(conv.Type, targetType) &&
                                                               conv.Operand != null &&
                                                               SymbolEqualityComparer.Default.Equals(conv.Operand.Type, valueType));
                        if (conversionOp != null)
                        {
                            PurityAnalysisEngine.LogDebug("    [AssignRule] Found implicit conversion in descendants of value operation.");
                        }
                    }


                    if (conversionOp != null)
                    {
                        PurityAnalysisEngine.LogDebug($"    [AssignRule] Checking implicit conversion operation: {conversionOp.Syntax}");
                        var conversionResult = PurityAnalysisEngine.CheckSingleOperation(conversionOp, context, currentState);
                        if (!conversionResult.IsPure)
                        {

                            PurityAnalysisEngine.LogDebug("    [AssignRule] Implicit conversion operation reported IMPURE.");
                            return PurityAnalysisEngine.PurityAnalysisResult.Impure(conversionResult.ImpureSyntaxNode ?? conversionOp.Operand?.Syntax ?? valueOperation.Syntax);
                        }
                    }
                }

            }


            PurityAnalysisEngine.LogDebug($"    [AssignRule] Checking assignment target (LHS): {targetOperation.Syntax} ({targetOperation.Kind})");
            var targetResult = PurityAnalysisEngine.CheckSingleOperation(targetOperation, context, currentState);
            if (!targetResult.IsPure)
            {

                PurityAnalysisEngine.LogDebug($"AssignmentPurityRule: Target check failed (Kind: {targetOperation.Kind}, RefKind: {(targetOperation as IParameterReferenceOperation)?.Parameter.RefKind}). Reporting impurity on the whole operation: {operation.Syntax}");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
            }


            var targetSymbol = TryResolveSymbol(targetOperation);
            bool isPureAssignment = IsAssignmentTargetPure(targetOperation, context, targetSymbol);

            if (!isPureAssignment)
            {
                PurityAnalysisEngine.LogDebug($"    [AssignRule] Assignment target itself is considered impure for assignment. Assignment is Impure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Impure(operation.Syntax);
            }



            if (valueOperation != null && targetSymbol != null && targetOperation.Type?.TypeKind == TypeKind.Delegate)
            {
                PurityAnalysisEngine.LogDebug($"    [AssignRule-DEL] Detected delegate assignment to: {targetSymbol.Name} ({targetSymbol.Kind})");
                PurityAnalysisEngine.LogDebug($"    [AssignRule-DEL]   Value Op Kind: {valueOperation.Kind} | Syntax: {valueOperation.Syntax}");


                PurityAnalysisEngine.PotentialTargets? valueTargets = null;
                if (valueOperation is IMethodReferenceOperation methodRef)
                {

                    valueTargets = PurityAnalysisEngine.PotentialTargets.FromSingle(methodRef.Method.OriginalDefinition);
                    PurityAnalysisEngine.LogDebug($"    [AssignRule-DEL]   Value is Method Group: {methodRef.Method.ToDisplayString()}");
                }
                else if (valueOperation is IDelegateCreationOperation delegateCreation)
                {
                    if (delegateCreation.Target is IMethodReferenceOperation lambdaRef)
                    {

                        valueTargets = PurityAnalysisEngine.PotentialTargets.FromSingle(lambdaRef.Method.OriginalDefinition);
                        PurityAnalysisEngine.LogDebug($"    [AssignRule-DEL]   Value is Lambda/Delegate Creation targeting: {lambdaRef.Method.ToDisplayString()}");
                    }
                    else
                    {
                        PurityAnalysisEngine.LogDebug($"    [AssignRule-DEL]   Value is Lambda/Delegate Creation with unresolvable target ({delegateCreation.Target?.Kind}). Cannot track.");
                    }
                }
                else
                {
                    ISymbol? valueSourceSymbol = TryResolveSymbol(valueOperation);
                    if (valueSourceSymbol != null && currentState.DelegateTargetMap.TryGetValue(valueSourceSymbol, out var sourceTargets))
                    {
                        valueTargets = sourceTargets;
                        PurityAnalysisEngine.LogDebug($"    [AssignRule-DEL]   Value is reference to {valueSourceSymbol.Name}. Propagating {sourceTargets.MethodSymbols.Count} targets.");
                    }
                    else
                    {
                        PurityAnalysisEngine.LogDebug($"    [AssignRule-DEL]   Value is reference ({valueOperation.Kind}) but source symbol ({valueSourceSymbol?.Name ?? "null"}) not found in map or unresolved. Cannot track.");
                    }
                }

                if (valueTargets != null)
                {


                    var nextState = currentState.WithDelegateTarget(targetSymbol, valueTargets.Value);

                    PurityAnalysisEngine.LogDebug($"    [AssignRule-DEL]   ---> Updating state map for {targetSymbol.Name} with {valueTargets.Value.MethodSymbols.Count} target(s). New Map Count: {nextState.DelegateTargetMap.Count}");



                }
            }


            PurityAnalysisEngine.LogDebug("AssignmentPurityRule: Both target and value (if applicable) are pure. Result: Pure");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }

        private bool IsAssignmentTargetPure(IOperation targetOperation, PurityAnalysisContext context, ISymbol? targetSymbol)
        {
            switch (targetOperation.Kind)
            {
                case OperationKind.LocalReference:
                    PurityAnalysisEngine.LogDebug($"    [AssignRule-Target] Target: LocalReference '{targetSymbol?.Name ?? "Unknown"}' - Pure Target Location");
                    return true;

                case OperationKind.ParameterReference:
                    if (targetOperation is IParameterReferenceOperation paramRef)
                    {
                        if (paramRef.Parameter.RefKind == RefKind.Ref || paramRef.Parameter.RefKind == RefKind.Out ||
                            paramRef.Parameter.RefKind == RefKind.In || paramRef.Parameter.RefKind == RefKind.RefReadOnly)
                        {
                            PurityAnalysisEngine.LogDebug($" Assignment Target: ParameterReference ({paramRef.Parameter.RefKind}) modification attempt - Impure Target");
                            return false;
                        }
                        else
                        {
                            PurityAnalysisEngine.LogDebug(" Assignment Target: ParameterReference (value) - Pure Target");
                            return true;
                        }
                    }
                    return true;

                case OperationKind.FieldReference:
                    var fieldRefOp = (IFieldReferenceOperation)targetOperation;
                    if (fieldRefOp.Field.IsStatic)
                    {
                        PurityAnalysisEngine.LogDebug($" Assignment Target: Static FieldReference '{fieldRefOp.Field.Name}' - Impure Target");
                        return false;
                    }
                    if (fieldRefOp.Instance is IInstanceReferenceOperation instanceRef &&
                        instanceRef.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance &&
                        context.ContainingMethodSymbol.MethodKind == MethodKind.Constructor)
                    {
                        PurityAnalysisEngine.LogDebug(" Assignment Target: Instance FieldReference 'this.Field' within Constructor - Allowed (Target is Pure)");
                        return true;
                    }
                    PurityAnalysisEngine.LogDebug($" Assignment Target: FieldReference '{fieldRefOp.Field.Name}' (Non-Static, Non-Constructor 'this.Field') - Impure Target");
                    return false;

                case OperationKind.PropertyReference:
                    var propRefOp = (IPropertyReferenceOperation)targetOperation;
                    if (propRefOp.Property.IsStatic)
                    {
                        PurityAnalysisEngine.LogDebug(" Assignment Target: Static PropertyReference - Impure Target");
                        return false;
                    }


                    if (propRefOp.Property.SetMethod != null && propRefOp.Property.SetMethod.IsInitOnly)
                    {
                        PurityAnalysisEngine.LogDebug(" Assignment Target: Init-only PropertyReference - Allowed (Target is Pure by IsAssignmentTargetPure)");
                        return true;
                    }


                    if (propRefOp.Instance is IInstanceReferenceOperation instanceRefKind &&
                        instanceRefKind.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance)
                    {
                        if (context.ContainingMethodSymbol.MethodKind == MethodKind.Constructor)
                        {
                            PurityAnalysisEngine.LogDebug(" Assignment Target: Instance PropertyReference 'this.Prop' (non-init) within Constructor - Allowed (Target is Pure)");
                            return true;
                        }

                        if (context.ContainingMethodSymbol.ContainingType.IsRecord &&
                            context.ContainingMethodSymbol.ContainingType.IsValueType &&
                            PurityAnalysisEngine.IsPureEnforced(context.ContainingMethodSymbol, context.EnforcePureAttributeSymbol))
                        {
                            PurityAnalysisEngine.LogDebug(" Assignment Target: Instance PropertyReference 'this.Prop' (non-init) within [EnforcePure] Record Struct Method - Target is Impure for this method context");
                            return false;
                        }

                        PurityAnalysisEngine.LogDebug(" Assignment Target: Instance PropertyReference 'this.Prop' (non-init, Non-Constructor/Special Record) - Impure Target due to 'this' modification");
                        return false;
                    }



                    PurityAnalysisEngine.LogDebug($" Assignment Target: PropertyReference on local/param for non-init prop ('{propRefOp.Instance?.Syntax}') - Impure Target by IsAssignmentTargetPure rule.");
                    return false;

                case OperationKind.ArrayElementReference:
                    PurityAnalysisEngine.LogDebug(" Assignment Target: ArrayElementReference - Impure Target");
                    return false;

                default:
                    PurityAnalysisEngine.LogDebug($" Assignment Target: Unhandled Kind {targetOperation.Kind} - Assuming Impure Target");
                    return false;
            }
        }


        private static ISymbol? TryResolveSymbol(IOperation? operation)
        {
            return operation switch
            {
                ILocalReferenceOperation localRef => localRef.Local,
                IParameterReferenceOperation paramRef => paramRef.Parameter,
                IFieldReferenceOperation fieldRef => fieldRef.Field,
                IPropertyReferenceOperation propRef => propRef.Property,

                _ => null
            };
        }
    }
}