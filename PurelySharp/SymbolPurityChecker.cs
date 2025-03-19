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
                // Get the syntax reference
                var syntaxRef = namedType.DeclaringSyntaxReferences.FirstOrDefault();
                if (syntaxRef != null)
                {
                    var syntax = syntaxRef.GetSyntax();
                    // Check if it's declared as a record
                    return syntax is RecordDeclarationSyntax;
                }
            }
            return false;
        }

        public static bool IsPureSymbol(ISymbol symbol)
        {
            switch (symbol)
            {
                case IParameterSymbol parameter:
                    // Ref/out parameters are impure
                    return parameter.RefKind == RefKind.None;

                case ILocalSymbol local:
                    return true;

                case IPropertySymbol property:
                    // Only allow get-only properties or auto-implemented properties
                    // For records, allow init-only properties
                    var isRecord = IsRecordType(property.ContainingType);
                    return property.IsReadOnly ||
                           (property.GetMethod != null && (property.SetMethod == null || property.SetMethod.IsInitOnly)) ||
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