using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using static PurelySharp.Analyzer.Engine.PurityAnalysisEngine;

namespace PurelySharp.Analyzer.Engine.Rules
{
    internal class ObjectCreationPurityRule : IPurityRule
    {
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.ObjectCreation);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            if (!(operation is IObjectCreationOperation objectCreationOperation))
            {
                return PurityAnalysisResult.Pure;
            }


            if (objectCreationOperation.Arguments.Length > 0)
            {
                PurityAnalysisEngine.LogDebug($"    [ObjCreateRule] Checking {objectCreationOperation.Arguments.Length} constructor arguments...");
                foreach (var argument in objectCreationOperation.Arguments)
                {
                    PurityAnalysisEngine.LogDebug($"      [ObjCreateRule.Args] Checking Argument: {argument.Syntax} ({argument.Value?.Kind})");
                    if (argument.Value == null)
                    {
                        return PurityAnalysisResult.Impure(objectCreationOperation.Syntax);
                    }
                    var argumentResult = PurityAnalysisEngine.CheckSingleOperation(argument.Value, context, currentState);
                    if (!argumentResult.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"      [ObjCreateRule.Args] Argument '{argument.Syntax}' is IMPURE. Object creation is Impure.");
                        return argumentResult;
                    }
                }
            }
            else
            {
                PurityAnalysisEngine.LogDebug($"    [ObjCreateRule] No arguments to check.");
            }


            if (objectCreationOperation.Initializer != null)
            {
                PurityAnalysisEngine.LogDebug($"    [ObjCreateRule] Checking Initializer: {objectCreationOperation.Initializer.Syntax} ({objectCreationOperation.Initializer.Kind})");
                var initializerResult = PurityAnalysisEngine.CheckSingleOperation(objectCreationOperation.Initializer, context, currentState);
                if (!initializerResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [ObjCreateRule] Initializer expression is IMPURE. Object creation is Impure.");
                    return initializerResult;
                }
            }


            IMethodSymbol? constructorSymbol = objectCreationOperation.Constructor;
            if (constructorSymbol != null)
            {
                PurityAnalysisEngine.LogDebug($"    [ObjCreateRule] Checking Constructor: {constructorSymbol.ToDisplayString()}");

                var cctorResult = PurityAnalysisEngine.CheckStaticConstructorPurity(constructorSymbol.ContainingType, context, currentState);
                if (!cctorResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [ObjCreateRule] Constructor invocation IMPURE due to impure static constructor in {constructorSymbol.ContainingType?.Name}.");
                    return cctorResult;
                }

                var constructorPurity = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                                                constructorSymbol.OriginalDefinition,
                                                context.SemanticModel,
                                                context.EnforcePureAttributeSymbol,
                                                context.AllowSynchronizationAttributeSymbol,
                                                context.VisitedMethods,
                                                context.PurityCache);


                if (!constructorPurity.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [ObjCreateRule] Constructor '{constructorSymbol.ToDisplayString()}' determined IMPURE by recursive check. Result: Impure.");
                    return constructorPurity;
                }
                PurityAnalysisEngine.LogDebug($"    [ObjCreateRule] Constructor '{constructorSymbol.ToDisplayString()}' determined PURE by recursive check. Trusting result.");
            }
            else
            {
                PurityAnalysisEngine.LogDebug($"    [ObjCreateRule] Constructor symbol not resolved (e.g., anonymous type). Assuming pure for now.");


            }


            if (objectCreationOperation.Type is IArrayTypeSymbol)
            {
                PurityAnalysisEngine.LogDebug($"    [ObjCreateRule] Object creation '{objectCreationOperation.Syntax}' is IMPURE because it creates an array.");
                return PurityAnalysisResult.Impure(objectCreationOperation.Syntax);
            }


            string? typeName = objectCreationOperation.Type?.OriginalDefinition.ToDisplayString();
            if (typeName != null && (
                typeName.StartsWith("System.Collections.Generic.List<") ||
                typeName.StartsWith("System.Collections.Generic.Dictionary<") ||
                typeName.StartsWith("System.Collections.Generic.HashSet<") ||
                typeName.StartsWith("System.Collections.Generic.Queue<") ||
                typeName.StartsWith("System.Collections.Generic.Stack<")
            ))
            {
                PurityAnalysisEngine.LogDebug($"    [ObjCreateRule] Object creation '{objectCreationOperation.Syntax}' is IMPURE because it creates a known mutable collection type '{typeName}'. StringBuilder is handled separately or by usage.");
                return PurityAnalysisResult.Impure(objectCreationOperation.Syntax);
            }


            if (objectCreationOperation.Type != null && PurityAnalysisEngine.IsInImpureNamespaceOrType(objectCreationOperation.Type))
            {
                PurityAnalysisEngine.LogDebug($"    [ObjCreateRule] Object creation '{objectCreationOperation.Syntax}' is IMPURE because type '{objectCreationOperation.Type.ToDisplayString()}' is in a known impure namespace/type.");
                return PurityAnalysisResult.Impure(objectCreationOperation.Syntax);
            }


            PurityAnalysisEngine.LogDebug($"    [ObjCreateRule] Object creation '{objectCreationOperation.Syntax}' determined to be Pure (Arguments & Constructor pure, Type not known impure).");
            return PurityAnalysisResult.Pure;
        }
    }
}