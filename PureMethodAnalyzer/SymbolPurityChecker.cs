using Microsoft.CodeAnalysis;

namespace PureMethodAnalyzer
{
    public static class SymbolPurityChecker
    {
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
                    return property.IsReadOnly ||
                           (property.GetMethod != null && (property.SetMethod == null || property.SetMethod.IsInitOnly));

                case IFieldSymbol field:
                    // Allow only readonly fields and static constants
                    // Static fields that are not const are considered impure
                    return (field.IsReadOnly && !field.IsStatic) || (field.IsStatic && field.IsConst);

                case IMethodSymbol method:
                    // Allow pure methods
                    return MethodPurityChecker.IsKnownPureMethod(method);

                default:
                    return false;
            }
        }
    }
}