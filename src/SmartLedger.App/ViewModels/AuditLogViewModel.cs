using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartLedger.Core.Models;
using SmartLedger.Infrastructure.Audit;

namespace SmartLedger.App.ViewModels;

public partial class AuditLogViewModel : ObservableObject
{
    private readonly AuditRepository repo;

    [ObservableProperty] private IReadOnlyList<AuditEntry> entries = Array.Empty<AuditEntry>();
    [ObservableProperty] private DateTime fromDate = DateTime.Today.AddDays(-29);
    [ObservableProperty] private DateTime toDate = DateTime.Today;
    [ObservableProperty] private string textFilter = "";
    [ObservableProperty] private string actionFilter = "";
    [ObservableProperty] private string entityFilter = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusText = "";

    public AuditLogViewModel(AuditRepository repo) => this.repo = repo;
    public async Task LoadAsync() => await SearchAsync();

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var from = new DateTimeOffset(FromDate.Date);
            var to = new DateTimeOffset(ToDate.Date.AddDays(1).AddTicks(-1));
            Entries = await repo.SearchAsync(from, to, action: ActionFilter, entityType: EntityFilter, text: TextFilter);
            StatusText = $"تم العثور على {Entries.Count:N0} سجل.";
        }
        catch (Exception ex) { StatusText = ex.Message; }
        finally { IsBusy = false; }
    }
}
