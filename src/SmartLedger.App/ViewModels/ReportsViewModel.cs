using System.Windows;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;
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

    public IReadOnlyList<DocumentPageKind> PageKinds { get; } = Enum.GetValues<DocumentPageKind>();
    public string PeriodText => $"من {FromDate:yyyy/MM/dd} إلى {ToDate:yyyy/MM/dd}";
    public string MovementIndicator => AmountFormatter.Format(Summary.Sales - Summary.Purchases - Summary.Expenses, absolute: false);

    partial void OnFromDateChanged(DateTime value) => NotifyPeriod();
    partial void OnToDateChanged(DateTime value) => NotifyPeriod();
    partial void OnSummaryChanged(ReportSummary value) => OnPropertyChanged(nameof(MovementIndicator));

    private void NotifyPeriod() => OnPropertyChanged(nameof(PeriodText));

    private IReadOnlyList<RecentOperationRow> BuildExportRows()
    {
        var rows=new List<RecentOperationRow>();
        if(Summary.Sales!=0m||Summary.Purchases!=0m||Summary.Expenses!=0m||Summary.Receivables!=0m||Summary.Payables!=0m||Summary.StockValue!=0m)
        {
            var note=$"المبيعات: {AmountFormatter.Format(Summary.Sales)}؛ المشتريات: {AmountFormatter.Format(Summary.Purchases)}؛ النفقات: {AmountFormatter.Format(Summary.Expenses)}؛ لنا: {AmountFormatter.Format(Summary.Receivables)}؛ علينا: {AmountFormatter.Format(Summary.Payables)}؛ قيمة المخزون الحالية: {AmountFormatter.Format(Summary.StockValue)}";
            rows.Add(new RecentOperationRow(new DateTimeOffset(DateTime.SpecifyKind(ToDate.Date,DateTimeKind.Local)),"ملخص الفترة","ملخص مالي",Summary.Sales-Summary.Purchases-Summary.Expenses,note));
        }
        rows.AddRange(Recent);
        return rows;
    }

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

    [RelayCommand]
    private async Task ExportPdfAsync()
    {
        var exportRows=BuildExportRows();
        if (IsBusy || exportRows.Count == 0)
        {
            StatusText = exportRows.Count == 0 ? "لا توجد بيانات لتصديرها ضمن الفترة المحددة." : "انتظر حتى يكتمل التقرير.";
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
            await service.GenerateStatementAsync(dialog.FileName, $"التقرير المالي  •  الفترة {FromDate:yyyy/MM/dd} – {ToDate:yyyy/MM/dd}", exportRows, SelectedPageKind);
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
        var exportRows=BuildExportRows();
        if (IsBusy || exportRows.Count == 0)
        {
            StatusText = exportRows.Count == 0 ? "لا توجد بيانات لتصديرها ضمن الفترة المحددة." : "انتظر حتى يكتمل التقرير.";
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
            service.ExportStatement(dialog.FileName, identity.BusinessName, exportRows, identity.Currency);
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
