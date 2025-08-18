using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace PurelySharp.Analyzer.Engine.Rules
{

    internal class UsingStatementPurityRule : IPurityRule
    {

        public IEnumerable<OperationKind> ApplicableOperationKinds => ImmutableArray.Create(OperationKind.Using, OperationKind.UsingDeclaration);

        public PurityAnalysisEngine.PurityAnalysisResult CheckPurity(IOperation operation, PurityAnalysisContext context, PurityAnalysisEngine.PurityAnalysisState currentState)
        {
            SyntaxNode? impureSyntaxNode = null;
            IOperation? resourceOperation = null;
            IOperation? bodyOperation = null;

            if (operation is IUsingOperation usingOperation)
            {
                resourceOperation = usingOperation.Resources;
                bodyOperation = usingOperation.Body;
                impureSyntaxNode = usingOperation.Syntax;
                PurityAnalysisEngine.LogDebug($"UsingStatementPurityRule: Analyzing Using Statement");
            }
            else if (operation is IUsingDeclarationOperation usingDeclarationOperation)
            {
                resourceOperation = usingDeclarationOperation.DeclarationGroup;
                impureSyntaxNode = usingDeclarationOperation.Syntax;
                PurityAnalysisEngine.LogDebug($"UsingStatementPurityRule: Analyzing Using Declaration");
            }
            else
            {
                PurityAnalysisEngine.LogDebug($"UsingStatementPurityRule: Unexpected operation kind {operation.Kind}. Assuming pure.");
                return PurityAnalysisEngine.PurityAnalysisResult.Pure;
            }


            if (resourceOperation != null)
            {
                PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Checking Resource operation {resourceOperation.Kind}");
                PurityAnalysisEngine.PurityAnalysisResult resourceResult = PurityAnalysisEngine.PurityAnalysisResult.Pure;

                if (resourceOperation is IVariableDeclarationGroupOperation declarationGroup)
                {
                    PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Resource is VariableDeclarationGroup. Checking initializers.");
                    foreach (var declaration in declarationGroup.Declarations)
                    {
                        foreach (var declarator in declaration.Declarators)
                        {
                            var initVal = declarator.Initializer?.Value;
                            if (initVal != null)
                            {
                                PurityAnalysisEngine.LogDebug($"  UsingStatementPurityRule: Checking initializer for {declarator.Symbol.Name}: {initVal.Syntax}");
                                var initializerResult = PurityAnalysisEngine.CheckSingleOperation(initVal, context, currentState);
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
                        if (!resourceResult.IsPure) break;
                    }
                }
                else if (resourceOperation is IVariableDeclarationOperation variableDeclaration)
                {
                    PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Resource is VariableDeclaration. Checking initializers.");
                    foreach (var declarator in variableDeclaration.Declarators)
                    {
                        var initVal = declarator.Initializer?.Value;
                        if (initVal != null)
                        {
                            PurityAnalysisEngine.LogDebug($"  UsingStatementPurityRule: Checking initializer for {declarator.Symbol.Name}: {initVal.Syntax}");
                            var initializerResult = PurityAnalysisEngine.CheckSingleOperation(initVal, context, currentState);
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
                else
                {
                    PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Resource is an expression {resourceOperation.Kind}. Checking expression directly.");
                    resourceResult = PurityAnalysisEngine.CheckSingleOperation(resourceOperation, context, currentState);
                }


                if (!resourceResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Resource operation is IMPURE.");
                    return resourceResult;
                }
                PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Resource operation is Pure.");
            }


            if (bodyOperation != null)
            {
                PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Checking Body operation {bodyOperation.Kind}");
                var bodyResult = PurityAnalysisEngine.CheckSingleOperation(bodyOperation, context, currentState);
                if (!bodyResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Body operation is IMPURE.");
                    return bodyResult;
                }
                PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Body operation is Pure.");
            }



            List<ILocalSymbol> declaredLocals = FindDeclaredLocals(resourceOperation);

            foreach (var local in declaredLocals)
            {
                if (local.Type == null || !ImplementsIDisposable(local.Type, context.SemanticModel.Compilation))
                {
                    PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Local '{local.Name}' is not IDisposable. Skipping Dispose check.");
                    continue;
                }

                PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Checking implicit Dispose() for local '{local.Name}' of type {local.Type.Name}");


                IMethodSymbol? disposeMethod = FindDisposeMethod(local.Type, context.SemanticModel.Compilation);

                if (disposeMethod == null)
                {

                    PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Could not find Dispose method for type {local.Type.Name}. Assuming impure.");
                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(impureSyntaxNode ?? operation.Syntax);
                }



                PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Checking callee purity of {disposeMethod.ToDisplayString()}");
                var disposeResult = PurityAnalysisEngine.GetCalleePurity(disposeMethod, context);

                if (!disposeResult.IsPure)
                {
                    PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Implicit Dispose() call on '{local.Name}' ({disposeMethod.Name}) is IMPURE.");

                    return PurityAnalysisEngine.PurityAnalysisResult.Impure(impureSyntaxNode ?? operation.Syntax);
                }
                else
                {

                    PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Implicit Dispose() call on '{local.Name}' ({disposeMethod.Name}) was analyzed as Pure.");

                }
            }


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
            else if (resourceOperation is IVariableDeclaratorOperation declaratorOperation)
            {
                locals.Add(declaratorOperation.Symbol);
            }
            else if (resourceOperation is ILocalReferenceOperation localRef)
            {



                PurityAnalysisEngine.LogDebug($" UsingStatementPurityRule: Resource is a reference to existing local '{localRef.Local.Name}'. Skipping implicit Dispose check within this rule (might be handled elsewhere).");

            }


            return locals;
        }


        private bool ImplementsIDisposable(ITypeSymbol typeSymbol, Compilation compilation)
        {
            INamedTypeSymbol? disposableInterface = compilation.GetTypeByMetadataName("System.IDisposable");
            if (disposableInterface == null)
            {
                PurityAnalysisEngine.LogDebug($" Error: Could not find System.IDisposable in compilation.");
                return false;
            }


            return typeSymbol.Equals(disposableInterface, SymbolEqualityComparer.Default) ||
                  typeSymbol.AllInterfaces.Contains(disposableInterface, SymbolEqualityComparer.Default);

        }

        private IMethodSymbol? FindDisposeMethod(ITypeSymbol typeSymbol, Compilation compilation)
        {
            INamedTypeSymbol? disposableInterface = compilation.GetTypeByMetadataName("System.IDisposable");
            if (disposableInterface == null) return null;


            IMethodSymbol? interfaceDisposeMethod = disposableInterface.GetMembers("Dispose").OfType<IMethodSymbol>().FirstOrDefault();
            if (interfaceDisposeMethod == null) return null;


            return typeSymbol.FindImplementationForInterfaceMember(interfaceDisposeMethod) as IMethodSymbol;
        }
    }
}