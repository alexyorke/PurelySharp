using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace PurelySharp.CallGraph
{
    public class MethodNode
    {
        public IMethodSymbol Method { get; }
        public MethodDeclarationSyntax Syntax { get; }
        public bool IsPure { get; set; }
        public HashSet<MethodNode> CalledMethods { get; }
        public HashSet<MethodNode> CalledBy { get; }

        public MethodNode(IMethodSymbol method, MethodDeclarationSyntax syntax)
        {
            Method = method;
            Syntax = syntax;
            IsPure = true; // Default to pure until proven otherwise
            CalledMethods = new HashSet<MethodNode>();
            CalledBy = new HashSet<MethodNode>();
        }

        public void AddCalledMethod(MethodNode method)
        {
            CalledMethods.Add(method);
            method.CalledBy.Add(this);
        }

        public override bool Equals(object obj)
        {
            if (obj is MethodNode other)
            {
                return Method.Equals(other.Method);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return Method.GetHashCode();
        }
    }
}