using Microsoft.CodeAnalysis;

namespace PurelySharp
{
    public static class DataFlowChecker
    {
        public static bool HasImpureCaptures(DataFlowAnalysis dataFlowAnalysis)
        {
            foreach (var captured in dataFlowAnalysis.CapturedInside)
            {
                if (captured is IFieldSymbol field &&
                    (!field.IsReadOnly && !field.IsConst || field.IsVolatile))
                    return true;

                if (captured is IPropertySymbol prop)
                {
                    // Handle indexers specially - they're always considered pure for symbol capture
                    // (actual read/write operations are handled in ExpressionPurityChecker)
                    if (prop.IsIndexer)
                        continue;

                    // A regular property is impure if it has a setter that is not init-only
                    if (prop.SetMethod != null && !prop.SetMethod.IsInitOnly)
                        return true;
                }
            }
            return false;
        }
    }
}