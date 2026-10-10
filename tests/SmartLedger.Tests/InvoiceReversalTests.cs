using SmartLedger.Infrastructure.Data;
using Xunit;

namespace SmartLedger.Tests;

public sealed class InvoiceReversalTests
{
    [Fact]
    public async Task ReverseInvoiceAsync_ReturnsFalseWhenInvoiceDoesNotExist()
    {
        var root = Path.Combine(Path.GetTempPath(), "SmartLedger-InvoiceReversal-" + Guid.NewGuid().ToString("N"));
        try
        {
            var database = new Database(root);
            await database.InitializeAsync();
            var repository = new AppRepository(database);
            var result = await repository.ReverseInvoiceAsync(987654321, "integration test missing invoice", Guid.NewGuid().ToString());
            Assert.False(result);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
