using System.Windows;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using SmartLedger.App.Formatting;
using SmartLedger.Infrastructure.FinalRelease;

namespace SmartLedger.App.ViewModels;

public partial class ReportsViewModel(IAppRepository repo) : ObservableObject
{
    [ObservableProperty] private ReportSummary summary = new(0,0,0,0,0,0);
    [ObservableProperty] private DateTime fromDate = DateTime.Today.AddDays(-29);
    [ObservableProperty] private DateTime toDate = DateTime.Today;
    [ObservableProperty] private IReadOnlyList<RecentOperationRow> recent = Array.Empty<RecentOperationRow>();
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusText = "";
    [ObservableProperty] private DocumentPageKind selectedPageKind = DocumentPageKind.A4;
    [ObservableProperty] private IReadOnlyList<PersonSummary> statementAccounts = Array.Empty<PersonSummary>();
    [ObservableProperty] private PersonSummary? selectedStatementAccount;
    [ObservableProperty] private IReadOnlyList<LedgerOperation> accountStatementMovements = Array.Empty<LedgerOperation>();
    [ObservableProperty] private IReadOnlyList<SpecializedExpenseRow> expenseAnalysis = Array.Empty<SpecializedExpenseRow>();
    [ObservableProperty] private string selectedReportMode = "Overview";
    [ObservableProperty] private decimal openingBalance;
    [ObservableProperty] private decimal closingBalance;
    [ObservableProperty] private decimal totalPhysicalStock;
    [ObservableProperty] private decimal stockCostValuation;
    [ObservableProperty] private decimal stockRetailValuation;
    [ObservableProperty] private decimal expectedGrossProfit;
    [ObservableProperty] private decimal totalCategorizedExpenses;
    [ObservableProperty] private string closingBalanceDirection = "Settled";
    public IReadOnlyList<string> ReportModes { get; } = new[] { "Overview", "Account Statement", "Inventory Valuation", "Expense Analysis" };
    public sealed record SpecializedExpenseRow(string Category, decimal Total, decimal Percentage, decimal CashTotal, decimal BankTotal);
    public static decimal SignedLedgerEffect(LedgerOperation op) => FinancialReportCalculations.SignedLedgerEffect(op);

    public IReadOnlyList<DocumentPageKind> PageKinds { get; } = Enum.GetValues<DocumentPageKind>();
    public string PeriodText => $"من {FromDate:yyyy/MM/dd} إلى {ToDate:yyyy/MM/dd}";
    public string MovementIndicator => AmountFormatter.Format(Summary.Sales - Summary.Purchases - Summary.Expenses, absolute: false);

    partial void OnFromDateChanged(DateTime value) => NotifyPeriod();
    partial void OnToDateChanged(DateTime value) => NotifyPeriod();
    partial void OnSummaryChanged(ReportSummary value) => OnPropertyChanged(nameof(MovementIndicator));

    private void NotifyPeriod() => OnPropertyChanged(nameof(PeriodText));

    [RelayCommand] private void SelectToday(){FromDate=DateTime.Today;ToDate=DateTime.Today;}
    [RelayCommand] private void SelectThisWeek(){var today=DateTime.Today;var monday=today.AddDays(-(((int)today.DayOfWeek+6)%7));FromDate=monday;ToDate=today;}
    [RelayCommand] private void SelectThisMonth(){FromDate=new DateTime(DateTime.Today.Year,DateTime.Today.Month,1);ToDate=DateTime.Today;}
    [RelayCommand] private void SelectCustomRange(){StatusText="حدد تاريخ البداية والنهاية من حقلي التاريخ أعلاه.";}

    public Task LoadAsync() => RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy) return;
        if (FromDate > DateTime.Today) FromDate = DateTime.Today;
        if (ToDate < FromDate) ToDate = FromDate;

        IsBusy = true;
        StatusText = "جاري تحميل التقرير...";
        try
        {
            var from = new DateTimeOffset(DateTime.SpecifyKind(FromDate.Date, DateTimeKind.Local));
            var to = new DateTimeOffset(DateTime.SpecifyKind(ToDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local));

            Summary = await repo.GetReportSummaryAsync(from, to);
            var byId = await repo.GetPersonNamesAsync();
            var operations = await repo.GetOperationsInPeriodAsync(from, to);

            Recent = operations
                .Select(x => new RecentOperationRow(
                    x.OccurredAt,
                    byId.TryGetValue(x.PersonId, out var name) ? name : "غير معروف",
                    OperationLabels.Label(x.Type),
                    x.Amount,
                    x.Note))
                .ToArray();

            await LoadSpecializedReportsAsync(from, to);

            StatusText = $"تم تحديث التقرير • {Recent.Count} حركة ضمن الفترة";
        }
        catch (Exception ex)
        {
            StatusText = $"تعذر تحميل التقرير: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedStatementAccountChanged(PersonSummary? value) => _ = LoadSelectedAccountStatementAsync();

    partial void OnSelectedReportModeChanged(string value)
    {
        if (value == "Account Statement") _ = LoadSelectedAccountStatementAsync();
    }

    private async Task LoadSpecializedReportsAsync(DateTimeOffset from, DateTimeOffset to)
    {
        StatementAccounts = await repo.GetPeopleAsync();
        var products = await repo.GetProductsAsync();
        var valuation = FinancialReportCalculations.CalculateInventoryValuation(products);
        TotalPhysicalStock = valuation.PhysicalQuantity;
        StockCostValuation = valuation.CostValue;
        StockRetailValuation = valuation.RetailValue;
        ExpectedGrossProfit = valuation.ExpectedGrossProfit;
        OnPropertyChanged(nameof(TotalPhysicalStock)); OnPropertyChanged(nameof(StockCostValuation));
        OnPropertyChanged(nameof(StockRetailValuation)); OnPropertyChanged(nameof(ExpectedGrossProfit));
        var expenses = await repo.GetExpensesAsync(from, to);
        TotalCategorizedExpenses = expenses.Sum(e => e.Amount);
        ExpenseAnalysis = expenses.GroupBy(e => string.IsNullOrWhiteSpace(e.Category) ? "Uncategorized" : e.Category.Trim())
            .Select(g => new SpecializedExpenseRow(g.Key, g.Sum(e => e.Amount),
                TotalCategorizedExpenses == 0 ? 0 : decimal.Round(g.Sum(e => e.Amount) * 100m / TotalCategorizedExpenses, 2),
                g.Where(e => string.Equals(e.PaymentMethod, "Cash", StringComparison.OrdinalIgnoreCase)).Sum(e => e.Amount),
                g.Where(e => string.Equals(e.PaymentMethod, "Bank", StringComparison.OrdinalIgnoreCase)).Sum(e => e.Amount)))
            .OrderByDescending(x => x.Total).ToArray();
        OnPropertyChanged(nameof(TotalCategorizedExpenses));
    }

    [RelayCommand]
    private async Task SelectReportModeAsync(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return;
        SelectedReportMode = mode;
        if (mode == "Account Statement") await LoadSelectedAccountStatementAsync();
        else await RefreshAsync();
    }

    private async Task LoadSelectedAccountStatementAsync()
    {
        if (SelectedStatementAccount is null) { AccountStatementMovements = Array.Empty<LedgerOperation>(); OpeningBalance = ClosingBalance = 0m; OnPropertyChanged(nameof(OpeningBalance)); OnPropertyChanged(nameof(ClosingBalance)); return; }
        var operations = await repo.GetOperationsAsync(SelectedStatementAccount.PersonId, 10000);
        var from = new DateTimeOffset(DateTime.SpecifyKind(FromDate.Date, DateTimeKind.Local));
        var to = new DateTimeOffset(DateTime.SpecifyKind(ToDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local));
        var calculation = FinancialReportCalculations.CalculateAccountStatement(operations, from, to);
        OpeningBalance = calculation.OpeningBalance;
        AccountStatementMovements = calculation.Movements;
        ClosingBalance = calculation.ClosingBalance;
        ClosingBalanceDirection = ClosingBalance > 0 ? "Due from account (Debit)" : ClosingBalance < 0 ? "Credit owed to account" : "Settled";
        OnPropertyChanged(nameof(OpeningBalance)); OnPropertyChanged(nameof(ClosingBalance)); OnPropertyChanged(nameof(ClosingBalanceDirection));
    }

    [RelayCommand]
    private async Task ExportAccountStatementPdfAsync()
    {
        if (SelectedStatementAccount is null) { StatusText = "Select an account before exporting its statement."; return; }
        var dialog = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", FileName = "SmartLedger_Account_Statement.pdf", AddExtension = true, OverwritePrompt = true };
        if (dialog.ShowDialog(Application.Current?.MainWindow) != true) return;
        try
        {
            var from = new DateTimeOffset(DateTime.SpecifyKind(FromDate.Date, DateTimeKind.Local));
            var to = new DateTimeOffset(DateTime.SpecifyKind(ToDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local));
            var all = await repo.GetOperationsAsync(SelectedStatementAccount.PersonId, 10000);
            var opening = all.Where(x => x.OccurredAt < from).Sum(SignedLedgerEffect);
            var period = all.Where(x => x.OccurredAt >= from && x.OccurredAt <= to).OrderBy(x => x.OccurredAt).ToList();
            if (opening != 0m) period.Insert(0, new LedgerOperation(Guid.Empty, SelectedStatementAccount.PersonId, opening > 0 ? OperationType.DebtOnUs : OperationType.DebtToUs, Math.Abs(opening), "Opening balance", from.AddTicks(-1), Guid.Empty));
            await new PdfDocumentService(repo).GenerateAccountStatementAsync(dialog.FileName, SelectedStatementAccount.Person.Name, SelectedStatementAccount.BookName, period, SelectedPageKind, periodFrom: from, periodTo: to);
            StatusText = "Account statement PDF exported: " + dialog.FileName;
        }
        catch (Exception ex) { StatusText = "Account statement PDF export failed: " + ex.Message; }
    }
    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        if (IsBusy || Recent.Count == 0)
        {
            StatusText = Recent.Count == 0 ? "لا توجد بيانات لتصديرها ضمن الفترة المحددة." : "انتظر حتى يكتمل التقرير.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = $"SmartLedger_Report_{FromDate:yyyyMMdd}_{ToDate:yyyyMMdd}.pdf",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(Application.Current?.MainWindow) != true) return;

        IsBusy = true;
        StatusText = "جاري إنشاء ملف PDF...";
        try
        {
            var service = new PdfDocumentService(repo);
            await service.GenerateStatementAsync(dialog.FileName, $"التقرير المالي  •  الفترة {FromDate:yyyy/MM/dd} – {ToDate:yyyy/MM/dd}", Recent, SelectedPageKind);
            StatusText = $"تم تصدير PDF بنجاح: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusText = $"فشل تصدير PDF: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        if (IsBusy || Recent.Count == 0)
        {
            StatusText = Recent.Count == 0 ? "لا توجد بيانات لتصديرها ضمن الفترة المحددة." : "انتظر حتى يكتمل التقرير.";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Excel (*.xlsx)|*.xlsx",
            FileName = $"SmartLedger_Report_{FromDate:yyyyMMdd}_{ToDate:yyyyMMdd}.xlsx",
            AddExtension = true,
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(Application.Current?.MainWindow) != true) return;

        IsBusy = true;
        StatusText = "جاري إنشاء ملف Excel...";
        try
        {
            var identity = await repo.GetIdentityAsync();
            var service = new ExcelExportService();
            service.ExportStatement(dialog.FileName, identity.BusinessName, Recent, identity.Currency);
            StatusText = $"تم تصدير Excel بنجاح: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusText = $"فشل تصدير Excel: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

internal static class OperationLabels
{
    public static string Label(OperationType type) => type switch
    {
        OperationType.Receive => "استلام",
        OperationType.Payment => "سداد",
        OperationType.DebtToUs => "له",
        OperationType.DebtOnUs => "عليه",
        OperationType.Sale => "بيع آجل",
        OperationType.Purchase => "شراء آجل",
        OperationType.Return => "مرتجع",
        OperationType.Settlement => "تسوية",
        _ => "أخرى"
    };
}
