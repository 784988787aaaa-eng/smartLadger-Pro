using Microsoft.Data.Sqlite;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Data;
using Xunit;

namespace SmartLedger.Tests;

public sealed class InvoiceReversalTests
{
    [Fact]
    public async Task ReverseInvoiceAsync_RollsBackWhenAuditFails_ThenRestoresLedgerCashAndExactBatchesOnce()
    {
        var root = Path.Combine(Path.GetTempPath(), "SmartLedger-InvoiceReversal-" + Guid.NewGuid().ToString("N"));
        try
        {
            var database = new Database(root);
            await database.InitializeAsync();

            var users = new UserRepository(database);
            var hasher = new PasswordHasher();
            await users.CreateAdministratorAsync(new AdminSetup(
                "reversaladmin", hasher.Hash("Password!123"), hasher.Hash("recovery"), "hint"));
            var admin = await users.FindAsync("reversaladmin");
            Assert.NotNull(admin);

            var repository = new AppRepository(database);
            var customerBook = (await repository.GetBooksAsync()).Single(x => x.Kind == BookKind.Customers);
            var customer = await repository.CreatePersonAsync(customerBook.Id, "عميل عكس الفاتورة", "7770000", "");
            var product = await repository.CreateProductAsync(new Product(
                Guid.NewGuid(), "صنف عكس الفاتورة", "REVERSAL-001", "اختبار",
                "كرتون", "حبة", 12m, 2m, 3m, 0m, 1m, DateTimeOffset.UtcNow));

            await repository.AddStockBatchAsync(new ProductBatch(
                Guid.NewGuid(), product.Id, "REV-EARLY", DateTimeOffset.UtcNow.AddMonths(1),
                10m, 1.5m, null, "PUR-REV-1", DateTimeOffset.UtcNow.AddDays(-2)));
            await repository.AddStockBatchAsync(new ProductBatch(
                Guid.NewGuid(), product.Id, "REV-LATE", DateTimeOffset.UtcNow.AddMonths(4),
                20m, 1.8m, null, "PUR-REV-2", DateTimeOffset.UtcNow.AddDays(-1)));

            var invoice = new Invoice(
                Guid.NewGuid(), InvoiceType.Sale, customer.Id, DateTimeOffset.UtcNow,
                36m, 0m, 36m, 10m, PaymentMode.Credit, "اختبار عكس متكامل", admin!.Id);
            var line = new InvoiceLine(
                Guid.NewGuid(), invoice.Id, product.Id, product.Name, 12m, product.SmallUnit,
                3m, 0m, 36m)
            {
                Barcode = product.Barcode,
                UnitSelected = product.SmallUnit,
                ConversionFactorApplied = 1m,
                QuantityMinor = 12m,
                CostPriceSnapshot = product.PurchasePrice
            };
            var movement = new StockMovement(
                Guid.NewGuid(), product.Id, StockMovementType.Sale, 12m, 3m,
                "اختبار عكس متكامل", DateTimeOffset.UtcNow, admin.Id);
            var customerDebit = new LedgerOperation(
                Guid.NewGuid(), customer.Id, OperationType.DebtOnUs, 26m,
                "رصيد مستحق من فاتورة البيع", DateTimeOffset.UtcNow, admin.Id);

            var saved = await repository.SaveSalesInvoiceAsync(invoice, new[] { line }, new[] { movement }, customerDebit);
            Assert.Equal(18m, Assert.Single(await repository.GetProductsAsync(product.Barcode)).StockSmallUnits);

            // Force audit insertion to fail and prove that the reversal transaction rolls back all effects.
            await using (var connection = database.Open())
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TRIGGER FailInvoiceReversalAudit
                    BEFORE INSERT ON AuditLogs
                    WHEN NEW.Action = 'INVOICE_REVERSED'
                    BEGIN
                        SELECT RAISE(ABORT, 'forced audit failure');
                    END;
                    """;
                await command.ExecuteNonQueryAsync();
            }

            await Assert.ThrowsAsync<SqliteException>(
                async () => await repository.ReverseInvoiceAsync(saved.InvoiceNumber, "audit rollback test", admin.Id));

            await using (var connection = database.Open())
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT Status FROM Invoices WHERE Id=$id;";
                command.Parameters.AddWithValue("$id", saved.Id.ToString("N"));
                Assert.Equal("Posted", Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));

                command.Parameters.Clear();
                command.CommandText = "SELECT COUNT(*) FROM AuditLogs WHERE Action='INVOICE_REVERSED';";
                Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));

                command.Parameters.Clear();
                command.CommandText = "DROP TRIGGER FailInvoiceReversalAudit;";
                await command.ExecuteNonQueryAsync();
            }

            Assert.Equal(18m, Assert.Single(await repository.GetProductsAsync(product.Barcode)).StockSmallUnits);
            var consumedBatches = await repository.GetProductBatchesAsync(product.Id);
            Assert.Equal(0m, consumedBatches.Single(x => x.BatchNumber == "REV-EARLY").QuantityMinor);
            Assert.Equal(18m, consumedBatches.Single(x => x.BatchNumber == "REV-LATE").QuantityMinor);

            Assert.True(await repository.ReverseInvoiceAsync(saved.InvoiceNumber, "confirmed reversal", admin.Id));

            // Aggregate stock and the exact FEFO lots must return to their pre-sale quantities.
            Assert.Equal(30m, Assert.Single(await repository.GetProductsAsync(product.Barcode)).StockSmallUnits);
            var restoredBatches = await repository.GetProductBatchesAsync(product.Id);
            Assert.Equal(10m, restoredBatches.Single(x => x.BatchNumber == "REV-EARLY").QuantityMinor);
            Assert.Equal(20m, restoredBatches.Single(x => x.BatchNumber == "REV-LATE").QuantityMinor);

            // Customer debit (26) and reversal receipt (26) net to zero; the paid cash leg (10)
            // is offset by exactly one refund entry (10).
            var operations = await repository.GetOperationsAsync(customer.Id);
            Assert.Equal(2, operations.Count);
            Assert.Contains(operations, x => x.Type == OperationType.DebtOnUs && x.Amount == 26m);
            Assert.Contains(operations, x => x.Type == OperationType.Receive && x.Amount == 26m);
            Assert.Equal(0m, operations.Sum(x => x.Type == OperationType.DebtOnUs ? x.Amount : -x.Amount));

            await using (var connection = database.Open())
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT EntryType,Amount FROM CashRegisterEntries WHERE InvoiceId=$invoice ORDER BY EntryType;";
                command.Parameters.AddWithValue("$invoice", saved.Id.ToString("N"));
                var entries = new List<(string Type, decimal Amount)>();
                await using (var reader = await command.ExecuteReaderAsync())
                    while (await reader.ReadAsync())
                        entries.Add((reader.GetString(0), decimal.Parse(reader.GetString(1), System.Globalization.CultureInfo.InvariantCulture)));

                Assert.Equal(2, entries.Count);
                Assert.Contains(("SALE_RECEIPT", 10m), entries);
                Assert.Contains(("SALE_REFUND", 10m), entries);
                Assert.Equal(0m, entries.Sum(x => x.Type == "SALE_RECEIPT" ? x.Amount : -x.Amount));
            }

            // A second reversal is rejected and cannot duplicate ledger, cash, audit, or stock effects.
            Assert.False(await repository.ReverseInvoiceAsync(saved.InvoiceNumber, "duplicate reversal", admin.Id));
            Assert.Equal(30m, Assert.Single(await repository.GetProductsAsync(product.Barcode)).StockSmallUnits);
            Assert.Equal(2, (await repository.GetOperationsAsync(customer.Id)).Count);

            await using (var connection = database.Open())
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM CashRegisterEntries WHERE InvoiceId=$invoice;";
                command.Parameters.AddWithValue("$invoice", saved.Id.ToString("N"));
                Assert.Equal(2L, Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));

                command.Parameters.Clear();
                command.CommandText = "SELECT COUNT(*) FROM AuditLogs WHERE Action='INVOICE_REVERSED';";
                Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ReverseInvoiceAsync_ReturnsFalseWhenInvoiceDoesNotExist()
    {
        var root = Path.Combine(Path.GetTempPath(), "SmartLedger-InvoiceReversal-" + Guid.NewGuid().ToString("N"));
        try
        {
            var database = new Database(root);
            await database.InitializeAsync();
            var users = new UserRepository(database);
            var hasher = new PasswordHasher();
            await users.CreateAdministratorAsync(new AdminSetup(
                "missinginvoiceadmin", hasher.Hash("Password!123"), hasher.Hash("recovery"), "hint"));
            var admin = await users.FindAsync("missinginvoiceadmin");
            Assert.NotNull(admin);

            var repository = new AppRepository(database);
            var result = await repository.ReverseInvoiceAsync("INV-DOES-NOT-EXIST", "integration test missing invoice", admin!.Id);
            Assert.False(result);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }
}
