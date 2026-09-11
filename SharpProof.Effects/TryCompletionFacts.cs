namespace SharpProof.Effects;

internal static class TryCompletionFacts
{
    internal static bool CanComplete(
        ITryOperation @try,
        Func<IOperation?, bool> canComplete)
    {
        if (@try.Finally != null && !canComplete(@try.Finally))
        {
            return false;
        }

        return canComplete(@try.Body) ||
            @try.Catches.Any(catchClause =>
                (catchClause.Filter == null ||
                 canComplete(catchClause.Filter)) &&
                canComplete(catchClause.Handler));
    }
}
