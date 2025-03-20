using Microsoft.CodeAnalysis;
using System.Linq;

namespace PurelySharp
{
    public static class MethodPurityChecker
    {
        public static bool IsKnownPureMethod(IMethodSymbol method)
        {
            if (method == null) return false;

            // Check if it's marked with [EnforcePure]
            var hasEnforcePure = method.GetAttributes().Any(attr => attr.AttributeClass?.Name == "EnforcePureAttribute");

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

            // Check if it's a pure Task method
            if (method.ContainingType.Name == "Task" && NamespaceChecker.IsInNamespace(method, "System.Threading.Tasks"))
            {
                var pureMethods = new[] { "FromResult", "WhenAll", "WhenAny", "CompletedTask" };
                if (pureMethods.Contains(method.Name))
                    return true;
            }

            // Check if it's an interface method
            if (method.ContainingType.TypeKind == TypeKind.Interface)
            {
                // Interface methods with pure constraints are considered pure
                if (method.TypeParameters.All(tp => tp.HasValueTypeConstraint))
                    return true;

                // Interface methods that are getters or have no side effects are pure
                if (method.MethodKind == MethodKind.PropertyGet)
                    return true;

                if (method.Name == "Convert" || method.Name == "CompareTo" || method.Name == "Equals")
                    return true;

                // Check if all implementations of this interface method are pure
                var implementations = method.ContainingType.GetMembers().OfType<IMethodSymbol>()
                    .Where(m => m.Name == method.Name && m.Parameters.Length == method.Parameters.Length);

                if (implementations.Any() && implementations.All(impl => IsKnownPureMethod(impl)))
                    return true;
            }

            // Check for known impure namespaces
            var impureNamespaces = new[] { "System.IO", "System.Net", "System.Data" };
            foreach (var ns in impureNamespaces)
            {
                if (NamespaceChecker.IsInNamespace(method, ns))
                    return false;
            }

            // Check for known impure types
            var impureTypes = new[] {
                "Random", "DateTime", "File", "Console", "Process",
                "Thread", "Timer", "WebClient", "HttpClient"
            };
            if (impureTypes.Contains(method.ContainingType.Name))
                return false;

            // Check if it's an async method
            if (method.IsAsync && !hasEnforcePure)
                return false;

            // Check if it's an iterator method (yield return)
            if (method.ReturnType.Name.StartsWith("IEnumerable") && !method.IsAsync)
                return true;

            // Check if it's a recursive method
            if (hasEnforcePure)
            {
                // Check if the method contains any impure operations
                var containsImpureOperations = method.DeclaringSyntaxReferences
                    .SelectMany(sr => sr.GetSyntax().DescendantNodes())
                    .OfType<IMethodSymbol>()
                    .Any(m => !IsKnownPureMethod(m));

                return !containsImpureOperations;
            }

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