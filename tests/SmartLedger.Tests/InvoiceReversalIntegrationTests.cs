using Microsoft.Data.Sqlite;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Data;
using Xunit;

namespace SmartLedger.Tests;

public sealed class InvoiceReversalIntegrationTests
{
    [Fact]
    public async Task ReverseInvoiceAsync_RestoresStock_MarksInvoiceReversed_AndWritesAuditEntry()
    {
        var root = Path.Combine(Path.GetTempPath(), "SL_REVERSAL_" + Guid.NewGuid().ToString("N"));
        try
        {
            var database = new Database(root);
            await database.InitializeAsync();
            var users = new UserRepository(database);
            var hasher = new PasswordHasher();
            await users.CreateAdministratorAsync(new AdminSetup("reversaladmin", hasher.Hash("Password!123"), hasher.Hash("recovery"), "hint"));
            var admin = await users.FindAsync("reversaladmin");
            Assert.NotNull(admin);
            var repository = new AppRepository(database);
            var customerBook = (await repository.GetBooksAsync()).Single(x => x.Kind == BookKind.Customers);
            var customer = await repository.CreatePersonAsync(customerBook.Id, "Reversal Test Customer", "777123456", string.Empty);
            var product = await repository.CreateProductAsync(new Product(Guid.NewGuid(), "Reversal Test Product", "REV-001", "Test", "Box", "Unit", 1m, 1m, 2m, 50m, 1m, DateTimeOffset.UtcNow));
            var invoiceId = Guid.NewGuid();
            const string invoiceNumber = "123456789";
            var invoice = new Invoice(invoiceId, InvoiceType.Sale, customer.Id, DateTimeOffset.UtcNow, 20m, 0m, 20m, 20m, PaymentMode.Cash, "reversal integration", admin!.Id) { InvoiceNumber = invoiceNumber };
            var line = new InvoiceLine(Guid.NewGuid(), invoiceId, product.Id, product.Name, 10m, product.SmallUnit, 2m, 0m, 20m)
            {
                Barcode = product.Barcode, UnitSelected = product.SmallUnit, ConversionFactorApplied = 1m, QuantityMinor = 10m, CostPriceSnapshot = product.PurchasePrice
            };
            var movement = new StockMovement(Guid.NewGuid(), product.Id, StockMovementType.Sale, 10m, 2m, "integration sale", DateTimeOffset.UtcNow, admin.Id);
            await repository.SaveSalesInvoiceAsync(invoice, new[] { line }, new[] { movement }, null);
            Assert.Equal(40m, Assert.Single(await repository.GetProductsAsync(product.Name)).StockSmallUnits);
            var reversed = await repository.ReverseInvoiceAsync(int.Parse(invoiceNumber), "Customer refund", admin.Id.ToString());
            Assert.True(reversed);
            Assert.Equal(50m, Assert.Single(await repository.GetProductsAsync(product.Name)).StockSmallUnits);
            await using var connection = database.Open();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT Status FROM Invoices WHERE Id=$id;";
            command.Parameters.AddWithValue("$id", invoiceId.ToString("N"));
            Assert.Equal("Reversed", Convert.ToString(await command.ExecuteScalarAsync()));
            command.Parameters.Clear();
            command.CommandText = "SELECT COUNT(*) FROM AuditLogs WHERE Action='INVOICE_REVERSED';";
            Assert.True(Convert.ToInt64(await command.ExecuteScalarAsync()) >= 1);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
