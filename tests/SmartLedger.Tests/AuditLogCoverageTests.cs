using SmartLedger.Infrastructure.Audit;
using SmartLedger.Infrastructure.Data;
using Xunit;

namespace SmartLedger.Tests;

public sealed class AuditLogCoverageTests
{
    [Theory]
    [InlineData("INVOICE_REVERSED", "Invoice")]
    [InlineData("TRANSACTION_DELETED", "Operation")]
    [InlineData("BALANCE_ADJUSTED", "Person")]
    [InlineData("PRODUCT_PRICE_UPDATED", "Product")]
    public async Task RequiredMutationAuditActions_ArePersistedAndSearchable(string action, string entityType)
    {
        var root = Path.Combine(Path.GetTempPath(), "SmartLedger-AuditCoverage-" + Guid.NewGuid().ToString("N"));
        try
        {
            var database = new Database(root);
            await database.InitializeAsync();
            var audit = new AuditRepository(database);
            var entityId = Guid.NewGuid();
            await audit.LogActionAsync(null, action, entityType, entityId, new { Test = true });
            var entries = await audit.SearchAsync(action: action, entityType: entityType);
            var entry = Assert.Single(entries);
            Assert.Equal(action, entry.Action);
            Assert.Equal(entityType, entry.EntityType);
            Assert.Equal(entityId, entry.EntityId);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}