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

        private static bool IsValueType(ITypeSymbol? type)
        {
            return type?.IsValueType == true;
        }

        private static bool HasPureConstraints(ITypeSymbol? type)
        {
            if (type is not ITypeParameterSymbol typeParam)
                return false;

            // Value type constraints are considered pure
            if (typeParam.HasValueTypeConstraint)
                return true;

            // Check if all constraint types are pure
            foreach (var constraint in typeParam.ConstraintTypes)
            {
                if (!IsPureSymbol(constraint))
                    return false;
            }

            return true;
        }

        public static bool IsPureSymbol(ISymbol symbol)
        {
            switch (symbol)
            {
                case IParameterSymbol parameter:
                    // Ref/out parameters are impure
                    if (parameter.RefKind != RefKind.None)
                        return false;

                    // Check if the parameter type is pure
                    return IsValueType(parameter.Type) || HasPureConstraints(parameter.Type);

                case ILocalSymbol local:
                    return true;

                case IPropertySymbol property:
                    // Only allow get-only properties or auto-implemented properties
                    // For records, allow init-only properties
                    var isRecord = IsRecordType(property.ContainingType);
                    var isInterfaceProperty = property.ContainingType.TypeKind == TypeKind.Interface;

                    // Interface properties with only getters are pure
                    if (isInterfaceProperty && property.GetMethod != null && property.SetMethod == null)
                        return true;

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
                    // Interface methods with pure constraints are considered pure
                    if (method.ContainingType.TypeKind == TypeKind.Interface)
                    {
                        // Check if all type parameters have pure constraints
                        if (method.TypeParameters.All(tp => HasPureConstraints(tp)))
                            return true;

                        // Check if it's a known pure interface method
                        if (method.Name == "Convert" || method.Name == "CompareTo" || method.Name == "Equals")
                            return true;

                        // Check if it's a getter
                        if (method.MethodKind == MethodKind.PropertyGet)
                            return true;
                    }

                    // Allow pure methods
                    return MethodPurityChecker.IsKnownPureMethod(method);

                case ITypeSymbol type:
                    // Value types are considered pure
                    if (IsValueType(type))
                        return true;

                    // Records are considered pure by default
                    if (IsRecordType(type))
                        return true;

                    // Interfaces with pure constraints are considered pure
                    if (type.TypeKind == TypeKind.Interface)
                        return HasPureConstraints(type);

                    // Generic type parameters with pure constraints are considered pure
                    if (type is ITypeParameterSymbol)
                        return HasPureConstraints(type);

                    return false;

                default:
                    return false;
            }
        }
    }
}