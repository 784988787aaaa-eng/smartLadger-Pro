using System.Reflection;
using SmartLedger.App.ViewModels;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;
using Xunit;

namespace SmartLedger.Tests;

public sealed class AccountsViewModelTests
{
    [Fact]
    public async Task SaveAndNewCommand_RejectsMissingNameWithoutPersistence()
    {
        var repository=DispatchProxy.Create<IAppRepository,RepositoryNotCalledProxy>();
        var vm=new AccountsViewModel(repository,new TestSession());
        await vm.SaveAndNewCommand.ExecuteAsync(null);
        Assert.Contains("اسم الحساب",vm.StatusText,StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAndCloseCommand_RejectsMissingNameWithoutClosingDrawer()
    {
        var repository=DispatchProxy.Create<IAppRepository,RepositoryNotCalledProxy>();
        var vm=new AccountsViewModel(repository,new TestSession()) { IsDialogOpen=true };
        await vm.SaveAndCloseCommand.ExecuteAsync(null);
        Assert.Contains("اسم الحساب",vm.StatusText,StringComparison.Ordinal);
        Assert.True(vm.IsDialogOpen);
    }

    [Fact]
    public void StatementKpis_AggregateDebitCreditAndNetBalance()
    {
        var person=Guid.NewGuid();
        var user=Guid.NewGuid();
        var now=DateTimeOffset.UtcNow;
        var vm=new AccountsViewModel(DispatchProxy.Create<IAppRepository,RepositoryNotCalledProxy>(),new TestSession())
        {
            SelectedAccountOperations=new[]
            {
                new LedgerOperation(Guid.NewGuid(),person,OperationType.DebtOnUs,100m,"دين",now,user),
                new LedgerOperation(Guid.NewGuid(),person,OperationType.Receive,35m,"تحصيل",now.AddMinutes(1),user),
                new LedgerOperation(Guid.NewGuid(),person,OperationType.Sale,20m,"بيع",now.AddMinutes(2),user)
            }
        };
        Assert.Equal(120m,vm.TotalDebit);
        Assert.Equal(35m,vm.TotalCredit);
        Assert.Equal(85m,vm.NetBalance);
        Assert.Equal("عليه (مدين)",vm.BalanceStatusText);
    }

    public class RepositoryNotCalledProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => throw new InvalidOperationException($"Unexpected repository call: {targetMethod?.Name}");
    }

    private sealed class TestSession : IAppSession
    {
        public AppUser? CurrentUser => null;
        public bool IsAuthenticated => false;
        public void Start(User user) => throw new NotSupportedException();
        public void Logout() => throw new NotSupportedException();
    }
}
