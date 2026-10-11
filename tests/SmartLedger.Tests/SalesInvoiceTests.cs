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
            var product=await repository.CreateProductAsync(new Product(Guid.NewGuid(),"صنف اختبار البيع","SALE-001","عام","كرتون","حبة",12m,2m,3m,0m,2m,DateTimeOffset.UtcNow));
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
            var remainingBatch=Assert.Single(batches);
            Assert.Equal("LATER",remainingBatch.BatchNumber);
            Assert.Equal(18m,remainingBatch.QuantityMinor);
            Assert.Equal(36m,Assert.Single(await repository.GetOperationsAsync(customer.Id)).Amount);
            await using var connection=database.Open();
            await connection.OpenAsync();
            await using var command=connection.CreateCommand();
            command.CommandText="SELECT QuantityMinor FROM ProductBatches WHERE BatchCode='EARLY';";
            Assert.Equal("0",Convert.ToString(await command.ExecuteScalarAsync(),System.Globalization.CultureInfo.InvariantCulture));
            command.CommandText="SELECT COUNT(*) FROM InvoiceBatchAllocations WHERE InvoiceId=$invoice;";
            command.Parameters.AddWithValue("$invoice",saved.Id.ToString("N"));
            Assert.Equal(1L,Convert.ToInt64(await command.ExecuteScalarAsync()));
            command.CommandText="SELECT b.BatchCode,a.QuantityMinor FROM ProductBatches b JOIN InvoiceBatchAllocations a ON a.BatchId=b.Id WHERE a.InvoiceId=$invoice;";
            await using(var allocationReader=await command.ExecuteReaderAsync())
            {
                Assert.True(await allocationReader.ReadAsync());
                Assert.Equal("EARLY",allocationReader.GetString(0));
                Assert.Equal("10",allocationReader.GetString(1));
            }
            command.Parameters.Clear();
            command.CommandText="SELECT COUNT(*) FROM CashRegisterEntries WHERE InvoiceId=$invoice;";
            command.Parameters.AddWithValue("$invoice",saved.Id.ToString("N"));
            Assert.Equal(0L,Convert.ToInt64(await command.ExecuteScalarAsync()));
            command.CommandText="SELECT InvoiceNumber FROM Invoices WHERE Id=$invoice;";
            Assert.Equal(saved.InvoiceNumber,Convert.ToString(await command.ExecuteScalarAsync()));
            // Exercise the reversal API's current numeric invoice-number contract and verify the
            // exact original FEFO lots are restored, not merely the aggregate product balance.
            command.CommandText="UPDATE Invoices SET InvoiceNumber='123' WHERE Id=$invoice;";
            await command.ExecuteNonQueryAsync();
            Assert.True(await repository.ReverseInvoiceAsync(123,"reversal regression test",admin.Id.ToString("N")));
            Assert.Equal(30m,Assert.Single(await repository.GetProductsAsync(product.Name)).StockSmallUnits);
            var restoredBatches=await repository.GetProductBatchesAsync(product.Id);
            Assert.Equal(2,restoredBatches.Count);
            Assert.Equal(10m,restoredBatches.Single(x=>x.BatchNumber=="EARLY").QuantityMinor);
            Assert.Equal(20m,restoredBatches.Single(x=>x.BatchNumber=="LATER").QuantityMinor);
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
            var invoice=new Invoice(Guid.NewGuid(),InvoiceType.Sale,null,DateTimeOffset.UtcNow,5m,1m,4.5m,4.5m,PaymentMode.Cash,"",admin!.Id){TaxAmount=0.5m};
            var line=new InvoiceLine(Guid.NewGuid(),invoice.Id,product.Id,product.Name,2m,product.SmallUnit,2.5m,1m,4m){QuantityMinor=2m,UnitSelected=product.SmallUnit,CostPriceSnapshot=product.PurchasePrice};
            var movement=new StockMovement(Guid.NewGuid(),product.Id,StockMovementType.Sale,2m,2.5m,"",DateTimeOffset.UtcNow,admin.Id);
            var saved=await repository.SaveSalesInvoiceAsync(invoice,new[]{line},new[]{movement},null);
            await using var connection=database.Open();
            await connection.OpenAsync();
            await using var command=connection.CreateCommand();
            command.CommandText="SELECT Amount,EntryType FROM CashRegisterEntries WHERE InvoiceId=$invoice;";
            command.Parameters.AddWithValue("$invoice",saved.Id.ToString("N"));
            await using var reader=await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("4.5",reader.GetString(0));
            Assert.Equal("SALE_RECEIPT",reader.GetString(1));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
    }
    [Fact]
    public async Task Sale_UsesTrackedBatchCostAndOpeningStockFallback_AndRejectsDuplicateInvoiceNumber()
    {
        var root=Path.Combine(Path.GetTempPath(),"SL_BATCH_COST_"+Guid.NewGuid().ToString("N"));
        try
        {
            var database=new Database(root);
            await database.InitializeAsync();
            var users=new UserRepository(database);
            var hasher=new PasswordHasher();
            await users.CreateAdministratorAsync(new AdminSetup("batchcostadmin",hasher.Hash("Password!123"),hasher.Hash("recovery"),"hint"));
            var admin=await users.FindAsync("batchcostadmin");
            Assert.NotNull(admin);
            var repository=new AppRepository(database);
            var product=await repository.CreateProductAsync(new Product(Guid.NewGuid(),"صنف رصيد افتتاحي","BATCH-COST-01","عام","كرتون","حبة",10m,2m,5m,10m,1m,DateTimeOffset.UtcNow));
            await repository.AddStockBatchAsync(new ProductBatch(Guid.NewGuid(),product.Id,"LOT-COST",DateTimeOffset.UtcNow.AddMonths(2),5m,3m,null,"PUR-COST",DateTimeOffset.UtcNow));
            var invoice=new Invoice(Guid.NewGuid(),InvoiceType.Sale,null,DateTimeOffset.UtcNow,40m,0m,40m,40m,PaymentMode.Cash,"اختبار تكلفة الدفعات",admin!.Id);
            var line=new InvoiceLine(Guid.NewGuid(),invoice.Id,product.Id,product.Name,8m,product.SmallUnit,5m,0m,40m)
            {
                QuantityMinor=8m,UnitSelected=product.SmallUnit,ConversionFactorApplied=1m,CostPriceSnapshot=product.PurchasePrice
            };
            var movement=new StockMovement(Guid.NewGuid(),product.Id,StockMovementType.Sale,8m,5m,"اختبار تكلفة الدفعات",DateTimeOffset.UtcNow,admin.Id);
            var saved=await repository.SaveSalesInvoiceAsync(invoice,new[]{line},new[]{movement},null);
            Assert.Equal(7m,Assert.Single(await repository.GetProductsAsync(product.Name)).StockSmallUnits);
            await using var connection=database.Open();
            await connection.OpenAsync();
            await using var command=connection.CreateCommand();
            command.CommandText="SELECT CostPriceSnapshot FROM InvoiceLines WHERE InvoiceId=$invoice;";
            command.Parameters.AddWithValue("$invoice",saved.Id.ToString("N"));
            Assert.Equal("2.625",Convert.ToString(await command.ExecuteScalarAsync(),System.Globalization.CultureInfo.InvariantCulture));
            command.Parameters.Clear();
            command.CommandText="SELECT SUM(CAST(QuantityMinor AS REAL)) FROM InvoiceBatchAllocations WHERE InvoiceId=$invoice;";
            command.Parameters.AddWithValue("$invoice",saved.Id.ToString("N"));
            Assert.Equal(5m,Convert.ToDecimal(await command.ExecuteScalarAsync(),System.Globalization.CultureInfo.InvariantCulture));

            var duplicate=invoice with { Id=Guid.NewGuid(), IssuedAt=DateTimeOffset.UtcNow, InvoiceNumber=saved.InvoiceNumber, Subtotal=5m, Total=5m, Paid=5m };
            var duplicateLine=new InvoiceLine(Guid.NewGuid(),duplicate.Id,product.Id,product.Name,1m,product.SmallUnit,5m,0m,5m){QuantityMinor=1m,UnitSelected=product.SmallUnit,ConversionFactorApplied=1m};
            var duplicateMovement=new StockMovement(Guid.NewGuid(),product.Id,StockMovementType.Sale,1m,5m,"اختبار تكرار رقم الفاتورة",DateTimeOffset.UtcNow,admin.Id);
            var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>repository.SaveSalesInvoiceAsync(duplicate,new[]{duplicateLine},new[]{duplicateMovement},null));
            Assert.Contains("رقم الفاتورة مستخدم مسبقاً",error.Message);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
    }

    [Fact]
    public async Task ManualReceiptsAndPayments_AffectCashSummaryAndRestoreWithoutDuplication()
    {
        var root=Path.Combine(Path.GetTempPath(),"SL_MANUAL_CASH_"+Guid.NewGuid().ToString("N"));
        try
        {
            var database=new Database(root);
            await database.InitializeAsync();
            var users=new UserRepository(database);
            var hasher=new PasswordHasher();
            await users.CreateAdministratorAsync(new AdminSetup("cashledgeradmin",hasher.Hash("Password!123"),hasher.Hash("recovery"),"hint"));
            var admin=await users.FindAsync("cashledgeradmin");
            Assert.NotNull(admin);
            var repository=new AppRepository(database);
            var books=await repository.GetBooksAsync();
            var customer=await repository.CreatePersonAsync(books.Single(x=>x.Kind==BookKind.Customers).Id,"عميل تسوية نقدية","7771111","");
            var supplier=await repository.CreatePersonAsync(books.Single(x=>x.Kind==BookKind.Suppliers).Id,"مورد تسوية نقدية","7772222","");
            var receipt=new LedgerOperation(Guid.NewGuid(),customer.Id,OperationType.Receive,20m,"قبض نقدي",DateTimeOffset.UtcNow,admin!.Id);
            var payment=new LedgerOperation(Guid.NewGuid(),supplier.Id,OperationType.Payment,7m,"سداد نقدي",DateTimeOffset.UtcNow,admin.Id);
            await repository.CreateOperationAsync(receipt);
            await repository.CreateOperationAsync(payment);

            var summary=await repository.GetCashRegisterSummaryAsync();
            Assert.Equal(20m,summary.CashIn);
            Assert.Equal(7m,summary.CashOut);
            Assert.Equal(13m,summary.Balance);

            await repository.DeleteOperationAsync(receipt.Id,admin.Id);
            summary=await repository.GetCashRegisterSummaryAsync();
            Assert.Equal(0m,summary.CashIn);
            Assert.Equal(7m,summary.CashOut);
            var deleted=(await repository.GetDeletedItemsAsync()).Single(x=>x.EntityType=="Operation"&&x.EntityId==receipt.Id);
            await repository.RestoreDeletedItemAsync(deleted.Id);
            summary=await repository.GetCashRegisterSummaryAsync();
            Assert.Equal(20m,summary.CashIn);
            Assert.Equal(7m,summary.CashOut);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
    }

}
