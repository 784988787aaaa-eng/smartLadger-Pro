using Microsoft.Data.Sqlite;
using SmartLedger.Core.Models;
using SmartLedger.Infrastructure.Data;
using Xunit;

namespace SmartLedger.Tests;

public sealed class BatchStockTests
{
    [Fact]
    public async Task AddStockBatch_UpdatesOnHandAndPreservesBatchValuation()
    {
        var root = Path.Combine(Path.GetTempPath(), "SL_BATCH_" + Guid.NewGuid().ToString("N"));
        try
        {
            var db = new Database(root);
            await db.InitializeAsync();
            var repo = new AppRepository(db);
            var product = await repo.CreateProductAsync(new Product(Guid.NewGuid(), "صنف دفعات", "", "عام", "كرتون", "حبة", 12m, 2m, 3m, 0m, 2m, DateTimeOffset.UtcNow));
            await repo.AddStockBatchAsync(new ProductBatch(Guid.NewGuid(), product.Id, "LOT-1", DateTimeOffset.UtcNow.AddMonths(6), 10m, 1.5m, null, "PUR-001", DateTimeOffset.UtcNow));
            await repo.AddStockBatchAsync(new ProductBatch(Guid.NewGuid(), product.Id, "LOT-2", null, 15m, 1.8m, null, "PUR-002", DateTimeOffset.UtcNow));
            var loaded = Assert.Single(await repo.GetProductsAsync("صنف دفعات"));
            Assert.Equal(25m, loaded.StockSmallUnits);
            var batches = await repo.GetProductBatchesAsync(product.Id);
            Assert.Equal(2, batches.Count);
            Assert.Equal(42.0m, batches.Sum(x => x.QuantityMinor * x.CostPrice));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
