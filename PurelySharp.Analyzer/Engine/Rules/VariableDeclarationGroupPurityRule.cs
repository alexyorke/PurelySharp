using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using PurelySharp.Analyzer.Engine; // Namespace for PurityAnalysisEngine
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes variable declaration groups for potential side effects in initializers.
    /// </summary>
    internal class VariableDeclarationGroupPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.VariableDeclarationGroup);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (operation is not IVariableDeclarationGroupOperation groupOperation)
            {
                return PurityAnalysisEngine.PurityAnalysisResult.Pure; // Should not happen
            }

            PurityAnalysisEngine.LogDebug($"  [VarDeclGrpRule] Checking VariableDeclarationGroup: {groupOperation.Syntax}");

            foreach (var declaration in groupOperation.Declarations)
            {
                PurityAnalysisEngine.LogDebug($"    [VarDeclGrpRule] Checking Declaration: {declaration.Syntax}");
                foreach (var declarator in declaration.Declarators)
                {
                    PurityAnalysisEngine.LogDebug($"      [VarDeclGrpRule] Checking Declarator: {declarator.Symbol.Name}");
                    if (declarator.Initializer != null)
                    {
                        var initializerValue = declarator.Initializer.Value; // Get the IOperation for the value
                        PurityAnalysisEngine.LogDebug($"        [VarDeclGrpRule] Checking Initializer: {initializerValue.Syntax} ({initializerValue.Kind})"); // Log kind
                        var initializerResult = PurityAnalysisEngine.CheckSingleOperation(initializerValue, context, currentState);
                        if (!initializerResult.IsPure)
                        {
                            PurityAnalysisEngine.LogDebug($"        [VarDeclGrpRule] --> IMPURE Initializer found: {declarator.Initializer.Syntax}");
                            // Propagate the specific impure node from the initializer if possible
                            return initializerResult.ImpureSyntaxNode != null
                                   ? PurityAnalysisEngine.PurityAnalysisResult.Impure(initializerResult.ImpureSyntaxNode)
                                   : PurityAnalysisEngine.PurityAnalysisResult.Impure(declarator.Initializer.Syntax);
                        }

                        // --- *** NEW: Delegate Target Tracking *** ---
                        ILocalSymbol declaredSymbol = declarator.Symbol;
                        if (declaredSymbol.Type?.TypeKind == TypeKind.Delegate)
                        {
                            PurityAnalysisEngine.LogDebug($"        [VarDeclGrpRule-DEL] Detected delegate variable declaration: {declaredSymbol.Name}");
                            PurityAnalysisEngine.LogDebug($"        [VarDeclGrpRule-DEL]   Initializer Op Kind: {initializerValue.Kind} | Syntax: {initializerValue.Syntax}");

                            PurityAnalysisEngine.PotentialTargets? valueTargets = null;
                            if (initializerValue is IMethodReferenceOperation methodRef)
                            {
                                valueTargets = PurityAnalysisEngine.PotentialTargets.FromSingle(methodRef.Method.OriginalDefinition);
                                PurityAnalysisEngine.LogDebug($"        [VarDeclGrpRule-DEL]   Initializer is Method Group: {methodRef.Method.ToDisplayString()}");
                            }
                            else if (initializerValue is IDelegateCreationOperation delegateCreation)
                            {
                                if (delegateCreation.Target is IMethodReferenceOperation lambdaRef)
                                {
                                    valueTargets = PurityAnalysisEngine.PotentialTargets.FromSingle(lambdaRef.Method.OriginalDefinition);
                                    PurityAnalysisEngine.LogDebug($"        [VarDeclGrpRule-DEL]   Initializer is Lambda/Delegate Creation targeting: {lambdaRef.Method.ToDisplayString()}");
                                }
                                else
                                {
                                    PurityAnalysisEngine.LogDebug($"        [VarDeclGrpRule-DEL]   Initializer is Lambda/Delegate Creation with unresolvable target ({delegateCreation.Target?.Kind}). Cannot track.");
                                }
                            }
                            else // Initializer is another variable/parameter/field/property reference
                            {
                                ISymbol? valueSourceSymbol = TryResolveSymbol(initializerValue); // Use same helper
                                if (valueSourceSymbol != null && currentState.DelegateTargetMap.TryGetValue(valueSourceSymbol, out var sourceTargets))
                                {
                                    valueTargets = sourceTargets; // Propagate targets
                                    PurityAnalysisEngine.LogDebug($"        [VarDeclGrpRule-DEL]   Initializer is reference to {valueSourceSymbol.Name}. Propagating {sourceTargets.MethodSymbols.Count} targets.");
                                }
                                else
                                {
                                    PurityAnalysisEngine.LogDebug($"        [VarDeclGrpRule-DEL]   Initializer is reference ({initializerValue.Kind}) but source symbol ({valueSourceSymbol?.Name ?? "null"}) not found in map or unresolved. Cannot track.");
                                }
                            }

                            if (valueTargets != null)
                            {
                                var nextState = currentState.WithDelegateTarget(declaredSymbol, valueTargets.Value);
                                PurityAnalysisEngine.LogDebug($"        [VarDeclGrpRule-DEL]   ---> Updating state map for {declaredSymbol.Name} with {valueTargets.Value.MethodSymbols.Count} target(s). New Map Count: {nextState.DelegateTargetMap.Count}");
                                // NOTE: State change local to this check for logging. Actual update in ApplyTransferFunction.
                            }
                        }
                        // --- *** END Delegate Target Tracking *** ---

                        PurityAnalysisEngine.LogDebug($"        [VarDeclGrpRule] Initializer is Pure.");
                    }
                    else
                    {
                        PurityAnalysisEngine.LogDebug($"      [VarDeclGrpRule] Declarator has no initializer. Pure.");
                    }
                }
            }

            PurityAnalysisEngine.LogDebug($"  [VarDeclGrpRule] VariableDeclarationGroup determined PURE.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }

        // Add helper (could be in PurityAnalysisEngine or here) - Reuse from AssignmentPurityRule
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