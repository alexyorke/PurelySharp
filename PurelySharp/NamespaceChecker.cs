using Microsoft.CodeAnalysis;
using System.Linq;

namespace PurelySharp
{
    public static class NamespaceChecker
    {
        public static bool IsInNamespace(ISymbol symbol, string ns)
        {
            var current = symbol.ContainingNamespace;
            while (current != null)
            {
                if (current.ToDisplayString() == ns)
                    return true;
                current = current.ContainingNamespace;
            }
            return false;
        }

        public static bool IsInImpureNamespace(ITypeSymbol type)
        {
            var impureNamespaces = new[] {
                "System.IO",
                "System.Net",
                "System.Data",
                "System.Threading",
                "System.Threading.Tasks",
                "System.Diagnostics",
                "System.Security.Cryptography",
                "System.Runtime.InteropServices"
            };

            return impureNamespaces.Any(ns => IsInNamespace(type, ns));
        }
    }
}