using Microsoft.Data.Sqlite;
using SmartLedger.Core.Models;
using SmartLedger.Infrastructure.Data;
using Xunit;

namespace SmartLedger.Tests;

public sealed class PurchaseInvoiceTests
{
    [Fact]
    public async Task SavePurchaseInvoice_IncreasesStockCreatesBatchAndPostsSupplierCredit()
    {
        var root = Path.Combine(Path.GetTempPath(), "SmartLedger-Purchase-" + Guid.NewGuid().ToString("N"));
        try
        {
            var database = new Database(root);
            await database.InitializeAsync();
            var userId = Guid.NewGuid();
            await using (var connection = database.Open())
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO Users(Id,UserName,DisplayName,PasswordHash,RecoveryHash,RecoveryHint,IsAdministrator,IsActive,CreatedAt) VALUES($id,'purchase-test','Purchase Test','hash','','',1,1,$created);";
                command.Parameters.AddWithValue("$id", userId.ToString("N"));
                command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
                await command.ExecuteNonQueryAsync();
            }

            var repository = new AppRepository(database);
            var book = await repository.CreateBookAsync("Phase 4 Supplier Book", "#123456", "truck");
            var supplier = await repository.CreatePersonAsync(book.Id, "مورد الاختبار", "", "");
            var product = await repository.CreateProductAsync(new Product(
                Guid.NewGuid(), "Phase 4 Test Product", "PHASE4-TEST-123", "اختبار",
                "كرتون", "حبة", 12m, 4.17m, 6m, 0m, 0m, DateTimeOffset.UtcNow));

            var invoiceId = Guid.NewGuid();
            var invoice = new Invoice(invoiceId, InvoiceType.Purchase, supplier.Id, DateTimeOffset.UtcNow,
                50m, 0m, 50m, 0m, PaymentMode.Credit, "اختبار شراء", userId)
            {
                InvoiceNumber = "INV-PHASE4-TEST-0001",
                CustomerName = supplier.Name
            };
            var line = new InvoiceLine(Guid.NewGuid(), invoiceId, product.Id, product.Name, 1m, "كرتون", 50m, 0m, 50m)
            {
                QuantityMinor = 12m,
                ConversionFactorApplied = 12m,
                UnitSelected = "كرتون",
                BatchNumber = "LOT-PHASE4-01",
                ExpiryDate = DateTimeOffset.UtcNow.AddMonths(6)
            };
            var movement = new StockMovement(Guid.NewGuid(), product.Id, StockMovementType.Purchase, 12m,
                4.1667m, "اختبار شراء", DateTimeOffset.UtcNow, userId);
            var operation = new LedgerOperation(Guid.NewGuid(), supplier.Id, OperationType.DebtToUs, 50m,
                "شراء آجل", DateTimeOffset.UtcNow, userId);

            await repository.CreateInvoiceTransactionWithMovementsAsync(invoice, new[] { line }, new[] { movement }, operation);

            var savedProduct = Assert.Single(await repository.GetProductsAsync("PHASE4-TEST-123"));
            Assert.Equal(12m, savedProduct.StockSmallUnits);
            var batch = Assert.Single(await repository.GetProductBatchesAsync(product.Id));
            Assert.Equal("LOT-PHASE4-01", batch.BatchCode);
            Assert.Equal(12m, batch.QuantityMinor);
            Assert.Equal(supplier.Id, batch.SupplierId);
            var operations = await repository.GetOperationsAsync(supplier.Id);
            Assert.Contains(operations, x => x.Type == OperationType.DebtToUs && x.Amount == 50m);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
