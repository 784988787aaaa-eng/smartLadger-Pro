using SmartLedger.Core.Models;
using Xunit;

namespace SmartLedger.Tests;

public sealed class LedgerCalculationTests
{
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
        Assert.Equal(new[] { 100m, 70m, 120m, 140m },rows.Select(x=>x.RunningBalance));
        Assert.Equal(140m,LedgerBalanceCalculator.CalculateNetBalance(operations));
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
