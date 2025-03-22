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
                if (captured is IPropertySymbol prop && prop.SetMethod != null && !prop.SetMethod.IsInitOnly)
                    return true;
            }
            return false;
        }
    }
}