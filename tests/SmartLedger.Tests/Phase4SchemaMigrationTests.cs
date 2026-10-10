using Microsoft.Data.Sqlite;
using SmartLedger.Infrastructure.Data;
using Xunit;

namespace SmartLedger.Tests;

public sealed class Phase4SchemaMigrationTests
{
    [Fact]
    public async Task Initialize_AddsExpenseCashFlowAndPreferencesWithoutDroppingLegacyTables()
    {
        var root = Path.Combine(Path.GetTempPath(), "SmartLedger-Phase4-" + Guid.NewGuid().ToString("N"));
        try
        {
            var database = new Database(root);
            await database.InitializeAsync();
            await using var connection = database.Open();
            await connection.OpenAsync();

            await using var versionCommand = connection.CreateCommand();
            versionCommand.CommandText = "PRAGMA user_version;";
            Assert.Equal(14L, Convert.ToInt64(await versionCommand.ExecuteScalarAsync()));

            await using var tableCommand = connection.CreateCommand();
            tableCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('Products','Invoices','InvoiceLines','Expenses','AuditLogs','ExpenseCashEntries','AppPreferences');";
            Assert.Equal(7L, Convert.ToInt64(await tableCommand.ExecuteScalarAsync()));

            await using var columnCommand = connection.CreateCommand();
            columnCommand.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Expenses') WHERE name IN ('CategoryId','PaymentMethod');";
            Assert.Equal(2L, Convert.ToInt32(await columnCommand.ExecuteScalarAsync()));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
