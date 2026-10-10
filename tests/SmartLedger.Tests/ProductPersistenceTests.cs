using Microsoft.Data.Sqlite;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Data;
using Xunit;

namespace SmartLedger.Tests;

public sealed class ProductPersistenceTests
{
    [Fact]
    public async Task CreateUpdateAndSearchByBarcodeAndName_RoundTripsCommercialMetadata()
    {
        var root = Path.Combine(Path.GetTempPath(), "SL_INV_" + Guid.NewGuid().ToString("N"));
        try
        {
            var db = new Database(root);
            await db.InitializeAsync();
            var repo = new AppRepository(db);
            var product = await repo.CreateProductAsync(new Product(Guid.NewGuid(), "صنف باركود", "6291234567890", "مواد غذائية", "كرتون", "حبة", 12m, 2m, 3m, 0m, 5m, DateTimeOffset.UtcNow)
            {
                CategoryId = "food",
                SellingPriceMajor = 42m,
                SellingPriceMinor = 3.5m,
                MinimumSellingPrice = 2.25m,
                ReorderPoint = 6m
            });
            var byBarcode = await repo.GetProductByBarcodeAsync(product.Barcode);
            Assert.NotNull(byBarcode);
            Assert.Equal(product.Id, byBarcode!.Id);
            Assert.Single(await repo.GetProductsAsync("مواد غذائية"));
            var updated = await repo.UpdateProductAsync(product with { Name = "صنف محدث", SellingPriceMajor = 48m, ReorderPoint = 7m });
            Assert.Equal("صنف محدث", updated.Name);
            var loaded = Assert.Single(await repo.GetProductsAsync("صنف محدث"));
            Assert.Equal(48m, loaded.SellingPriceMajor);
            Assert.Equal(7m, loaded.ReorderPoint);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task CreateProduct_RejectsDuplicateBarcode()
    {
        var root = Path.Combine(Path.GetTempPath(), "SL_INV_" + Guid.NewGuid().ToString("N"));
        try
        {
            var db = new Database(root);
            await db.InitializeAsync();
            var repo = new AppRepository(db);
            Product Make(string name) => new(Guid.NewGuid(), name, "6290000000001", "عام", "كرتون", "حبة", 12m, 1m, 2m, 0m, 0m, DateTimeOffset.UtcNow);
            await repo.CreateProductAsync(Make("الأول"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => repo.CreateProductAsync(Make("الثاني")));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
