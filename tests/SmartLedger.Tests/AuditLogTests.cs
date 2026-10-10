using SmartLedger.Infrastructure.Audit;
using SmartLedger.Infrastructure.Data;
using Xunit;

namespace SmartLedger.Tests;

public sealed class AuditLogTests
{
    [Fact]
    public async Task LogActionAsync_PersistsReadableAuditEntry()
    {
        var root = Path.Combine(Path.GetTempPath(), "SmartLedger-Audit-" + Guid.NewGuid().ToString("N"));
        try
        {
            var database = new Database(root);
            await database.InitializeAsync();
            var audit = new AuditRepository(database);
            var entityId = Guid.NewGuid();

            await audit.LogActionAsync(null, "PHASE4_TEST", "Expense", entityId, new { Description = "اختبار سجل التدقيق", Amount = 125m });
            var entries = await audit.SearchAsync(action: "PHASE4_TEST", entityType: "Expense");

            var entry = Assert.Single(entries);
            Assert.Equal(entityId, entry.EntityId);
            Assert.Contains("اختبار سجل التدقيق", entry.Details);
            Assert.Contains("125", entry.Details);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
