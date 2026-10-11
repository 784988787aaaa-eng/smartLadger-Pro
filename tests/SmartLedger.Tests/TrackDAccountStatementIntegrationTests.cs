using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using Xunit;
#pragma warning disable CA1861

namespace SmartLedger.Tests;

public sealed class TrackDAccountStatementIntegrationTests
{
    [Fact]
    public void CustomerLedger_OpeningSalePartialReceiptAndReturn_ReconcilesEveryRunningBalance()
    {
        var person = Guid.NewGuid();
        var user = Guid.NewGuid();
        var day1 = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        var operations = new[]
        {
            new LedgerOperation(Guid.NewGuid(), person, OperationType.DebtOnUs, 100m, "opening", day1, user),
            new LedgerOperation(Guid.NewGuid(), person, OperationType.Sale, 80m, "invoice sale", day1.AddDays(1), user),
            new LedgerOperation(Guid.NewGuid(), person, OperationType.Receive, 30m, "partial receipt", day1.AddDays(2), user),
            new LedgerOperation(Guid.NewGuid(), person, OperationType.Return, 20m, "sales return", day1.AddDays(3), user)
        };

        var rows = LedgerBalanceCalculator.CalculateRunningBalances(operations);
        Assert.Equal(new[] { 100m, 180m, 150m, 130m }, rows.Select(x => x.RunningBalance));
        Assert.Equal(130m, LedgerBalanceCalculator.CalculateNetBalance(operations));
        Assert.Equal(100m, FinancialReportCalculations.SignedLedgerEffect(operations[0]));
        Assert.Equal(-30m, FinancialReportCalculations.SignedLedgerEffect(operations[2]));
        Assert.Equal(-20m, FinancialReportCalculations.SignedLedgerEffect(operations[3]));
    }

    [Fact]
    public void DateScopedStatement_UsesAllEarlierMovementsAsOpeningAndReconcilesPeriod()
    {
        var person = Guid.NewGuid();
        var user = Guid.NewGuid();
        var start = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var operations = new[]
        {
            new LedgerOperation(Guid.NewGuid(), person, OperationType.DebtOnUs, 100m, "opening", start.AddHours(1), user),
            new LedgerOperation(Guid.NewGuid(), person, OperationType.Sale, 80m, "invoice", start.AddDays(1), user),
            new LedgerOperation(Guid.NewGuid(), person, OperationType.Receive, 30m, "receipt note", start.AddDays(2), user),
            new LedgerOperation(Guid.NewGuid(), person, OperationType.Return, 20m, "return", start.AddDays(3), user)
        };

        var statement = FinancialReportCalculations.CalculateAccountStatement(operations, start.AddDays(1), start.AddDays(2).AddHours(23));
        Assert.Equal(100m, statement.OpeningBalance);
        Assert.Equal(new[] { OperationType.Sale, OperationType.Receive }, statement.Movements.Select(x => x.Type));
        Assert.Equal(new[] { 180m, 150m }, statement.Movements.Select(x => x.RunningBalance));
        Assert.Equal(150m, statement.ClosingBalance);
        Assert.Equal(130m, FinancialReportCalculations.CalculateAccountStatement(operations, start, start.AddDays(4)).ClosingBalance);
    }
    [Fact]
    public async Task ReceiptAndSupplierPayment_AffectCashAndBalancesButNeverInvoiceSalesOrPurchases()
    {
        var root = Path.Combine(Path.GetTempPath(), "SL_TRACK_D_" + Guid.NewGuid().ToString("N"));
        try
        {
            var database = new SmartLedger.Infrastructure.Data.Database(root);
            await database.InitializeAsync();
            var users = new SmartLedger.Infrastructure.Data.UserRepository(database);
            var hasher = new PasswordHasher();
            await users.CreateAdministratorAsync(new SmartLedger.Core.Models.AdminSetup("trackd_admin", hasher.Hash("Password!123"), hasher.Hash("recovery"), "hint"));
            var admin = await users.FindAsync("trackd_admin");
            Assert.NotNull(admin);
            var repo = new SmartLedger.Infrastructure.Data.AppRepository(database);
            var books = await repo.GetBooksAsync();
            var customer = await repo.CreatePersonAsync(books.Single(x => x.Kind == BookKind.Customers).Id, "Track D Customer", "", "");
            var supplier = await repo.CreatePersonAsync(books.Single(x => x.Kind == BookKind.Suppliers).Id, "Track D Supplier", "", "");
            var now = DateTimeOffset.UtcNow;
            await repo.CreateOperationAsync(new LedgerOperation(Guid.NewGuid(), customer.Id, OperationType.Receive, 40m, "partial receipt", now, admin!.Id));
            await repo.CreateOperationAsync(new LedgerOperation(Guid.NewGuid(), supplier.Id, OperationType.Payment, 25m, "supplier payment", now.AddMinutes(1), admin.Id));

            var report = await repo.GetReportSummaryAsync(now.AddDays(-1), now.AddDays(1));
            Assert.Equal(0m, report.Sales);
            Assert.Equal(0m, report.Purchases);
            var cash = await repo.GetCashRegisterSummaryAsync(now.AddDays(-1), now.AddDays(1));
            Assert.Equal(40m, cash.CashIn);
            Assert.Equal(25m, cash.CashOut);
            Assert.Equal(15m, cash.Balance);
            Assert.Equal(-40m, LedgerBalanceCalculator.CalculateNetBalance(await repo.GetOperationsAsync(customer.Id)));
            Assert.Equal(25m, LedgerBalanceCalculator.CalculateNetBalance(await repo.GetOperationsAsync(supplier.Id)));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}

