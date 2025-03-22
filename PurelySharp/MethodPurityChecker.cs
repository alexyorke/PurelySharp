using Microsoft.CodeAnalysis;
using System.Linq;

namespace PurelySharp
{
    public static class MethodPurityChecker
    {
        public static bool IsKnownPureMethod(IMethodSymbol method)
        {
            if (method == null) return false;

            // Check if method has ref or out parameters
            foreach (var parameter in method.Parameters)
            {
                if (parameter.RefKind == RefKind.Ref || parameter.RefKind == RefKind.Out)
                    return false; // Methods with ref or out parameters are not pure
            }

            // Check if it's a LINQ method
            if (NamespaceChecker.IsInNamespace(method, "System.Linq") &&
                method.ContainingType.Name == "Enumerable")
            {
                return true;
            }

            // Check if it's a Math method
            if (NamespaceChecker.IsInNamespace(method, "System") &&
                method.ContainingType.Name == "Math")
            {
                return true;
            }

            // Check if it's a string method
            if (method.ContainingType.SpecialType == SpecialType.System_String)
            {
                return true; // All string methods are pure
            }

            // Check if it's a pure collection method
            if (IsPureCollectionMethod(method))
            {
                return true;
            }

            // Check if it's a tuple method
            if (method.ContainingType.IsTupleType)
            {
                return true; // All tuple methods are pure
            }

            // Check if it's a conversion method
            if (method.MethodKind == MethodKind.Conversion ||
                method.Name == "Parse" || method.Name == "TryParse" ||
                method.Name == "Convert" || method.Name == "CompareTo" || method.Name == "Equals")
            {
                return true;
            }

            // Check for known impure types
            var impureNamespaces = new[] { "System.IO", "System.Net", "System.Data" };
            foreach (var ns in impureNamespaces)
            {
                if (NamespaceChecker.IsInNamespace(method, ns))
                    return false;
            }

            // Check for known impure types
            var impureTypes = new[] {
                "Random", "DateTime", "File", "Console", "Process",
                "Task", "Thread", "Timer", "WebClient", "HttpClient"
            };
            if (impureTypes.Contains(method.ContainingType.Name))
                return false;

            // Check if it's marked with [EnforcePure]
            if (method.GetAttributes().Any(attr => attr.AttributeClass?.Name == "EnforcePureAttribute"))
                return true;

            return false;
        }

        public static bool IsPureCollectionMethod(IMethodSymbol method)
        {
            // Pure collection methods that don't modify state
            var pureCollectionMethods = new[] {
                "Count", "Contains", "ElementAt", "First", "FirstOrDefault",
                "Last", "LastOrDefault", "Single", "SingleOrDefault",
                "Any", "All", "ToArray", "ToList", "ToDictionary",
                "AsEnumerable", "AsQueryable", "GetEnumerator", "GetHashCode",
                "Equals", "ToString", "CompareTo", "Clone", "GetType",
                "Select", "Where", "OrderBy", "OrderByDescending",
                "ThenBy", "ThenByDescending", "GroupBy", "Join",
                "Skip", "Take", "Reverse", "Concat", "Union",
                "Intersect", "Except", "Distinct", "Count", "Sum",
                "Average", "Min", "Max", "Aggregate"
            };

            return pureCollectionMethods.Contains(method.Name);
        }
    }
}