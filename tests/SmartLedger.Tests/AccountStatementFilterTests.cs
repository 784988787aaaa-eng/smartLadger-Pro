using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using Xunit;

namespace SmartLedger.Tests;

public sealed class AccountStatementFilterTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

    private static LedgerOperation Op(OperationType type, decimal amount, int day, string note, string? reference = null, Guid? invoiceId = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), type, amount, note, Start.AddDays(day), Guid.NewGuid())
        {
            ReferenceNumber = reference,
            SourceInvoiceId = invoiceId
        };

    [Fact]
    public void NotesSearch_FiltersVisibleRowsButPreservesLedgerBalanceAndOpening()
    {
        var rows = new[]
        {
            Op(OperationType.DebtOnUs, 100m, 0, "opening"),
            Op(OperationType.Sale, 80m, 1, "Retail invoice", "INV-101"),
            Op(OperationType.Receive, 30m, 2, "Bank transfer receipt", "RC-009"),
            Op(OperationType.Return, 10m, 3, "damaged goods return", "RET-004")
        };

        var result = AccountStatementFilter.Apply(rows, Start.AddDays(1), Start.AddDays(3), AccountStatementKindFilter.All, "bank transfer");

        Assert.Equal(100m, result.OpeningBalance);
        Assert.Single(result.Movements);
        Assert.Equal("Bank transfer receipt", result.Movements[0].Note);
        Assert.Equal(150m, result.Movements[0].RunningBalance);
        Assert.Equal(140m, result.ClosingBalance);
    }

    [Fact]
    public void VoucherFilter_ReturnsOnlyCustomerReceiptsAndKeepsOpeningAndClosingAccurate()
    {
        var rows = new[]
        {
            Op(OperationType.DebtOnUs, 200m, 0, "opening"),
            Op(OperationType.Sale, 75m, 1, "sale", "INV-202"),
            Op(OperationType.Receive, 25m, 2, "receipt one", "RC-001"),
            Op(OperationType.Receive, 15m, 3, "receipt two", "RC-002"),
            Op(OperationType.Return, 5m, 4, "return", "RET-001")
        };

        var result = AccountStatementFilter.Apply(rows, Start.AddDays(1), Start.AddDays(4), AccountStatementKindFilter.Vouchers);

        Assert.Equal(200m, result.OpeningBalance);
        Assert.Equal(2, result.Movements.Count);
        Assert.All(result.Movements, x => Assert.Equal(OperationType.Receive, x.Type));
        Assert.Equal(40m, result.Movements.Sum(x => x.Amount));
        Assert.Equal(230m, result.ClosingBalance);
    }

    [Fact]
    public void InvoiceReferenceAndSourceId_ArePreservedOnStatementMovement()
    {
        var invoiceId = Guid.NewGuid();
        var operation = Op(OperationType.Sale, 55m, 1, "Invoice posted", "INV-TRACE-7", invoiceId);

        var result = AccountStatementFilter.Apply(new[] { operation }, Start, Start.AddDays(2));

        var movement = Assert.Single(result.Movements);
        Assert.Equal("INV-TRACE-7", movement.ReferenceNumber);
        Assert.Equal(invoiceId, movement.SourceInvoiceId);
        Assert.Equal("Invoice posted", movement.Note);
        Assert.Equal(OperationType.Sale, movement.Type);
    }
}
public sealed class AccountStatementSourceLinkIntegrationTests
{
    [Fact]
    public async Task RepositoryStatementOperation_ResolvesOriginalInvoiceIdFromReferenceNumber()
    {
        var root = Path.Combine(Path.GetTempPath(), "SL_STATEMENT_LINK_" + Guid.NewGuid().ToString("N"));
        try
        {
            var database = new SmartLedger.Infrastructure.Data.Database(root);
            await database.InitializeAsync();
            var users = new SmartLedger.Infrastructure.Data.UserRepository(database);
            var hasher = new PasswordHasher();
            await users.CreateAdministratorAsync(new SmartLedger.Core.Models.AdminSetup(
                "statement_link_admin", hasher.Hash("Password!123"), hasher.Hash("recovery"), "hint"));
            var admin = await users.FindAsync("statement_link_admin");
            Assert.NotNull(admin);
            var repo = new SmartLedger.Infrastructure.Data.AppRepository(database);
            var books = await repo.GetBooksAsync();
            var customer = await repo.CreatePersonAsync(books.Single(x => x.Kind == BookKind.Customers).Id,
                "Statement Link Customer", "", "");
            var invoiceId = Guid.NewGuid();
            const string invoiceNumber = "INV-TRACE-DB-01";
            var now = DateTimeOffset.UtcNow;
            await using (var db = database.Open())
            {
                await db.OpenAsync();
                await using var cmd = db.CreateCommand();
                cmd.CommandText = "INSERT INTO Invoices(Id,Type,PersonId,IssuedAt,Subtotal,Discount,Total,Paid,PaymentMode,Note,CreatedBy,InvoiceNumber) VALUES($id,$type,$person,$issued,$subtotal,$discount,$total,$paid,$mode,$note,$user,$number);";
                cmd.Parameters.AddWithValue("$id", invoiceId.ToString("N"));
                cmd.Parameters.AddWithValue("$type", (int)InvoiceType.Sale);
                cmd.Parameters.AddWithValue("$person", customer.Id.ToString("N"));
                cmd.Parameters.AddWithValue("$issued", now.ToString("O"));
                cmd.Parameters.AddWithValue("$subtotal", "55");
                cmd.Parameters.AddWithValue("$discount", "0");
                cmd.Parameters.AddWithValue("$total", "55");
                cmd.Parameters.AddWithValue("$paid", "0");
                cmd.Parameters.AddWithValue("$mode", (int)PaymentMode.Credit);
                cmd.Parameters.AddWithValue("$note", "Traceability test");
                cmd.Parameters.AddWithValue("$user", admin!.Id.ToString("N"));
                cmd.Parameters.AddWithValue("$number", invoiceNumber);
                await cmd.ExecuteNonQueryAsync();
            }

            await repo.CreateOperationAsync(new LedgerOperation(Guid.NewGuid(), customer.Id, OperationType.Sale,
                55m, "Invoice posted; delivery note included", now, admin!.Id) { ReferenceNumber = invoiceNumber });

            var operation = Assert.Single(await repo.GetOperationsAsync(customer.Id));
            Assert.Equal(invoiceNumber, operation.ReferenceNumber);
            Assert.Equal(invoiceId, operation.SourceInvoiceId);
            Assert.Equal("Invoice posted; delivery note included", operation.Note);
            Assert.Equal(OperationType.Sale, operation.Type);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
