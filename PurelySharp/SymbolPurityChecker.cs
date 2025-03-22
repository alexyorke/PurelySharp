using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace PurelySharp
{
    public static class SymbolPurityChecker
    {
        private static bool IsRecordType(ITypeSymbol? type)
        {
            if (type == null) return false;

            // Check if it's a named type
            if (type is INamedTypeSymbol namedType)
            {
                // C# 9 and later has built-in record detection
                if (namedType.IsRecord)
                    return true;

                // Fallback for older Roslyn versions
                var syntaxRef = namedType.DeclaringSyntaxReferences.FirstOrDefault();
                if (syntaxRef != null)
                {
                    var syntax = syntaxRef.GetSyntax();
                    // Check if it's declared as a record (including record struct)
                    return syntax is RecordDeclarationSyntax ||
                           syntax.ToString().Contains("record struct");
                }
            }
            return false;
        }

        public static bool IsPureSymbol(ISymbol symbol)
        {
            switch (symbol)
            {
                case IParameterSymbol parameter:
                    // Ref/out parameters are impure, but 'in' (readonly ref) parameters are pure
                    return parameter.RefKind == RefKind.None ||
                           parameter.RefKind == RefKind.In;

                case ILocalSymbol local:
                    return true;

                case IPropertySymbol property:
                    // Only allow get-only properties or auto-implemented properties
                    // For records, allow init-only properties
                    var isRecord = IsRecordType(property.ContainingType);

                    // Handle both regular properties and indexers
                    var isIndexer = property.IsIndexer;

                    if (isIndexer)
                    {
                        // For indexers - they're considered pure for the purpose of symbol access
                        // (actual purity of operations is handled in ExpressionPurityChecker)
                        return true;
                    }

                    return property.IsReadOnly ||
                           // For regular properties
                           (property.GetMethod != null && (property.SetMethod == null || property.SetMethod.IsInitOnly)) ||
                           // For record properties
                           (isRecord && property.GetMethod != null && property.SetMethod?.IsInitOnly == true);

                case IFieldSymbol field:
                    // Allow only readonly fields and static constants
                    // Static fields that are not const are considered impure
                    // For records, allow backing fields of init-only properties
                    var isRecordField = IsRecordType(field.ContainingType);
                    var isBackingField = field.AssociatedSymbol is IPropertySymbol prop && prop.SetMethod?.IsInitOnly == true;
                    return (field.IsReadOnly && !field.IsStatic) ||
                           (field.IsStatic && field.IsConst) ||
                           (isRecordField && isBackingField);

                case IMethodSymbol method:
                    // Allow pure methods
                    return MethodPurityChecker.IsKnownPureMethod(method);

                case ITypeSymbol type:
                    // Records are considered pure by default
                    return IsRecordType(type);

                default:
                    return false;
            }
        }
    }
}