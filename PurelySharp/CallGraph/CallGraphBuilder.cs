using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace PurelySharp.CallGraph
{
    public class CallGraphBuilder
    {
        private readonly Dictionary<IMethodSymbol, MethodNode> _methodNodes;
        private readonly SemanticModel _semanticModel;

        public CallGraphBuilder(SemanticModel semanticModel)
        {
            _methodNodes = new Dictionary<IMethodSymbol, MethodNode>();
            _semanticModel = semanticModel;
        }

        public IReadOnlyDictionary<IMethodSymbol, MethodNode> MethodNodes => _methodNodes;

        public void AnalyzeMethod(MethodDeclarationSyntax methodDeclaration)
        {
            var methodSymbol = _semanticModel.GetDeclaredSymbol(methodDeclaration);
            if (methodSymbol == null) return;

            // Create or get the node for this method
            var currentNode = GetOrCreateMethodNode(methodSymbol, methodDeclaration);

            // Find all method invocations within this method
            var methodInvocations = methodDeclaration
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>();

            foreach (var invocation in methodInvocations)
            {
                var symbol = _semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
                if (symbol != null)
                {
                    var calledNode = GetOrCreateMethodNode(symbol, null);
                    currentNode.AddCalledMethod(calledNode);
                }
            }
        }

        private MethodNode GetOrCreateMethodNode(IMethodSymbol method, MethodDeclarationSyntax? syntax = null)
        {
            if (!_methodNodes.TryGetValue(method, out var node))
            {
                node = new MethodNode(method, syntax);
                _methodNodes[method] = node;
            }
            return node;
        }

        public IEnumerable<MethodNode> GetImpureMethods()
        {
            return _methodNodes.Values.Where(node => !node.IsPure);
        }

        public IEnumerable<MethodNode> GetMethodsAffectedByImpurity()
        {
            var affectedMethods = new HashSet<MethodNode>();
            var impureMethods = GetImpureMethods();

            foreach (var impureMethod in impureMethods)
            {
                AddAffectedMethods(impureMethod, affectedMethods);
            }

            return affectedMethods;
        }

        private void AddAffectedMethods(MethodNode method, HashSet<MethodNode> affectedMethods)
        {
            foreach (var caller in method.CalledBy)
            {
                if (affectedMethods.Add(caller))
                {
                    AddAffectedMethods(caller, affectedMethods);
                }
            }
        }
    }
}