using Microsoft.Data.Sqlite;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Data;
using Xunit;

namespace SmartLedger.Tests;

public sealed class SalesInvoiceTests
{
    [Fact]
    public async Task SaveSalesInvoice_NumbersInvoiceDeductsStockFefoAndPostsCustomerDebitAtomically()
    {
        var root=Path.Combine(Path.GetTempPath(),"SL_SALE_"+Guid.NewGuid().ToString("N"));
        try
        {
            var database=new Database(root);
            await database.InitializeAsync();
            var users=new UserRepository(database);
            var hasher=new PasswordHasher();
            await users.CreateAdministratorAsync(new AdminSetup("salesadmin",hasher.Hash("Password!123"),hasher.Hash("recovery"),"hint"));
            var admin=await users.FindAsync("salesadmin");
            Assert.NotNull(admin);
            var repository=new AppRepository(database);
            var customerBook=(await repository.GetBooksAsync()).Single(x=>x.Kind==BookKind.Customers);
            var customer=await repository.CreatePersonAsync(customerBook.Id,"عميل اختبار المبيعات","7770000","");
            var product=await repository.CreateProductAsync(new Product(Guid.NewGuid(),"صنف اختبار البيع","SALE-001","عام","كرتون","حبة",12m,2m,3m,30m,2m,DateTimeOffset.UtcNow));
            await repository.AddStockBatchAsync(new ProductBatch(Guid.NewGuid(),product.Id,"EARLY",DateTimeOffset.UtcNow.AddMonths(1),10m,1.5m,null,"PUR-1",DateTimeOffset.UtcNow.AddDays(-2)));
            await repository.AddStockBatchAsync(new ProductBatch(Guid.NewGuid(),product.Id,"LATER",DateTimeOffset.UtcNow.AddMonths(4),20m,1.8m,null,"PUR-2",DateTimeOffset.UtcNow.AddDays(-1)));
            var invoice=new Invoice(Guid.NewGuid(),InvoiceType.Sale,customer.Id,DateTimeOffset.UtcNow,36m,0m,36m,0m,PaymentMode.Credit,"اختبار بيع ذري",admin!.Id);
            var line=new InvoiceLine(Guid.NewGuid(),invoice.Id,product.Id,product.Name,12m,product.SmallUnit,3m,0m,36m)
            {
                Barcode=product.Barcode,UnitSelected=product.SmallUnit,ConversionFactorApplied=1m,QuantityMinor=12m,CostPriceSnapshot=product.PurchasePrice
            };
            var movement=new StockMovement(Guid.NewGuid(),product.Id,StockMovementType.Sale,12m,3m,"اختبار بيع ذري",DateTimeOffset.UtcNow,admin.Id);
            var operation=new LedgerOperation(Guid.NewGuid(),customer.Id,OperationType.DebtOnUs,36m,"فاتورة بيع",DateTimeOffset.UtcNow,admin.Id);
            var saved=await repository.SaveSalesInvoiceAsync(invoice,new[]{line},new[]{movement},operation);
            Assert.StartsWith($"INV-{DateTimeOffset.UtcNow:yyyy}-",saved.InvoiceNumber,StringComparison.Ordinal);
            Assert.Equal(18m,Assert.Single(await repository.GetProductsAsync(product.Name)).StockSmallUnits);
            var batches=await repository.GetProductBatchesAsync(product.Id);
            Assert.Equal(0m,batches.Single(x=>x.BatchNumber=="EARLY").QuantityMinor);
            Assert.Equal(18m,batches.Single(x=>x.BatchNumber=="LATER").QuantityMinor);
            Assert.Equal(36m,Assert.Single(await repository.GetOperationsAsync(customer.Id)).Amount);
            await using var connection=database.Open();
            await connection.OpenAsync();
            await using var command=connection.CreateCommand();
            command.CommandText="SELECT COUNT(*) FROM CashRegisterEntries WHERE InvoiceId=$invoice;";
            command.Parameters.AddWithValue("$invoice",saved.Id.ToString("N"));
            Assert.Equal(0L,Convert.ToInt64(await command.ExecuteScalarAsync()));
            command.CommandText="SELECT InvoiceNumber FROM Invoices WHERE Id=$invoice;";
            Assert.Equal(saved.InvoiceNumber,Convert.ToString(await command.ExecuteScalarAsync()));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
    }

    [Fact]
    public async Task SaveSalesInvoice_CashSaleWritesCashRegisterEntry()
    {
        var root=Path.Combine(Path.GetTempPath(),"SL_CASH_"+Guid.NewGuid().ToString("N"));
        try
        {
            var database=new Database(root);
            await database.InitializeAsync();
            var users=new UserRepository(database);
            var hasher=new PasswordHasher();
            await users.CreateAdministratorAsync(new AdminSetup("cashadmin",hasher.Hash("Password!123"),hasher.Hash("recovery"),"hint"));
            var admin=await users.FindAsync("cashadmin");
            Assert.NotNull(admin);
            var repository=new AppRepository(database);
            var product=await repository.CreateProductAsync(new Product(Guid.NewGuid(),"صنف نقدي","CASH-001","عام","علبة","حبة",6m,1m,2m,10m,1m,DateTimeOffset.UtcNow));
            var invoice=new Invoice(Guid.NewGuid(),InvoiceType.Sale,null,DateTimeOffset.UtcNow,4m,0m,4m,4m,PaymentMode.Cash,"",admin!.Id);
            var line=new InvoiceLine(Guid.NewGuid(),invoice.Id,product.Id,product.Name,2m,product.SmallUnit,2m,0m,4m){QuantityMinor=2m,UnitSelected=product.SmallUnit,CostPriceSnapshot=product.PurchasePrice};
            var movement=new StockMovement(Guid.NewGuid(),product.Id,StockMovementType.Sale,2m,2m,"",DateTimeOffset.UtcNow,admin.Id);
            var saved=await repository.SaveSalesInvoiceAsync(invoice,new[]{line},new[]{movement},null);
            await using var connection=database.Open();
            await connection.OpenAsync();
            await using var command=connection.CreateCommand();
            command.CommandText="SELECT Amount,EntryType FROM CashRegisterEntries WHERE InvoiceId=$invoice;";
            command.Parameters.AddWithValue("$invoice",saved.Id.ToString("N"));
            await using var reader=await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("4",reader.GetString(0));
            Assert.Equal("SALE_RECEIPT",reader.GetString(1));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
    }
}
