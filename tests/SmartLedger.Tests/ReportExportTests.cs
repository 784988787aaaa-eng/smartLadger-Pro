using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using SmartLedger.Core.Models;
using SmartLedger.Infrastructure;
using SmartLedger.Infrastructure.Data;
using SmartLedger.Infrastructure.FinalRelease;
using Xunit;

namespace SmartLedger.Tests;

public sealed class ReportExportTests
{
    [Fact]
    public async Task ReportSummary_UsesInclusiveFullLocalDayRange()
    {
        var root = CreateTempRoot();
        try
        {
            var db = await SmartLedger.Infrastructure.Infrastructure.CreateDatabaseAsync(root);
            var repo = new AppRepository(db);
            var day = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);
            await using var c = db.Open();
            await c.OpenAsync();
            await using var cmd = c.CreateCommand();
            cmd.CommandText = """
INSERT INTO Users(Id,UserName,DisplayName,PasswordHash,RecoveryHash,RecoveryHint,IsAdministrator,IsActive,CreatedAt)
VALUES('test-user','tester','مستخدم اختبار','hash','recovery','hint',1,1,$created);
INSERT INTO Invoices(Id,Type,PersonId,IssuedAt,Subtotal,Discount,Total,Paid,PaymentMode,Note,CreatedBy)
SELECT 'inv-inside',0,NULL,$inside,'100','0','100','100',0,'',Id FROM Users WHERE Id='test-user';
INSERT INTO Invoices(Id,Type,PersonId,IssuedAt,Subtotal,Discount,Total,Paid,PaymentMode,Note,CreatedBy)
SELECT 'inv-outside',0,NULL,$outside,'999','0','999','999',0,'',Id FROM Users LIMIT 1;
INSERT INTO Expenses(Id,PersonId,Category,Amount,Note,OccurredAt,CreatedBy)
SELECT 'exp-inside',NULL,'تشغيل','25','',$inside,Id FROM Users LIMIT 1;
""";
            cmd.Parameters.AddWithValue("$inside", day.AddHours(12).ToString("O"));
            cmd.Parameters.AddWithValue("$outside", day.AddDays(1).ToString("O"));
            cmd.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync();

            var summary = await repo.GetReportSummaryAsync(day, day.AddDays(1).AddTicks(-1));
            Assert.Equal(100m, summary.Sales);
            Assert.Equal(0m, summary.Purchases);
            Assert.Equal(25m, summary.Expenses);
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public async Task PdfExport_SupportsAllRequiredPageKinds()
    {
        var root = CreateTempRoot();
        try
        {
            var db = await SmartLedger.Infrastructure.Infrastructure.CreateDatabaseAsync(root);
            var repo = new AppRepository(db);
            var rows = new[]
            {
                new RecentOperationRow(
                    DateTimeOffset.Now,
                    "حساب تجريبي",
                    "قبض من عميل",
                    1234.50m,
                    "بيان عربي للاختبار")
            };
            foreach (var kind in Enum.GetValues<DocumentPageKind>())
            {
                var path = Path.Combine(root, $"report-{kind}.pdf");
                await new PdfDocumentService(repo).GenerateStatementAsync(path, "التقرير المالي", rows, kind);
                Assert.True(File.Exists(path));
                Assert.True(new FileInfo(path).Length > 1000);
                if (kind == DocumentPageKind.A4)
                {
                    var reviewPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "docs", "report-design-review.pdf"));
                    Directory.CreateDirectory(Path.GetDirectoryName(reviewPath)!);
                    File.Copy(path, reviewPath, true);
                }
            }

            var longRows = Enumerable.Range(1, 180).Select(i => new RecentOperationRow(
                DateTimeOffset.Now.AddMinutes(-i),
                i % 3 == 0 ? "حساب تجريبي باسم عربي طويل لاختبار عرض الأعمدة" : "حساب تجريبي",
                i % 2 == 0 ? "قبض من عميل" : "سداد مورد",
                i % 5 == 0 ? 987654321.75m : 1234.50m,
                i % 4 == 0 ? "بيان عربي طويل للتأكد من التفاف النص وعدم تجاوز حدود الجدول عند الطباعة" : "بيان اختبار"))
                .ToArray();
            var multipagePath = Path.Combine(root, "report-multipage.pdf");
            await new PdfDocumentService(repo).GenerateStatementAsync(multipagePath, "تقرير الحركات - اختبار الصفحات المتعددة", longRows, DocumentPageKind.A4);
            Assert.True(File.Exists(multipagePath));
            Assert.True(new FileInfo(multipagePath).Length > 5000);
            var multiReviewPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "docs", "report-design-review-multipage.pdf"));
            Directory.CreateDirectory(Path.GetDirectoryName(multiReviewPath)!);
            File.Copy(multipagePath, multiReviewPath, true);
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public async Task ExcelExport_WritesAmountAsNumericCell()
    {
        var root = CreateTempRoot();
        try
        {
            var path = Path.Combine(root, "report.xlsx");
            var rows = new[]
            {
                new RecentOperationRow(DateTimeOffset.Now, "حساب تجريبي", "بيع آجل", 1234.50m, "اختبار")
            };
            new ExcelExportService().ExportStatement(path, "SmartLedger", rows, "ريال");

            using var doc = SpreadsheetDocument.Open(path, false);
            var sheet = doc.WorkbookPart!.WorksheetParts.Single().Worksheet;
            var cells = sheet.Descendants<Cell>().ToList();
            Assert.Contains(cells, c => c.StyleIndex?.Value == 1 && c.CellValue is not null && decimal.TryParse(c.CellValue.Text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var value) && value == 1234.5m);
        }
        finally { TryDelete(root); }
    }


    [Fact]
    public async Task AccountStatementExports_ContainRunningBalanceAndRealDateCells()
    {
        var root=CreateTempRoot();
        try
        {
            var db=await SmartLedger.Infrastructure.Infrastructure.CreateDatabaseAsync(root);
            var repo=new AppRepository(db);
            var userRepo=new SmartLedger.Infrastructure.Data.UserRepository(db);
            var hasher=new SmartLedger.Core.Services.PasswordHasher();
            await userRepo.CreateAdministratorAsync(new SmartLedger.Core.Models.AdminSetup("admin",hasher.Hash("Password!123"),hasher.Hash("recovery"),"hint"));
            var admin=await userRepo.FindAsync("admin");
            var book=(await repo.GetBooksAsync()).First(x=>x.Kind==BookKind.Customers);
            var person=await repo.CreatePersonAsync(book.Id,"حساب تقرير","1","");
            var ops=new[]
            {
                new LedgerOperation(Guid.NewGuid(),person.Id,OperationType.DebtToUs,100m,"افتتاح",DateTimeOffset.Now.AddHours(-2),admin!.Id),
                new LedgerOperation(Guid.NewGuid(),person.Id,OperationType.Receive,25m,"استلام",DateTimeOffset.Now.AddHours(-1),admin.Id)
            };
            foreach(var op in ops) await repo.CreateOperationAsync(op);
            var loaded=await repo.GetOperationsAsync(person.Id,100);
            var pdf=Path.Combine(root,"account.pdf");
            await new PdfDocumentService(repo).GenerateAccountStatementAsync(pdf,person.Name,book.Name,loaded,DocumentPageKind.A4);
            Assert.True(File.Exists(pdf)); Assert.True(new FileInfo(pdf).Length>1000);
            var xlsx=Path.Combine(root,"account.xlsx");
            new ExcelExportService().ExportAccountStatement(xlsx,"SmartLedger",person.Name,book.Name,loaded,"ريال");
            using var doc=SpreadsheetDocument.Open(xlsx,false);
            var cells=doc.WorkbookPart!.WorksheetParts.Single().Worksheet.Descendants<Cell>().ToList();
            Assert.Contains(cells,c=>c.StyleIndex?.Value==3&&c.DataType?.Value==CellValues.Number);
            Assert.Contains(cells,c=>c.StyleIndex?.Value==1&&c.CellValue is not null&&decimal.TryParse(c.CellValue.Text,System.Globalization.NumberStyles.Number,System.Globalization.CultureInfo.InvariantCulture,out var v)&&v==-125m);
        }
        finally { TryDelete(root); }
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "SmartLedger_ReportTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}
