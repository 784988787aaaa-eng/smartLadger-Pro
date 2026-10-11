using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Audit;
using SmartLedger.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace SmartLedger.Tests;

public sealed class RecycleBinSafetyTests
{
    private static async Task<(string Root, Database Database, AppRepository Repository, User Administrator)> CreateFixture()
    {
        var root = Path.Combine(Path.GetTempPath(), "SL_RECYCLE_G_" + Guid.NewGuid().ToString("N"));
        var database = new Database(root);
        await database.InitializeAsync();
        var users = new UserRepository(database);
        var hasher = new PasswordHasher();
        await users.CreateAdministratorAsync(new AdminSetup("recycle_admin", hasher.Hash("Password!123"), hasher.Hash("recovery"), "hint"));
        var administrator = await users.FindAsync("recycle_admin");
        Assert.NotNull(administrator);
        return (root, database, new AppRepository(database), administrator!);
    }

    [Fact]
    public async Task SoftDeletedPerson_CanBeRestored_AndBothActionsAreAudited()
    {
        var fixture = await CreateFixture();
        try
        {
            var book = (await fixture.Repository.GetBooksAsync()).Single(x => x.Kind == BookKind.Customers);
            var person = await fixture.Repository.CreatePersonAsync(book.Id, "Recycle Restore Customer", "7770001", "test note");
            await fixture.Repository.DeletePersonAsync(person.Id, fixture.Administrator.Id);
            Assert.DoesNotContain(await fixture.Repository.GetPeopleAsync(book.Id), x => x.Person.Id == person.Id);
            var deleted = Assert.Single(await fixture.Repository.GetDeletedItemsAsync(), x => x.EntityType == "Person" && x.EntityId == person.Id);

            await fixture.Repository.RestoreDeletedItemAsync(deleted.Id);
            var restored = Assert.Single(await fixture.Repository.GetPeopleAsync(book.Id), x => x.Person.Id == person.Id);
            Assert.Equal("Recycle Restore Customer", restored.Person.Name);
            Assert.DoesNotContain(await fixture.Repository.GetDeletedItemsAsync(), x => x.Id == deleted.Id);

            var audit = new AuditRepository(fixture.Database);
            var deleteEvent = Assert.Single(await audit.SearchAsync(action: "PERSON_DELETE", entityType: "Person"));
            var restoreEvent = Assert.Single(await audit.SearchAsync(action: "RECYCLE_RESTORE", entityType: "Person"));
            Assert.Equal(person.Id, deleteEvent.EntityId);
            Assert.Equal(person.Id, restoreEvent.EntityId);
            Assert.NotEqual(default, deleteEvent.OccurredAt);
            Assert.NotEqual(default, restoreEvent.OccurredAt);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(fixture.Root)) Directory.Delete(fixture.Root, true);
        }
    }

    [Fact]
    public async Task RestoringStockMovementTwice_DoesNotDuplicateInventoryEffect()
    {
        var fixture = await CreateFixture();
        try
        {
            var product = await fixture.Repository.CreateProductAsync(new Product(Guid.NewGuid(), "Restore Movement Product", "", "", "box", "unit", 12m, 2m, 3m, 10m, 1m, DateTimeOffset.UtcNow));
            var movement = await fixture.Repository.CreateStockMovementAsync(new StockMovement(Guid.NewGuid(), product.Id, StockMovementType.AdjustmentIn, 5m, 2m, "audit restore test", DateTimeOffset.UtcNow, fixture.Administrator.Id));
            Assert.Equal(15m, (await fixture.Repository.GetProductsAsync()).Single(x => x.Id == product.Id).StockSmallUnits);

            await fixture.Repository.DeleteStockMovementAsync(movement.Id, fixture.Administrator.Id);
            Assert.Equal(10m, (await fixture.Repository.GetProductsAsync()).Single(x => x.Id == product.Id).StockSmallUnits);
            var deleted = Assert.Single(await fixture.Repository.GetDeletedItemsAsync(), x => x.EntityType == "StockMovement" && x.EntityId == movement.Id);
            await fixture.Repository.RestoreDeletedItemAsync(deleted.Id);
            Assert.Equal(15m, (await fixture.Repository.GetProductsAsync()).Single(x => x.Id == product.Id).StockSmallUnits);

            await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Repository.RestoreDeletedItemAsync(deleted.Id));
            Assert.Equal(15m, (await fixture.Repository.GetProductsAsync()).Single(x => x.Id == product.Id).StockSmallUnits);
            Assert.Single(await fixture.Repository.GetStockMovementsAsync(product.Id), x => x.Id == movement.Id);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(fixture.Root)) Directory.Delete(fixture.Root, true);
        }
    }

    [Fact]
    public async Task RestoreWithBarcodeConflict_IsRejectedWithoutPartialRestore()
    {
        var fixture = await CreateFixture();
        try
        {
            var original = await fixture.Repository.CreateProductAsync(new Product(Guid.NewGuid(), "Original Product", "G-UNIQUE-001", "", "box", "unit", 12m, 2m, 3m, 0m, 1m, DateTimeOffset.UtcNow));
            await fixture.Repository.DeleteProductAsync(original.Id, fixture.Administrator.Id);
            var other = await fixture.Repository.CreateProductAsync(new Product(Guid.NewGuid(), "Conflicting Product", "G-UNIQUE-001", "", "box", "unit", 12m, 2m, 3m, 0m, 1m, DateTimeOffset.UtcNow));
            var deleted = Assert.Single(await fixture.Repository.GetDeletedItemsAsync(), x => x.EntityType == "Product" && x.EntityId == original.Id);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Repository.RestoreDeletedItemAsync(deleted.Id));
            Assert.Contains("الباركود", error.Message);
            Assert.Contains((await fixture.Repository.GetProductsAsync()).Select(x => x.Id), id => id == other.Id);
            Assert.DoesNotContain((await fixture.Repository.GetProductsAsync()).Select(x => x.Id), id => id == original.Id);
            Assert.Contains(await fixture.Repository.GetDeletedItemsAsync(), x => x.Id == deleted.Id);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(fixture.Root)) Directory.Delete(fixture.Root, true);
        }
    }
}
