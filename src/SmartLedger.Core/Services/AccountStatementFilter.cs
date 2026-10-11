using SmartLedger.Core.Models;

namespace SmartLedger.Core.Services;

public enum AccountStatementKindFilter { All, Invoices, Vouchers, Returns }

public static class AccountStatementFilter
{
    public static AccountStatementCalculation Apply(IEnumerable<LedgerOperation> operations, DateTimeOffset fromInclusive,
        DateTimeOffset toInclusive, AccountStatementKindFilter kind = AccountStatementKindFilter.All, string? search = null)
    {
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentOutOfRangeException.ThrowIfLessThan(toInclusive, fromInclusive);
        var all = operations.OrderBy(x => x.OccurredAt).ThenBy(x => x.Id).ToArray();
        var calculation = FinancialReportCalculations.CalculateAccountStatement(all, fromInclusive, toInclusive);
        var query = search?.Trim();
        var visible = calculation.Movements.Where(x => MatchesKind(x.Type, kind));
        if (!string.IsNullOrEmpty(query))
            visible = visible.Where(x => (x.ReferenceNumber?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || x.Note.Contains(query, StringComparison.OrdinalIgnoreCase)
                || x.TypeLabel.Contains(query, StringComparison.OrdinalIgnoreCase));
        return calculation with { Movements = visible.ToArray() };
    }

    public static bool MatchesKind(OperationType type, AccountStatementKindFilter kind) => kind switch
    {
        AccountStatementKindFilter.All => true,
        AccountStatementKindFilter.Invoices => type is OperationType.Sale or OperationType.Purchase,
        AccountStatementKindFilter.Vouchers => type is OperationType.Receive or OperationType.Payment,
        AccountStatementKindFilter.Returns => type == OperationType.Return,
        _ => false
    };
}

