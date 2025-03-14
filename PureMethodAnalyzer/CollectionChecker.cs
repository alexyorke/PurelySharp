using Microsoft.CodeAnalysis;
using System.Linq;

namespace PureMethodAnalyzer
{
    public static class CollectionChecker
    {
        public static bool IsModifiableCollectionType(ITypeSymbol type)
        {
            var modifiableCollections = new[] {
                "List", "Dictionary", "HashSet", "Queue", "Stack", "LinkedList",
                "SortedList", "SortedDictionary", "SortedSet", "Collection"
            };

            // Allow immutable collections
            if (type.ContainingNamespace?.Name == "Immutable" &&
                NamespaceChecker.IsInNamespace(type, "System.Collections.Immutable"))
                return false;

            // Allow read-only collections
            if (type.Name.StartsWith("IReadOnly"))
                return false;

            return modifiableCollections.Contains(type.Name) ||
                   (type is INamedTypeSymbol namedType &&
                    namedType.TypeArguments.Any() &&
                    modifiableCollections.Contains(namedType.ConstructedFrom.Name));
        }
    }
}