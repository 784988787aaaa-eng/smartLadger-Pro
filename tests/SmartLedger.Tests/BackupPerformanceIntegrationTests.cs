using Microsoft.Data.Sqlite;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Backup;
using SmartLedger.Infrastructure.Data;
using System.IO.Compression;
using Xunit;

namespace SmartLedger.Tests;

public sealed class BackupPerformanceIntegrationTests
{
    [Fact]
    public async Task BackupAndRestore_PreserveLedgerAndInventoryRows_AndPassIntegrityCheck()
    {
        var root = Path.Combine(Path.GetTempPath(), "SL_BackupIntegration_" + Guid.NewGuid().ToString("N"));
        var backupFolder = Path.Combine(root, "backups");
        try
        {
            var database = new Database(root);
            await database.InitializeAsync();
            var users = new UserRepository(database);
            var hasher = new PasswordHasher();
            await users.CreateAdministratorAsync(new AdminSetup("admin", hasher.Hash("Password!123"), hasher.Hash("recovery"), "hint"));
            var admin = await users.FindAsync("admin");
            Assert.NotNull(admin);
            var repo = new AppRepository(database);
            var customerBook = (await repo.GetBooksAsync()).First(x => x.Kind == BookKind.Customers);
            var person = await repo.CreatePersonAsync(customerBook.Id, "Backup integration customer", "700001", "test fixture");
            await repo.CreateOperationAsync(new LedgerOperation(Guid.NewGuid(), person.Id, OperationType.DebtToUs, 125.50m, "backup ledger fixture", DateTimeOffset.UtcNow, admin!.Id));
            var product = await repo.CreateProductAsync(new Product(Guid.NewGuid(), "Backup integration item", "BACKUP-001", "test", "box", "piece", 12, 5, 9, 40, 1, DateTimeOffset.UtcNow));
            await repo.CreateStockMovementAsync(new StockMovement(Guid.NewGuid(), product.Id, StockMovementType.Purchase, 6, 5, "backup stock fixture", DateTimeOffset.UtcNow, admin.Id));

            var service = new BackupService(root);
            var backup = await service.CreateAsync(backupFolder);
            Assert.True(File.Exists(backup.FilePath));
            Assert.True(new FileInfo(backup.FilePath).Length > 0);
            Assert.Equal(18, backup.Schema);
            Assert.Equal(backup.Sha256, await service.VerifyBackupAsync(backup.FilePath));

            var extract = Path.Combine(root, "verified-snapshot");
            ZipFile.ExtractToDirectory(backup.FilePath, extract);
            var snapshotDb = Path.Combine(extract, "smartledger.db");
            Assert.Equal("ok", await IntegrityAsync(snapshotDb));
            var originalCounts = await ReadCountsAsync(snapshotDb);

            await using (var c = database.Open())
            {
                await c.OpenAsync();
                await using var q = c.CreateCommand();
                q.CommandText = "UPDATE BusinessIdentity SET BusinessName='post-backup mutation' WHERE Id=1; DELETE FROM StockMovements; DELETE FROM Operations;";
                await q.ExecuteNonQueryAsync();
            }

            await service.RestoreAsync(backup.FilePath);
            var liveDb = Path.Combine(root, "smartledger.db");
            Assert.Equal("ok", await IntegrityAsync(liveDb));
            Assert.Equal(originalCounts, await ReadCountsAsync(liveDb));
            Assert.NotEqual("post-backup mutation", await ReadBusinessNameAsync(liveDb));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Database_IndexesLedgerInvoiceAndInventoryLookupPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "SL_IndexAudit_" + Guid.NewGuid().ToString("N"));
        try
        {
            var db = new Database(root);
            await db.InitializeAsync();
            await using var c = db.Open();
            await c.OpenAsync();
            await using var q = c.CreateCommand();
            q.CommandText = "SELECT name FROM sqlite_master WHERE type='index';";
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await q.ExecuteReaderAsync();
            while (await reader.ReadAsync()) names.Add(reader.GetString(0));
            Assert.Contains("IX_Invoices_Type_Person_IssuedAt", names);
            Assert.Contains("IX_InvoiceLines_ProductId", names);
            Assert.Contains("IX_StockMovements_Product_OccurredAt", names);
            Assert.Contains("IX_Operations_Person_OccurredAt", names);
            Assert.Contains("UX_Invoices_InvoiceNumber", names);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static async Task<(long People, long Operations, long Products, long Movements)> ReadCountsAsync(string path)
    {
        await using var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        await c.OpenAsync();
        async Task<long> Count(string table)
        {
            await using var q = c.CreateCommand();
            q.CommandText = $"SELECT COUNT(*) FROM {table};";
            return Convert.ToInt64(await q.ExecuteScalarAsync());
        }
        return (await Count("People"), await Count("Operations"), await Count("Products"), await Count("StockMovements"));
    }

    private static async Task<string> IntegrityAsync(string path)
    {
        await using var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        await c.OpenAsync();
        await using var q = c.CreateCommand();
        q.CommandText = "PRAGMA integrity_check;";
        return Convert.ToString(await q.ExecuteScalarAsync()) ?? string.Empty;
    }

    private static async Task<string> ReadBusinessNameAsync(string path)
    {
        await using var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        await c.OpenAsync();
        await using var q = c.CreateCommand();
        q.CommandText = "SELECT BusinessName FROM BusinessIdentity WHERE Id=1;";
        return Convert.ToString(await q.ExecuteScalarAsync()) ?? string.Empty;
    }
}
