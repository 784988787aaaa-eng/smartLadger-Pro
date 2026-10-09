using Microsoft.Data.Sqlite;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Backup;
using SmartLedger.Infrastructure.Data;
using Xunit;

namespace SmartLedger.Tests;

public sealed class ResetDataServiceTests
{
    [Fact]
    public async Task Reset_RequiresValidPasswordPhraseAndVerifiedBackup_ThenCanRestoreBackup()
    {
        var root = NewRoot();
        var backupDir = NewRoot();
        var restoreRoot = NewRoot();
        try
        {
            var database = new Database(root);
            await database.InitializeAsync();
            var users = new UserRepository(database);
            var hasher = new PasswordHasher();
            await users.CreateAdministratorAsync(new AdminSetup("admin", hasher.Hash("Valid-password-123"), hasher.Hash("Recovery phrase"), "hint"));
            var admin = await users.FindAsync("admin");
            Assert.NotNull(admin);
            await SeedWorkDataAsync(database, admin!.Id);
            var brandingDirectory = Path.Combine(root, "Branding");
            Directory.CreateDirectory(brandingDirectory);
            var logoPath = Path.Combine(brandingDirectory, "business-logo.png");
            await File.WriteAllBytesAsync(logoPath, new byte[] { 1, 2, 3, 4, 5, 6 });

            var reset = new ResetDataService(database, users, root);
            var backup = await reset.CreateVerifiedBackupAsync(admin.Id, "Valid-password-123", backupDir);
            Assert.True(File.Exists(backup.FilePath));
            await new BackupService(root).VerifyBackupAsync(backup.FilePath);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reset.ResetAsync(admin.Id, "wrong password", ResetDataService.ConfirmationPhrase, backup.FilePath));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reset.ResetAsync(admin.Id, "Valid-password-123", "wrong phrase", backup.FilePath));
            var corrupt = Path.Combine(backupDir, "corrupt.zip");
            await File.WriteAllTextAsync(corrupt, "not a zip");
            await Assert.ThrowsAnyAsync<Exception>(() => reset.ResetAsync(admin.Id, "Valid-password-123", ResetDataService.ConfirmationPhrase, corrupt));
            Assert.True(await CountAsync(database, "Operations") > 0);

            await reset.ResetAsync(admin.Id, "Valid-password-123", ResetDataService.ConfirmationPhrase, backup.FilePath);
            foreach (var table in new[] { "Users", "UserPermissions", "People", "Operations", "Products", "StockMovements", "Invoices", "InvoiceLines", "Expenses", "AuditLogs", "DeletedItems" })
                Assert.Equal(0, await CountAsync(database, table));
            Assert.Equal(5, await CountAsync(database, "Books"));
            Assert.Equal(1, await CountAsync(database, "BusinessIdentity"));
            Assert.Equal(2, await CountAsync(database, "MessageTemplates"));
            Assert.False(File.Exists(logoPath));
            Assert.False(await users.HasAdministratorAsync());
            Assert.Null(await users.FindAsync("admin"));
            const string newPassword = "Fresh-admin-password-123";
            await users.CreateAdministratorAsync(new AdminSetup("freshadmin", hasher.Hash(newPassword), hasher.Hash("Fresh recovery phrase"), "fresh hint"));
            Assert.True(await users.HasAdministratorAsync());
            var freshAdmin = await users.FindAsync("freshadmin");
            Assert.NotNull(freshAdmin);
            Assert.True(PasswordHasher.Verify(newPassword, freshAdmin!.PasswordHash));
            Assert.Null(await users.FindAsync("admin"));

            Directory.CreateDirectory(restoreRoot);
            var restore = new BackupService(restoreRoot);
            await restore.RestoreAsync(backup.FilePath);
            Assert.True(File.Exists(Path.Combine(restoreRoot, "Branding", "business-logo.png")));
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6 }, await File.ReadAllBytesAsync(Path.Combine(restoreRoot, "Branding", "business-logo.png")));
            await using var restored = new Database(restoreRoot).Open();
            await restored.OpenAsync();
            await using var verify = restored.CreateCommand();
            verify.CommandText = "SELECT (SELECT COUNT(*) FROM Users), (SELECT COUNT(*) FROM Operations), (SELECT COUNT(*) FROM AuditLogs), PRAGMA_USER_VERSION();";
            // Read critical pre-reset data directly; restore has to bring the old identity and work records back.
            verify.CommandText = "SELECT (SELECT COUNT(*) FROM Users), (SELECT COUNT(*) FROM Operations), (SELECT COUNT(*) FROM AuditLogs);";
            await using var reader = await verify.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(1L, reader.GetInt64(0));
            Assert.True(reader.GetInt64(1) > 0);
            Assert.True(reader.GetInt64(2) > 0);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteRoot(root);
            DeleteRoot(backupDir);
            DeleteRoot(restoreRoot);
        }
    }

    [Fact]
    public async Task Reset_RejectsNonAdministratorEvenWhenPasswordIsCorrect()
    {
        var root = NewRoot();
        var backupDir = NewRoot();
        try
        {
            var database = new Database(root);
            await database.InitializeAsync();
            var users = new UserRepository(database);
            var hasher = new PasswordHasher();
            await users.CreateAdministratorAsync(new AdminSetup("admin", hasher.Hash("Admin-password-123"), hasher.Hash("Recovery phrase"), "hint"));
            var regular = new User(Guid.NewGuid(), "worker", "Worker", hasher.Hash("Worker-password-123"), hasher.Hash("Recovery phrase"), "hint", false, true);
            await users.CreateUserAsync(regular);
            var reset = new ResetDataService(database, users, root);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reset.CreateVerifiedBackupAsync(regular.Id, "Worker-password-123", backupDir));
            Assert.Equal(2, await CountAsync(database, "Users"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteRoot(root);
            DeleteRoot(backupDir);
        }
    }

    private static async Task SeedWorkDataAsync(Database database, Guid adminId)
    {
        await using var connection = database.Open();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Books VALUES('11111111111111111111111111111111','Custom book',3,20,0,'#000000','folder');
            UPDATE BusinessIdentity SET BusinessName='Test Company',Phone='123';
            INSERT INTO People(Id,BookId,Name,Phone,Note,CreatedAt) VALUES('22222222222222222222222222222222','00000000000000000000000000000002','Test person','','', $now);
            INSERT INTO Operations(Id,PersonId,Type,Amount,Note,OccurredAt,CreatedBy) VALUES('33333333333333333333333333333333','22222222222222222222222222222222',0,'10','test',$now,$admin);
            INSERT INTO Products(Id,Name,Barcode,Category,LargeUnit,SmallUnit,ConversionFactor,PurchasePrice,SalePrice,StockSmallUnits,AlertThreshold,CreatedAt) VALUES('44444444444444444444444444444444','Test product','','','box','unit','12','2','3','5','1',$now);
            INSERT INTO Invoices(Id,Type,PersonId,IssuedAt,Subtotal,Discount,Total,Paid,PaymentMode,Note,CreatedBy) VALUES('55555555555555555555555555555555',0,'22222222222222222222222222222222',$now,'3','0','3','0',0,'test',$admin);
            INSERT INTO InvoiceLines(Id,InvoiceId,ProductId,Description,Quantity,Unit,UnitPrice,Discount,LineTotal) VALUES('66666666666666666666666666666666','55555555555555555555555555555555','44444444444444444444444444444444','Test product','1','unit','3','0','3');
            INSERT INTO StockMovements(Id,ProductId,Type,QuantitySmallUnits,UnitPrice,Note,OccurredAt,CreatedBy) VALUES('77777777777777777777777777777777','44444444444444444444444444444444',0,'1','2','test',$now,$admin);
            INSERT INTO Expenses(Id,PersonId,Category,Amount,Note,OccurredAt,CreatedBy) VALUES('88888888888888888888888888888888',NULL,'Other','1','test',$now,$admin);
            INSERT INTO AuditLogs VALUES('99999999999999999999999999999999',$admin,'TEST','Test',NULL,'{}',$now);
            INSERT INTO DeletedItems VALUES('aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa','Operation','33333333333333333333333333333333',$now,$admin,'{}');
            INSERT INTO MessageTemplates VALUES('bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb','custom template','custom body',0);
            """;
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$admin", adminId.ToString("N"));
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountAsync(Database database, string table)
    {
        await using var connection = database.Open();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM \"" + table + "\";";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "SL_Reset_" + Guid.NewGuid().ToString("N"));
    private static void DeleteRoot(string path) { SqliteConnection.ClearAllPools(); if (Directory.Exists(path)) Directory.Delete(path, true); }
}