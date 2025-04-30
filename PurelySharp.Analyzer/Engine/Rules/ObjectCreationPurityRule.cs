using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using static PurelySharp.Analyzer.Engine.PurityAnalysisEngine; // Import static members like PurityAnalysisResult

namespace PurelySharp.Analyzer.Engine.Rules
{
    internal class ObjectCreationPurityRule : IPurityRule
    {
        public PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context)
        {
            if (operation is not IObjectCreationOperation objectCreationOperation)
            {
                return PurityAnalysisResult.Pure; // Should not happen if ApplicableOperationKinds is correct
            }

            // Check arguments first
            if (objectCreationOperation.Arguments.Length > 0)
            {
                PurityAnalysisEngine.LogDebug($"    [ObjCreationRule] Checking {objectCreationOperation.Arguments.Length} arguments...");
                foreach (var argument in objectCreationOperation.Arguments)
                {
                    PurityAnalysisResult argumentPurity;

                    // Handle ParamArray arguments specifically
                    if (argument.ArgumentKind == ArgumentKind.ParamArray && argument.Value is IArrayCreationOperation arrayCreation)
                    {
                        PurityAnalysisEngine.LogDebug($"    [ObjCreationRule] Argument '{argument.Syntax}' is ParamArray. Checking elements...");
                        argumentPurity = PurityAnalysisResult.Pure; // Assume pure until an element is impure

                        if (arrayCreation.Initializer != null)
                        {
                            foreach (var elementValue in arrayCreation.Initializer.ElementValues)
                            {
                                var elementPurity = PurityAnalysisEngine.CheckSingleOperation(elementValue, context);
                                if (!elementPurity.IsPure)
                                {
                                    PurityAnalysisEngine.LogDebug($"    [ObjCreationRule] ParamArray element '{elementValue.Syntax}' is Impure. Result: Impure.");
                                    argumentPurity = elementPurity; // Mark as impure
                                    break; // Stop checking elements for this argument
                                }
                                PurityAnalysisEngine.LogDebug($"    [ObjCreationRule] ParamArray element '{elementValue.Syntax}' is Pure.");
                            }
                        }
                        else
                        {
                            PurityAnalysisEngine.LogDebug($"    [ObjCreationRule] ParamArray argument '{argument.Syntax}' has null initializer. Assuming pure for this argument.");
                        }
                    }
                    else
                    {
                        // Standard argument check
                        argumentPurity = PurityAnalysisEngine.CheckSingleOperation(argument.Value, context);
                    }

                    // Check the result for this argument (either standard or ParamArray)
                    if (!argumentPurity.IsPure)
                    {
                        PurityAnalysisEngine.LogDebug($"    [ObjCreationRule] Argument '{argument.Syntax}' (or its element) is Impure. Result: Impure.");
                        return argumentPurity;
                    }
                    PurityAnalysisEngine.LogDebug($"    [ObjCreationRule] Argument '{argument.Syntax}' (including elements if ParamArray) is Pure.");
                }
                PurityAnalysisEngine.LogDebug($"    [ObjCreationRule] All arguments are Pure.");
            }
            else
            {
                PurityAnalysisEngine.LogDebug($"    [ObjCreationRule] No arguments to check.");
            }

            // Check constructor purity (if one is resolved)
            if (objectCreationOperation.Constructor is not null)
            {
                var constructorMethodSymbol = objectCreationOperation.Constructor;
                PurityAnalysisEngine.LogDebug($"    [ObjCreationRule] Recursively checking constructor: {constructorMethodSymbol.ToDisplayString()}");

                var constructorPurity = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                                                constructorMethodSymbol.OriginalDefinition, // Analyze the definition
                                                context.SemanticModel,
                                                context.EnforcePureAttributeSymbol,
                                                context.AllowSynchronizationAttributeSymbol,
                                                context.VisitedMethods,
                                                context.PurityCache);

                // Trust the result from the recursive analysis for the constructor.
                if (!constructorPurity.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($"    [ObjCreationRule] Constructor '{constructorMethodSymbol.ToDisplayString()}' determined IMPURE by recursive check. Result: Impure.");
                    return constructorPurity; // Return the trusted impure result
                }
                PurityAnalysisEngine.LogDebug($"    [ObjCreationRule] Constructor '{constructorMethodSymbol.ToDisplayString()}' determined PURE by recursive check. Trusting result.");
            }
            else
            {
                PurityAnalysisEngine.LogDebug($"    [ObjCreationRule] Constructor symbol not resolved (e.g., anonymous type). Assuming pure for now.");
                // Consider if anonymous types should always be pure or if we need more checks?
                // For now, if args are pure, and no specific constructor is impure, assume pure.
            }

            // *** NEW CHECK: Check for array creation ***
            if (objectCreationOperation.Type is IArrayTypeSymbol)
            {
                PurityAnalysisEngine.LogDebug($"    [ObjCreationRule] Object creation '{objectCreationOperation.Syntax}' is IMPURE because it creates an array.");
                return PurityAnalysisResult.Impure(objectCreationOperation.Syntax);
            }

            // *** NEW CHECK: Check for common mutable collection types ***
            string? typeName = objectCreationOperation.Type?.OriginalDefinition.ToDisplayString();
            if (typeName != null && (
                typeName.StartsWith("System.Collections.Generic.List<") ||
                typeName.StartsWith("System.Collections.Generic.Dictionary<") ||
                typeName.StartsWith("System.Collections.Generic.HashSet<") ||
                typeName.StartsWith("System.Collections.Generic.Queue<") || // Added
                typeName.StartsWith("System.Collections.Generic.Stack<") || // Added
                typeName.StartsWith("System.Text.StringBuilder") // Treat StringBuilder creation as impure
            ))
            {
                PurityAnalysisEngine.LogDebug($"    [ObjCreationRule] Object creation '{objectCreationOperation.Syntax}' is IMPURE because it creates a known mutable collection type or StringBuilder '{typeName}'.");
                return PurityAnalysisResult.Impure(objectCreationOperation.Syntax);
            }

            // Check if the TYPE itself is known to be mutable/impure (existing check)
            if (objectCreationOperation.Type != null && PurityAnalysisEngine.IsInImpureNamespaceOrType(objectCreationOperation.Type))
            {
                PurityAnalysisEngine.LogDebug($"    [ObjCreationRule] Object creation '{objectCreationOperation.Syntax}' is IMPURE because type '{objectCreationOperation.Type.ToDisplayString()}' is in a known impure namespace/type.");
                return PurityAnalysisResult.Impure(objectCreationOperation.Syntax);
            }
            // *** END NEW CHECK ***

            PurityAnalysisEngine.LogDebug($"    [ObjCreationRule] Object creation '{objectCreationOperation.Syntax}' determined to be Pure (Arguments & Constructor pure, Type not known impure).");
            return PurityAnalysisResult.Pure;
        }

        public IEnumerable<OperationKind> ApplicableOperationKinds => new[]
        {
            OperationKind.ObjectCreation
        };
    }
}