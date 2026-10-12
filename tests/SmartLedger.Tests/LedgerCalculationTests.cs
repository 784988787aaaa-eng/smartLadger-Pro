using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Data;
using Xunit;

namespace SmartLedger.Tests;

public sealed class LedgerCalculationTests
{
    private static readonly decimal[] ExpectedRunningBalances = { 100m, 70m, 120m, 140m };
    [Fact]
    public void AccountStatement_IncludesInitialBalanceAndPrePeriodMovements()
    {
        var person = Guid.NewGuid();
        var user = Guid.NewGuid();
        var from = DateTimeOffset.Parse("2026-10-01T00:00:00Z");
        var to = DateTimeOffset.Parse("2026-10-31T23:59:59Z");
        var operations = new[]
        {
            new LedgerOperation(Guid.NewGuid(), person, OperationType.DebtOnUs, 25m, "قبل الفترة", from.AddDays(-2), user),
            new LedgerOperation(Guid.NewGuid(), person, OperationType.Receive, 10m, "داخل الفترة", from.AddDays(1), user)
        };

        var statement = FinancialReportCalculations.CalculateAccountStatement(operations, from, to, initialBalance: 500m);

        Assert.Equal(525m, statement.OpeningBalance);
        Assert.Single(statement.Movements);
        Assert.Equal(515m, statement.Movements[0].RunningBalance);
        Assert.Equal(515m, statement.ClosingBalance);
    }

    [Fact]
    public void RunningBalances_AccumulateDebitsAndCreditsChronologically()
    {
        var person=Guid.NewGuid();
        var user=Guid.NewGuid();
        var start=DateTimeOffset.Parse("2026-01-01T09:00:00Z");
        var operations=new[]
        {
            new LedgerOperation(Guid.NewGuid(),person,OperationType.DebtOnUs,100m,"مدين",start,user),
            new LedgerOperation(Guid.NewGuid(),person,OperationType.Receive,30m,"تحصيل",start.AddHours(1),user),
            new LedgerOperation(Guid.NewGuid(),person,OperationType.Sale,50m,"بيع",start.AddHours(2),user),
            new LedgerOperation(Guid.NewGuid(),person,OperationType.Payment,20m,"سداد",start.AddHours(3),user)
        };
        var rows=LedgerBalanceCalculator.CalculateRunningBalances(operations);
        Assert.Equal(ExpectedRunningBalances,rows.Select(x=>x.RunningBalance));
        Assert.Equal(140m,LedgerBalanceCalculator.CalculateNetBalance(operations));
    }

    [Fact]
    public async Task RepositoryDeleteOperation_SoftDeletesAtomicallyAndRecalculatesLedger()
    {
        var root=Path.Combine(Path.GetTempPath(),"SmartLedgerTests",Guid.NewGuid().ToString("N"));
        try
        {
            var database=new Database(root);
            await database.InitializeAsync();
            var users=new UserRepository(database);
            await users.CreateAdministratorAsync(new AdminSetup("ledger_test_admin","test-hash","recovery-hash","hint"));
            var user=await users.FindAsync("ledger_test_admin") ?? throw new InvalidOperationException("Test user was not created.");
            var repository=new AppRepository(database);
            var book=(await repository.GetBooksAsync()).First(x=>x.Kind==BookKind.Customers);
            var now=DateTimeOffset.UtcNow;
            var person=await repository.CreatePersonAsync(new Person(Guid.NewGuid(),book.Id,"حساب اختبار","","",now));
            var debit1=await repository.CreateOperationAsync(new LedgerOperation(Guid.NewGuid(),person.Id,OperationType.DebtOnUs,100m,"مدين أول",now,user.Id));
            var credit=await repository.CreateOperationAsync(new LedgerOperation(Guid.NewGuid(),person.Id,OperationType.Receive,20m,"تحصيل",now.AddMinutes(1),user.Id));
            await repository.CreateOperationAsync(new LedgerOperation(Guid.NewGuid(),person.Id,OperationType.Sale,30m,"مدين ثان",now.AddMinutes(2),user.Id));
            Assert.Equal(110m,LedgerBalanceCalculator.CalculateNetBalance(await repository.GetOperationsAsync(person.Id,1000)));
            await repository.DeleteOperationAsync(credit.Id,user.Id);
            var remaining=await repository.GetOperationsAsync(person.Id,1000);
            Assert.DoesNotContain(remaining,x=>x.Id==credit.Id);
            Assert.Contains((await repository.GetDeletedItemsAsync()),x=>x.EntityType=="Operation" && x.EntityId==credit.Id);
            Assert.Equal(130m,LedgerBalanceCalculator.CalculateNetBalance(remaining));
            Assert.Contains(remaining,x=>x.Id==debit1.Id);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if(Directory.Exists(root)) Directory.Delete(root,true);
        }
    }

    [Fact]
    public void RemovingAnOperation_RecalculatesAllSubsequentBalances()
    {
        var person=Guid.NewGuid();
        var user=Guid.NewGuid();
        var start=DateTimeOffset.Parse("2026-01-01T09:00:00Z");
        var first=new LedgerOperation(Guid.NewGuid(),person,OperationType.DebtOnUs,100m,"مدين",start,user);
        var second=new LedgerOperation(Guid.NewGuid(),person,OperationType.Receive,25m,"تحصيل",start.AddHours(1),user);
        var third=new LedgerOperation(Guid.NewGuid(),person,OperationType.Sale,40m,"بيع",start.AddHours(2),user);
        var before=LedgerBalanceCalculator.CalculateRunningBalances(new[]{first,second,third});
        var after=LedgerBalanceCalculator.CalculateRunningBalances(new[]{first,third});
        Assert.Equal(115m,before[^1].RunningBalance);
        Assert.Equal(140m,after[^1].RunningBalance);
        Assert.Equal(140m,LedgerBalanceCalculator.CalculateNetBalance(new[]{first,third}));
    }
}
