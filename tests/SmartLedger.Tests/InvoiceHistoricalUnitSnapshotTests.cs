using Microsoft.Data.Sqlite;
using SmartLedger.Core.Models;
using SmartLedger.Infrastructure.Data;
using Xunit;

namespace SmartLedger.Tests;

public sealed class InvoiceHistoricalUnitSnapshotTests
{
    [Fact]
    public async Task PurchaseMajorUnit_PersistsHistoricalUnitFactorPriceAndDescriptionAfterProductChanges()
    {
        var root = Path.Combine(Path.GetTempPath(), "SL_UNIT_SNAPSHOT_" + Guid.NewGuid().ToString("N"));
        try
        {
            var database = new Database(root);
            await database.InitializeAsync();
            var userId = Guid.NewGuid();
            await using (var connection = database.Open())
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "INSERT INTO Users(Id,UserName,DisplayName,PasswordHash,RecoveryHash,RecoveryHint,IsAdministrator,IsActive,CreatedAt) VALUES($id,'snapshot-test','Snapshot Test','hash','','',1,1,$created);";
                command.Parameters.AddWithValue("$id", userId.ToString("N"));
                command.Parameters.AddWithValue("$created", DateTimeOffset.UtcNow.ToString("O"));
                await command.ExecuteNonQueryAsync();
            }

            var repo = new AppRepository(database);
            var book = await repo.CreateBookAsync("Snapshot Supplier Book", "#123456", "truck");
            var supplier = await repo.CreatePersonAsync(book.Id, "Supplier Snapshot", "", "");
            var product = await repo.CreateProductAsync(new Product(Guid.NewGuid(), "Original carton product", "SNAP-12", "General",
                "Carton", "Piece", 12m, 10m, 15m, 0m, 1m, DateTimeOffset.UtcNow)
            {
                SellingPriceMajor = 180m,
                SellingPriceMinor = 15m
            });

            var invoiceId = Guid.NewGuid();
            var lineId = Guid.NewGuid();
            var invoice = new Invoice(invoiceId, InvoiceType.Purchase, supplier.Id, DateTimeOffset.UtcNow,
                600m, 0m, 600m, 0m, PaymentMode.Credit, "snapshot purchase", userId)
            {
                InvoiceNumber = "SNAPSHOT-PUR-001",
                CustomerName = supplier.Name
            };
            var line = new InvoiceLine(lineId, invoiceId, product.Id, product.Name, 5m, "Carton", 120m, 0m, 600m)
            {
                UnitSelected = "Carton",
                ConversionFactorApplied = 12m,
                QuantityMinor = 60m,
                CostPriceSnapshot = 10m,
                Barcode = product.Barcode
            };
            var movement = new StockMovement(Guid.NewGuid(), product.Id, StockMovementType.Purchase,
                60m, 10m, "five cartons", DateTimeOffset.UtcNow, userId);
            var operation = new LedgerOperation(Guid.NewGuid(), supplier.Id, OperationType.DebtToUs,
                600m, "credit purchase", DateTimeOffset.UtcNow, userId);

            await repo.CreateInvoiceTransactionWithMovementsAsync(invoice, new[] { line }, new[] { movement }, operation);

            // Unit semantics are locked once stock or invoices exist; names and prices remain editable.
            await Assert.ThrowsAsync<InvalidOperationException>(() => repo.UpdateProductAsync(product with
            {
                SmallUnit = "Tablet",
                LargeUnit = "Case",
                ConversionFactor = 24m
            }));
            await repo.UpdateProductAsync(product with
            {
                Name = "Renamed product",
                PurchasePrice = 99m,
                SalePrice = 250m,
                SellingPriceMajor = 250m,
                SellingPriceMinor = 12m
            });

            await using var db = database.Open();
            await db.OpenAsync();
            await using var query = db.CreateCommand();
            query.CommandText = "SELECT Description,Unit,UnitSelected,ConversionFactorApplied,Quantity,QuantityMinor,UnitPrice,CostPriceSnapshot,Barcode FROM InvoiceLines WHERE Id=$line;";
            query.Parameters.AddWithValue("$line", lineId.ToString("N"));
            await using var reader = await query.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("Original carton product", reader.GetString(0));
            Assert.Equal("Carton", reader.GetString(1));
            Assert.Equal("Carton", reader.GetString(2));
            Assert.Equal("12", reader.GetString(3));
            Assert.Equal("5", reader.GetString(4));
            Assert.Equal("60", reader.GetString(5));
            Assert.Equal("120", reader.GetString(6));
            Assert.Equal("10", reader.GetString(7));
            Assert.Equal(product.Barcode, reader.GetString(8));
            await reader.DisposeAsync();

            // Retrying the same invoice identity must fail without applying inventory twice.
            await Assert.ThrowsAnyAsync<Exception>(() =>
                repo.CreateInvoiceTransactionWithMovementsAsync(invoice, new[] { line }, new[] { movement }, operation));
            Assert.Equal(60m, Assert.Single(await repo.GetProductsAsync("SNAP-12")).StockSmallUnits);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
