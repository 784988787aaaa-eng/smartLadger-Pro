using CommunityToolkit.Mvvm.ComponentModel;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;
namespace SmartLedger.App.ViewModels;
public partial class DashboardViewModel(IAppRepository repo):ObservableObject
{
 [ObservableProperty]private DashboardSummary summary=new(0,0,0,0,0,0,0);[ObservableProperty]private IReadOnlyList<RecentOperationRow> recent=Array.Empty<RecentOperationRow>();[ObservableProperty]private bool loading;
 public decimal DailyNetEstimate => Summary.SalesToday - Summary.PurchasesToday - Summary.ExpensesToday;
 public async Task LoadAsync(){Loading=true;Summary=await repo.GetDashboardAsync();Recent=await repo.GetRecentOperationsAsync(10);OnPropertyChanged(nameof(DailyNetEstimate));Loading=false;}
}
