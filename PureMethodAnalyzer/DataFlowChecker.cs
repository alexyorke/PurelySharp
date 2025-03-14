using Microsoft.CodeAnalysis;

namespace PureMethodAnalyzer
{
    public static class DataFlowChecker
    {
        public static bool HasImpureCaptures(DataFlowAnalysis dataFlowAnalysis)
        {
            foreach (var captured in dataFlowAnalysis.CapturedInside)
            {
                if (captured is IFieldSymbol field && !field.IsReadOnly && !field.IsConst)
                    return true;
                if (captured is IPropertySymbol prop && prop.SetMethod != null && !prop.SetMethod.IsInitOnly)
                    return true;
            }
            return false;
        }
    }
}