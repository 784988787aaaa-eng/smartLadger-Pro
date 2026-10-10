namespace SmartLedger.Core.Models;

public sealed record LedgerBalanceEntry(LedgerOperation Operation, decimal RunningBalance);

public static class LedgerBalanceCalculator
{
    public static decimal SignedEffect(LedgerOperation operation) => operation.Type switch
    {
        OperationType.DebtOnUs or OperationType.Sale or OperationType.Payment or OperationType.Settlement => operation.Amount,
        OperationType.Receive or OperationType.Return or OperationType.DebtToUs or OperationType.Purchase => -operation.Amount,
        _ => 0m
    };

    public static IReadOnlyList<LedgerBalanceEntry> CalculateRunningBalances(IEnumerable<LedgerOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var running = 0m;
        var entries = new List<LedgerBalanceEntry>();
        foreach (var operation in operations.OrderBy(x => x.OccurredAt).ThenBy(x => x.Id))
        {
            running = decimal.Round(running + SignedEffect(operation), 2, MidpointRounding.AwayFromZero);
            entries.Add(new LedgerBalanceEntry(operation, running));
        }
        return entries;
    }

    public static decimal CalculateNetBalance(IEnumerable<LedgerOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        return decimal.Round(operations.Sum(SignedEffect), 2, MidpointRounding.AwayFromZero);
    }
}
