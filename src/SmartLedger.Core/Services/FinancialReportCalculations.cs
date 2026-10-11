using SmartLedger.Core.Models;

namespace SmartLedger.Core.Services;

public sealed record AccountStatementCalculation(decimal OpeningBalance, IReadOnlyList<LedgerOperation> Movements, decimal ClosingBalance);
public sealed record InventoryValuationCalculation(decimal PhysicalQuantity, decimal CostValue, decimal RetailValue, decimal ExpectedGrossProfit);

/// <summary>Deterministic financial calculations shared by report presentation and automated tests.</summary>
public static class FinancialReportCalculations
{
    public static decimal SignedLedgerEffect(LedgerOperation operation) => LedgerBalanceCalculator.SignedEffect(operation);

    public static AccountStatementCalculation CalculateAccountStatement(IEnumerable<LedgerOperation> operations, DateTimeOffset fromInclusive, DateTimeOffset toInclusive)
    {
        ArgumentNullException.ThrowIfNull(operations);
        if (toInclusive < fromInclusive) throw new ArgumentOutOfRangeException(nameof(toInclusive), "End date must not precede start date.");
        var ordered = operations.OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).ToArray();
        var opening = ordered.Where(x => x.OccurredAt < fromInclusive).Sum(SignedLedgerEffect);
        var balance = opening;
        var rows = new List<LedgerOperation>();
        foreach (var operation in ordered.Where(x => x.OccurredAt >= fromInclusive && x.OccurredAt <= toInclusive))
        {
            balance += SignedLedgerEffect(operation);
            rows.Add(operation with { RunningBalance = balance });
        }
        return new AccountStatementCalculation(opening, rows, balance);
    }

    public static InventoryValuationCalculation CalculateInventoryValuation(IEnumerable<Product> products)
    {
        ArgumentNullException.ThrowIfNull(products);
        var items = products.ToArray();
        var quantity = items.Sum(x => x.StockSmallUnits);
        var cost = items.Sum(x => x.StockSmallUnits * x.PurchasePrice);
        var retail = items.Sum(x => x.StockSmallUnits * x.SellingPriceMinor);
        return new InventoryValuationCalculation(quantity, cost, retail, retail - cost);
    }

    public static bool CanDeleteAccount(int historicalOperationCount, decimal currentBalance, out string reason)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(historicalOperationCount);
        if (historicalOperationCount > 0)
        {
            reason = "Account has historical ledger transactions and cannot be deleted without damaging the audit trail.";
            return false;
        }
        if (currentBalance != 0m)
        {
            reason = "Account has a non-zero balance and must be settled before deletion.";
            return false;
        }
        reason = string.Empty;
        return true;
    }
}

