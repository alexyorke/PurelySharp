using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace PurelySharp.Analyzer.Engine.Rules
{
    /// <summary>
    /// Analyzes using statements and declarations for potential side effects,
    /// focusing on the resource acquisition and implicit Dispose call.
    /// </summary>
    internal class UsingStatementPurityRule : IPurityRule
    {
        // Apply to both using statements and using declarations (C# 8.0+)
        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Using, OperationKind.UsingDeclaration);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            SyntaxNode? impureSyntaxNode = null;
            IOperation? resourceOperation = null;
            IOperation? bodyOperation = null; // Only applicable to OperationKind.Using

            if (operation is IUsingOperation usingOperation)
            {
                resourceOperation = usingOperation.Resources; // Can be declaration group or expression
                bodyOperation = usingOperation.Body;
                impureSyntaxNode = usingOperation.Syntax; // Default diagnostic location
                PurityAnalysisEngine.LogDebug($"UsingStatementPurityRule: Analyzing Using Statement");
            }
            else if (operation is IUsingDeclarationOperation usingDeclarationOperation)
            {
                resourceOperation = usingDeclarationOperation.DeclarationGroup;
                impureSyntaxNode = usingDeclarationOperation.Syntax; // Default diagnostic location
                PurityAnalysisEngine.LogDebug($"UsingStatementPurityRule: Analyzing Using Declaration");
            }
            else
            {
                PurityAnalysisEngine.LogDebug($"UsingStatementPurityRule: Unexpected operation kind {operation.Kind}. Assuming pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure; // Should not happen if ApplicableOperationKinds is correct
            }

            // 1. Check the purity of the resource acquisition/declaration
            if (resourceOperation != null)
            {
                PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Checking Resource operation {resourceOperation.Kind}");
                PurityAnalysisEngine.PurityAnalysisResult resourceResult = PurityAnalysisEngine.PurityAnalysisResult.Pure; // Assume pure initially

                if (resourceOperation is IVariableDeclarationGroupOperation declarationGroup)
                {
                    PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Resource is VariableDeclarationGroup. Checking initializers.");
                    foreach (var declaration in declarationGroup.Declarations)
                    {
                        foreach (var declarator in declaration.Declarators)
                        {
                            if (declarator.Initializer != null)
                            {
                                PurityAnalysisEngine.LogDebug($"  UsingStatementPurityRule: Checking initializer for {declarator.Symbol.Name}: {declarator.Initializer.Value?.Syntax}");
                                var initializerResult = PurityAnalysisEngine.CheckSingleOperation(declarator.Initializer.Value, context, currentState);
                                if (!initializerResult.IsPure)
                                {
                                    PurityAnalysisEngine.LogDebug($"  UsingStatementPurityRule: Initializer for {declarator.Symbol.Name} is IMPURE.");
                                    resourceResult = initializerResult; // Capture the first impure result
                                    break; // Stop checking initializers in this declarator
                                }
                                PurityAnalysisEngine.LogDebug($"  UsingStatementPurityRule: Initializer for {declarator.Symbol.Name} is Pure.");
                            }
                            else
                            {
                                // Declaration without initializer - considered pure in acquisition phase
                                PurityAnalysisEngine.LogDebug($"  UsingStatementPurityRule: No initializer for {declarator.Symbol.Name}. Assuming pure acquisition.");
                            }
                        }
                        if (!resourceResult.IsPure) break; // Stop checking declarations if impurity found
                    }
                }
                else if (resourceOperation is IVariableDeclarationOperation variableDeclaration) // Handle single IVariableDeclarationOperation if needed, though IVariableDeclarationGroupOperation is more common
                {
                    PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Resource is VariableDeclaration. Checking initializers.");
                    foreach (var declarator in variableDeclaration.Declarators)
                    {
                        if (declarator.Initializer != null)
                        {
                            PurityAnalysisEngine.LogDebug($"  UsingStatementPurityRule: Checking initializer for {declarator.Symbol.Name}: {declarator.Initializer.Value?.Syntax}");
                            var initializerResult = PurityAnalysisEngine.CheckSingleOperation(declarator.Initializer.Value, context, currentState);
                            if (!initializerResult.IsPure)
                            {
                                PurityAnalysisEngine.LogDebug($"  UsingStatementPurityRule: Initializer for {declarator.Symbol.Name} is IMPURE.");
                                resourceResult = initializerResult;
                                break;
                            }
                            PurityAnalysisEngine.LogDebug($"  UsingStatementPurityRule: Initializer for {declarator.Symbol.Name} is Pure.");
                        }
                        else
                        {
                            PurityAnalysisEngine.LogDebug($"  UsingStatementPurityRule: No initializer for {declarator.Symbol.Name}. Assuming pure acquisition.");
                        }
                    }
                }
                else // Resource is likely an expression (e.g., using (GetDisposable()))
                {
                    PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Resource is an expression {resourceOperation.Kind}. Checking expression directly.");
                    resourceResult = PurityAnalysisEngine.CheckSingleOperation(resourceOperation, context, currentState);
                }

                // Check the final result for the resource acquisition part
                if (!resourceResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Resource operation is IMPURE.");
                    return resourceResult; // Resource acquisition itself is impure
                }
                PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Resource operation is Pure.");
            }

            // 2. Check the purity of the body (for using statements)
            if (bodyOperation != null) // Only check body if it exists (i.e., for IUsingOperation)
            {
                PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Checking Body operation {bodyOperation.Kind}");
                var bodyResult = PurityAnalysisEngine.CheckSingleOperation(bodyOperation, context, currentState);
                if (!bodyResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Body operation is IMPURE.");
                    return bodyResult; // Body contains impure operations
                }
                PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Body operation is Pure.");
            }

            // 3. Check the purity of the implicit Dispose() call
            // Need to find the variable(s) declared and check their Dispose method.
            List<ILocalSymbol> declaredLocals = FindDeclaredLocals(resourceOperation);

            foreach (var local in declaredLocals)
            {
                if (local.Type == null || !ImplementsIDisposable(local.Type, context.SemanticModel.Compilation))
                {
                    PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Local '{local.Name}' is not IDisposable. Skipping Dispose check.");
                    continue; // Should not happen for using resources, but check anyway
                }

                PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Checking implicit Dispose() for local '{local.Name}' of type {local.Type.Name}");

                // Find the Dispose method symbol
                IMethodSymbol? disposeMethod = FindDisposeMethod(local.Type, context.SemanticModel.Compilation);

                if (disposeMethod == null)
                {
                    // Should be rare if it implements IDisposable, but handle it.
                    PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Could not find Dispose method for type {local.Type.Name}. Assuming impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(impureSyntaxNode ?? operation.Syntax);
                }

                // Check the purity of the specific Dispose method recursively
                // Important: Pass the current context to continue cycle detection and caching
                PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Recursively checking purity of {disposeMethod.ToDisplayString()}");
                var disposeResult = PurityAnalysisEngine.DeterminePurityRecursiveInternal(
                    disposeMethod.OriginalDefinition, // Analyze the original definition
                    context.SemanticModel, // This might be tricky if Dispose is in different assembly/model? Assume same for now.
                    context.EnforcePureAttributeSymbol,
                    context.AllowSynchronizationAttributeSymbol,
                    context.VisitedMethods, // *** CORRECTED PROPERTY NAME ***
                    context.PurityCache // *** Pass the cache ***
                );

                if (!disposeResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Implicit Dispose() call on '{local.Name}' ({disposeMethod.Name}) is IMPURE.");
                    // Report impurity at the using statement/declaration level
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(impureSyntaxNode ?? operation.Syntax);
                }
                else
                {
                    // Dispose method analyzed as Pure by DeterminePurityRecursiveInternal.
                    PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Implicit Dispose() call on '{local.Name}' ({disposeMethod.Name}) was analyzed as Pure.");
                    // Continue loop to check other locals if any
                }
            }

            // If resource, body (if applicable), and all Dispose calls are pure, the using operation is pure.
            PurityAnalysisEngine.LogDebug($"UsingStatementPurityRule: Resource, Body (if applicable), and Dispose() calls are all pure. Result: Pure.");
            return PurityAnalysisEngine.PurityAnalysisResult.Pure;
        }

        private List<ILocalSymbol> FindDeclaredLocals(IOperation? resourceOperation)
        {
            var locals = new List<ILocalSymbol>();
            if (resourceOperation is IVariableDeclarationGroupOperation declarationGroup)
            {
                foreach (var declaration in declarationGroup.Declarations)
                {
                    foreach (var declarator in declaration.Declarators)
                    {
                        locals.Add(declarator.Symbol);
                    }
                }
            }
            else if (resourceOperation is IVariableDeclaratorOperation declaratorOperation) // Single declarator case?
            {
                locals.Add(declaratorOperation.Symbol);
            }
            else if (resourceOperation is ILocalReferenceOperation localRef) // Using existing variable? (Less common)
            {
                // This rule primarily targets implicit dispose from declarations,
                // but if using targets an existing local, we might want to check its Dispose too.
                // For now, focus on declared resources.
                PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Resource is a reference to existing local '{localRef.Local.Name}'. Skipping implicit Dispose check within this rule (might be handled elsewhere).");

            }
            // Handle other cases like expression resources if necessary

            return locals;
        }


        private bool ImplementsIDisposable(ITypeSymbol typeSymbol, Compilation compilation)
        {
            INamedTypeSymbol? disposableInterface = compilation.GetTypeByMetadataName("System.IDisposable");
            if (disposableInterface == null)
            {
                PurityAnalysisEngine.LogDebug($" Error: Could not find System.IDisposable in compilation.");
                return false; // Cannot check
            }

            // Check if the type itself is IDisposable or implements it
            return typeSymbol.Equals(disposableInterface, SymbolEqualityComparer.Default) ||
                  typeSymbol.AllInterfaces.Contains(disposableInterface, SymbolEqualityComparer.Default);

        }

        private IMethodSymbol? FindDisposeMethod(ITypeSymbol typeSymbol, Compilation compilation)
        {
            INamedTypeSymbol? disposableInterface = compilation.GetTypeByMetadataName("System.IDisposable");
            if (disposableInterface == null) return null;

            // Find the Dispose method from the IDisposable interface itself
            IMethodSymbol? interfaceDisposeMethod = disposableInterface.GetMembers("Dispose").OfType<IMethodSymbol>().FirstOrDefault();
            if (interfaceDisposeMethod == null) return null; // Should not happen

            // Find the implementation of the Dispose method in the concrete type
            return typeSymbol.FindImplementationForInterfaceMember(interfaceDisposeMethod) as IMethodSymbol;
        }
    }
}