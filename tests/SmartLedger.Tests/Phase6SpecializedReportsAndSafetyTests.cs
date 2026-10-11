using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using Xunit;

namespace SmartLedger.Tests;

public sealed class Phase6SpecializedReportsAndSafetyTests
{
    [Fact]
    public void AccountStatement_CalculatesOpeningBalanceFromMovementsBeforePeriod()
    {
        var person = Guid.NewGuid();
        var user = Guid.NewGuid();
        var start = DateTimeOffset.Parse("2026-03-01T00:00:00Z");
        var operations = new[]
        {
            new LedgerOperation(Guid.NewGuid(), person, OperationType.DebtOnUs, 150m, "opening debit", start.AddDays(-3), user),
            new LedgerOperation(Guid.NewGuid(), person, OperationType.Receive, 40m, "opening receipt", start.AddDays(-1), user),
            new LedgerOperation(Guid.NewGuid(), person, OperationType.Sale, 25m, "period sale", start.AddDays(1), user)
        };

        var result = FinancialReportCalculations.CalculateAccountStatement(operations, start, start.AddDays(5));

        Assert.Equal(110m, result.OpeningBalance);
        Assert.Equal(135m, result.ClosingBalance);
        Assert.Single(result.Movements);
    }

    [Fact]
    public void AccountStatement_SortsMovementsChronologicallyAndCalculatesRunningBalances()
    {
        var person = Guid.NewGuid();
        var user = Guid.NewGuid();
        var start = DateTimeOffset.Parse("2026-03-01T00:00:00Z");
        var later = new LedgerOperation(Guid.NewGuid(), person, OperationType.Sale, 70m, "later", start.AddHours(2), user);
        var earlier = new LedgerOperation(Guid.NewGuid(), person, OperationType.Receive, 20m, "earlier", start.AddHours(1), user);

        var result = FinancialReportCalculations.CalculateAccountStatement(new[] { later, earlier }, start, start.AddDays(1));

        Assert.Collection(result.Movements,
            first => { Assert.Equal("earlier", first.Note); Assert.Equal(-20m, first.RunningBalance); },
            second => { Assert.Equal("later", second.Note); Assert.Equal(50m, second.RunningBalance); });
        Assert.Equal(50m, result.ClosingBalance);
    }

    [Fact]
    public void AccountStatement_ExcludesMovementsOutsideSelectedPeriod()
    {
        var person = Guid.NewGuid();
        var user = Guid.NewGuid();
        var start = DateTimeOffset.Parse("2026-03-01T00:00:00Z");
        var operations = new[]
        {
            new LedgerOperation(Guid.NewGuid(), person, OperationType.DebtOnUs, 10m, "before", start.AddTicks(-1), user),
            new LedgerOperation(Guid.NewGuid(), person, OperationType.Sale, 30m, "inside", start.AddHours(1), user),
            new LedgerOperation(Guid.NewGuid(), person, OperationType.Sale, 99m, "after", start.AddDays(2), user)
        };

        var result = FinancialReportCalculations.CalculateAccountStatement(operations, start, start.AddDays(1));

        Assert.Equal(10m, result.OpeningBalance);
        Assert.Single(result.Movements);
        Assert.Equal(40m, result.ClosingBalance);
    }

    [Fact]
    public void InventoryValuation_CalculatesPhysicalQuantityCostRetailAndGrossProfit()
    {
        var now = DateTimeOffset.Parse("2026-03-01T00:00:00Z");
        var products = new[]
        {
            new Product(Guid.NewGuid(), "A", "", "General", "Box", "Unit", 10m, 2m, 3m, 12m, 1m, now),
            new Product(Guid.NewGuid(), "B", "", "General", "Pack", "Unit", 5m, 4m, 7m, 3m, 1m, now)
        };

        var result = FinancialReportCalculations.CalculateInventoryValuation(products);

        Assert.Equal(15m, result.PhysicalQuantity);
        Assert.Equal(36m, result.CostValue);
        Assert.Equal(57m, result.RetailValue);
        Assert.Equal(21m, result.ExpectedGrossProfit);
    }

    [Fact]
    public void AccountDeletionGuardrail_BlocksHistoricalTransactionsEvenWhenBalanceIsZero()
    {
        var allowed = FinancialReportCalculations.CanDeleteAccount(2, 0m, out var reason);

        Assert.False(allowed);
        Assert.Contains("historical ledger", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0, 125.5)]
    [InlineData(0, -125.5)]
    public void AccountDeletionGuardrail_BlocksNonZeroBalance(int operationCount, decimal balance)
    {
        var allowed = FinancialReportCalculations.CanDeleteAccount(operationCount, balance, out var reason);

        Assert.False(allowed);
        Assert.Contains("non-zero balance", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AccountDeletionGuardrail_AllowsEmptySettledAccount()
    {
        Assert.True(FinancialReportCalculations.CanDeleteAccount(0, 0m, out var reason));
        Assert.Equal(string.Empty, reason);
    }
}
