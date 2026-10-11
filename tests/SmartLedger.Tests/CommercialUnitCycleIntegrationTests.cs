using Microsoft.Data.Sqlite;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Data;
using Xunit;

namespace SmartLedger.Tests;

public sealed class CommercialUnitCycleIntegrationTests
{
    [Fact]
    public async Task PurchaseFiveCartonsThenPartiallyPaidMixedUnitSale_PreservesStockLedgersCashAndCogs()
    {
        var root = Path.Combine(Path.GetTempPath(), "SL_UNIT_CYCLE_" + Guid.NewGuid().ToString("N"));
        try
        {
            var database = new Database(root);
            await database.InitializeAsync();
            var users = new UserRepository(database);
            var hasher = new PasswordHasher();
            await users.CreateAdministratorAsync(new AdminSetup("unitcycleadmin", hasher.Hash("Password!123"), hasher.Hash("recovery"), "hint"));
            var admin = await users.FindAsync("unitcycleadmin");
            Assert.NotNull(admin);

            var repo = new AppRepository(database);
            var books = await repo.GetBooksAsync();
            var supplier = await repo.CreatePersonAsync(books.Single(x => x.Kind == BookKind.Suppliers).Id, "مورد دورة الوحدات", "", "");
            var customer = await repo.CreatePersonAsync(books.Single(x => x.Kind == BookKind.Customers).Id, "عميل دورة الوحدات", "", "");
            var product = await repo.CreateProductAsync(new Product(Guid.NewGuid(), "صنف كرتون وحبة", "UNIT-CYCLE-12", "عام",
                "كرتون", "حبة", 12m, 10m, 15m, 0m, 1m, DateTimeOffset.UtcNow));

            // Purchase: 5 cartons × 12 = 60 base units, 120 per carton = 10 per piece, all on credit.
            var purchaseId = Guid.NewGuid();
            var purchase = new Invoice(purchaseId, InvoiceType.Purchase, supplier.Id, DateTimeOffset.UtcNow,
                600m, 0m, 600m, 0m, PaymentMode.Credit, "اختبار دورة الوحدات", admin!.Id);
            var purchaseLine = new InvoiceLine(Guid.NewGuid(), purchaseId, product.Id, product.Name, 5m, "كرتون", 120m, 0m, 600m)
            {
                QuantityMinor = InventoryUnitConverter.ToMinorUnits(5m, 0m, 12m),
                ConversionFactorApplied = 12m,
                UnitSelected = "كرتون",
                CostPriceSnapshot = 10m
            };
            var purchaseMovement = new StockMovement(Guid.NewGuid(), product.Id, StockMovementType.Purchase, 60m, 10m,
                "شراء خمس كراتين", DateTimeOffset.UtcNow, admin.Id);
            await repo.CreateInvoiceTransactionWithMovementsAsync(purchase, new[] { purchaseLine }, new[] { purchaseMovement },
                new LedgerOperation(Guid.NewGuid(), supplier.Id, OperationType.DebtToUs, 600m, "شراء آجل", DateTimeOffset.UtcNow, admin.Id));

            Assert.Equal(60m, Assert.Single(await repo.GetProductsAsync("UNIT-CYCLE-12")).StockSmallUnits);
            Assert.Equal(10m, Assert.Single(await repo.GetProductBatchesAsync(product.Id)).CostPrice);
            Assert.Equal(60m, Assert.Single(await repo.GetProductBatchesAsync(product.Id)).QuantityMinor);
            Assert.Equal(-600m, LedgerBalanceCalculator.CalculateNetBalance(await repo.GetOperationsAsync(supplier.Id, 1000)));

            // Sale: one carton (12) plus three pieces, total 15 pieces; sale 180 + 45 = 225, 100 received.
            var saleId = Guid.NewGuid();
            var sale = new Invoice(saleId, InvoiceType.Sale, customer.Id, DateTimeOffset.UtcNow,
                225m, 0m, 225m, 100m, PaymentMode.Partial, "كرتون وثلاث حبات", admin.Id);
            var cartonLine = new InvoiceLine(Guid.NewGuid(), saleId, product.Id, product.Name, 1m, "كرتون", 180m, 0m, 180m)
            {
                QuantityMinor = InventoryUnitConverter.ToMinorUnits(1m, 0m, 12m),
                ConversionFactorApplied = 12m,
                UnitSelected = "كرتون",
                CostPriceSnapshot = 10m
            };
            var piecesLine = new InvoiceLine(Guid.NewGuid(), saleId, product.Id, product.Name, 3m, "حبة", 15m, 0m, 45m)
            {
                QuantityMinor = InventoryUnitConverter.ToMinorUnits(0m, 3m, 12m),
                ConversionFactorApplied = 1m,
                UnitSelected = "حبة",
                CostPriceSnapshot = 10m
            };
            var saleMovements = new[]
            {
                new StockMovement(Guid.NewGuid(), product.Id, StockMovementType.Sale, 12m, 15m, "بيع كرتون", DateTimeOffset.UtcNow, admin.Id),
                new StockMovement(Guid.NewGuid(), product.Id, StockMovementType.Sale, 3m, 15m, "بيع حبات", DateTimeOffset.UtcNow, admin.Id)
            };
            await repo.SaveSalesInvoiceAsync(sale, new[] { cartonLine, piecesLine }, saleMovements,
                new LedgerOperation(Guid.NewGuid(), customer.Id, OperationType.DebtOnUs, 125m, "رصيد فاتورة جزئية", DateTimeOffset.UtcNow, admin.Id));

            Assert.Equal(45m, Assert.Single(await repo.GetProductsAsync("UNIT-CYCLE-12")).StockSmallUnits);
            Assert.Equal(125m, LedgerBalanceCalculator.CalculateNetBalance(await repo.GetOperationsAsync(customer.Id, 1000)));
            var cash = await repo.GetCashRegisterSummaryAsync(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            Assert.Equal(100m, cash.CashIn);
            Assert.Equal(0m, cash.CashOut);
            Assert.Equal(100m, cash.Balance);

            await using var connection = database.Open();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COALESCE(SUM(CAST(a.QuantityMinor AS REAL) * CAST(b.CostPrice AS REAL)), 0)
                FROM InvoiceBatchAllocations a
                JOIN ProductBatches b ON b.Id = a.BatchId
                WHERE a.InvoiceId = $invoice;
                """;
            command.Parameters.AddWithValue("$invoice", saleId.ToString("N"));
            Assert.Equal(150m, Convert.ToDecimal(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));

            command.Parameters.Clear();
            command.CommandText = "SELECT SUM(CAST(QuantityMinor AS REAL)) FROM InvoiceBatchAllocations WHERE InvoiceId=$invoice;";
            command.Parameters.AddWithValue("$invoice", saleId.ToString("N"));
            Assert.Equal(15m, Convert.ToDecimal(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));

            command.Parameters.Clear();
            command.CommandText = "SELECT SUM(CAST(CostPriceSnapshot AS REAL) * CAST(QuantityMinor AS REAL)) FROM InvoiceLines WHERE InvoiceId=$invoice;";
            command.Parameters.AddWithValue("$invoice", saleId.ToString("N"));
            Assert.Equal(150m, Convert.ToDecimal(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}

