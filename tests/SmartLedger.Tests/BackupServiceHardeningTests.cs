using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SmartLedger.Infrastructure.Backup;
using SmartLedger.Infrastructure.Data;
using Xunit;

namespace SmartLedger.Tests;

public sealed class BackupServiceHardeningTests
{
    [Fact]
    public async Task Backup_IncludesCommittedWalTransaction_AndPreservesIntegrity()
    {
        var root = NewRoot();
        var backupDir = Path.Combine(root, "backups");
        try
        {
            var db = new Database(root);
            await db.InitializeAsync();

            await using var writer = db.Open();
            await writer.OpenAsync();
            await using var tx = await writer.BeginTransactionAsync();
            await using var command = writer.CreateCommand();
            command.Transaction = (SqliteTransaction)tx;
            command.CommandText = "UPDATE BusinessIdentity SET BusinessName=$name WHERE Id=1;";
            command.Parameters.AddWithValue("$name", "معاملة WAL ملتزمة");
            await command.ExecuteNonQueryAsync();
            await tx.CommitAsync();

            var walPath = Path.Combine(root, "smartledger.db-wal");
            Assert.True(File.Exists(walPath));

            var service = new BackupService(root);
            var backup = await service.CreateAsync(backupDir);

            Assert.True(File.Exists(backup.FilePath));
            var extracted = await ExtractDatabaseAsync(backup.FilePath, root);
            await using var restored = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = extracted,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString());
            await restored.OpenAsync();
            await using var read = restored.CreateCommand();
            read.CommandText = "SELECT BusinessName FROM BusinessIdentity WHERE Id=1;";
            Assert.Equal("معاملة WAL ملتزمة", Convert.ToString(await read.ExecuteScalarAsync()));
            Assert.Equal("ok", await IntegrityAsync(extracted));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Restore_ValidArchive_ReplacesDatabase_ClosesHandles_AndLeavesNoWalSidecars()
    {
        var root = NewRoot();
        var backupDir = Path.Combine(root, "backups");
        try
        {
            var db = new Database(root);
            await db.InitializeAsync();
            var source = Path.Combine(root, "smartledger.db");
            var service = new BackupService(root);

            await using (var c = db.Open())
            {
                await c.OpenAsync();
                await using var q = c.CreateCommand();
                q.CommandText = "UPDATE BusinessIdentity SET BusinessName='الحالة الأولى' WHERE Id=1;";
                await q.ExecuteNonQueryAsync();
            }

            var backup = await service.CreateAsync(backupDir);

            await using (var c = db.Open())
            {
                await c.OpenAsync();
                await using var q = c.CreateCommand();
                q.CommandText = "UPDATE BusinessIdentity SET BusinessName='الحالة الثانية' WHERE Id=1;";
                await q.ExecuteNonQueryAsync();
            }

            await service.RestoreAsync(backup.FilePath);

            Assert.False(File.Exists(source + "-wal"));
            Assert.False(File.Exists(source + "-shm"));
            await using (var exclusive = new FileStream(source, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert.True(exclusive.Length > 0);
            Assert.Equal("ok", await IntegrityAsync(source));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Restore_CorruptArchive_PreservesOriginalAndIntegrity()
    {
        var root = NewRoot();
        var backupDir = Path.Combine(root, "backups");
        try
        {
            var db = new Database(root);
            await db.InitializeAsync();
            var source = Path.Combine(root, "smartledger.db");
            var service = new BackupService(root);
            await service.CreateAsync(backupDir);

            var corrupt = Path.Combine(backupDir, "corrupt.zip");
            await File.WriteAllTextAsync(corrupt, "هذا ليس ملف ZIP صالحاً");

            await Assert.ThrowsAnyAsync<Exception>(() => service.RestoreAsync(corrupt));
            Assert.Equal("ok", await IntegrityAsync(source));
            Assert.True(File.Exists(source));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Restore_HashMismatch_PreservesOriginalAndIntegrity()
    {
        var root = NewRoot();
        var backupDir = Path.Combine(root, "backups");
        try
        {
            var db = new Database(root);
            await db.InitializeAsync();
            var service = new BackupService(root);
            var backup = await service.CreateAsync(backupDir);

            var tampered = Path.Combine(backupDir, "hash-mismatch.zip");
            await using (var source = ZipFile.OpenRead(backup.FilePath))
            using (var target = ZipFile.Open(tampered, ZipArchiveMode.Create))
            {
                foreach (var entry in source.Entries)
                {
                    var newEntry = target.CreateEntry(entry.FullName);
                    await using var input = entry.Open();
                    await using var output = newEntry.Open();
                    if (entry.FullName.Equals("metadata.json", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(input);
                        var json = JsonDocument.Parse(await reader.ReadToEndAsync());
                        var tamperedMetadata = JsonSerializer.Serialize(new
                        {
                            schema = json.RootElement.GetProperty("schema").GetInt32(),
                            createdAt = json.RootElement.GetProperty("createdAt").GetString(),
                            sha256 = new string('0', 64)
                        });
                        await using var writer = new StreamWriter(output);
                        await writer.WriteAsync(tamperedMetadata);
                    }
                    else
                    {
                        await input.CopyToAsync(output);
                    }
                }
            }

            await Assert.ThrowsAsync<InvalidDataException>(() => service.RestoreAsync(tampered));
            Assert.Equal("ok", await IntegrityAsync(Path.Combine(root, "smartledger.db")));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Restore_UnsupportedSchema_PreservesOriginalAndIntegrity()
    {
        var root = NewRoot();
        var backupDir = Path.Combine(root, "backups");
        var work = Path.Combine(root, "schema-work");
        try
        {
            var db = new Database(root);
            await db.InitializeAsync();
            var service = new BackupService(root);
            var backup = await service.CreateAsync(backupDir);

            Directory.CreateDirectory(work);
            ZipFile.ExtractToDirectory(backup.FilePath, work, true);
            var restored = Path.Combine(work, "smartledger.db");

            await using (var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = restored }.ToString()))
            {
                await c.OpenAsync();
                await using var q = c.CreateCommand();
                q.CommandText = "PRAGMA user_version=15;";
                await q.ExecuteNonQueryAsync();
            }

            SqliteConnection.ClearAllPools();
            var hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(restored)));
            var metadata = JsonSerializer.Serialize(new { schema = 15, createdAt = DateTimeOffset.UtcNow, sha256 = hash });
            await File.WriteAllTextAsync(Path.Combine(work, "metadata.json"), metadata);

            var unsupported = Path.Combine(backupDir, "unsupported-schema.zip");
            ZipFile.CreateFromDirectory(work, unsupported);

            await Assert.ThrowsAsync<InvalidDataException>(() => service.RestoreAsync(unsupported));
            Assert.Equal("ok", await IntegrityAsync(Path.Combine(root, "smartledger.db")));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Restore_ReplacementFailure_RestoresEmergencyCopyAndIntegrity()
    {
        var root = NewRoot();
        var backupDir = Path.Combine(root, "backups");
        var calls = 0;
        try
        {
            var db = new Database(root);
            await db.InitializeAsync();
            var source = Path.Combine(root, "smartledger.db");
            await SetBusinessNameAsync(source, "الأصل قبل الفشل");

            var creator = new BackupService(root);
            var backup = await creator.CreateAsync(backupDir);
            await SetBusinessNameAsync(source, "تغيير يجب التراجع عنه");

            var failing = new BackupService(root, (from, to) =>
            {
                calls++;
                if (calls == 1)
                {
                    using var input = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var output = new FileStream(to, FileMode.Create, FileAccess.Write, FileShare.None);
                    input.CopyTo(output);
                    throw new IOException("محاكاة فشل الاستبدال.");
                }

                File.Copy(from, to, true);
            });

            await Assert.ThrowsAsync<IOException>(() => failing.RestoreAsync(backup.FilePath));

            Assert.Equal("ok", await IntegrityAsync(source));
            Assert.Equal("تغيير يجب التراجع عنه", await GetBusinessNameAsync(source));
            Assert.True(Directory.GetFiles(root, "SmartLedger_PreRestore_*.db").Length >= 1);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Restore_InterruptionDuringReplacement_RestoresEmergencyCopyAndIntegrity()
    {
        var root = NewRoot();
        var backupDir = Path.Combine(root, "backups");
        try
        {
            var db = new Database(root);
            await db.InitializeAsync();
            var source = Path.Combine(root, "smartledger.db");
            await SetBusinessNameAsync(source, "الحالة الأصلية");

            var creator = new BackupService(root);
            var backup = await creator.CreateAsync(backupDir);
            await SetBusinessNameAsync(source, "حالة ما قبل الانقطاع");

            var interrupted = new BackupService(root, (_, _) => throw new OperationCanceledException("محاكاة انقطاع منتصف الاستعادة."));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => interrupted.RestoreAsync(backup.FilePath));

            Assert.Equal("ok", await IntegrityAsync(source));
            Assert.Equal("حالة ما قبل الانقطاع", await GetBusinessNameAsync(source));
            Assert.True(Directory.GetFiles(root, "SmartLedger_PreRestore_*.db").Length >= 1);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Restore_CancellationBeforeReplacement_PreservesOriginalAndIntegrity()
    {
        var root = NewRoot();
        var backupDir = Path.Combine(root, "backups");
        try
        {
            var db = new Database(root);
            await db.InitializeAsync();
            var source = Path.Combine(root, "smartledger.db");
            await SetBusinessNameAsync(source, "قبل الإلغاء");

            var creator = new BackupService(root);
            var backup = await creator.CreateAsync(backupDir);
            await SetBusinessNameAsync(source, "قبل بدء الاستعادة");

            using var cts = new CancellationTokenSource();
            var cancelled = new BackupService(root, (_, _) =>
            {
                cts.Cancel();
                throw new OperationCanceledException(cts.Token);
            });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.RestoreAsync(backup.FilePath, cts.Token));

            Assert.Equal("ok", await IntegrityAsync(source));
            Assert.Equal("قبل بدء الاستعادة", await GetBusinessNameAsync(source));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteRoot(root);
        }
    }

    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "SL_Backup_" + Guid.NewGuid().ToString("N"));

    private static void DeleteRoot(string root)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    private static Task<string> ExtractDatabaseAsync(string zip, string root)
    {
        var dir = Path.Combine(root, "extract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        ZipFile.ExtractToDirectory(zip, dir);
        return Task.FromResult(Path.Combine(dir, "smartledger.db"));
    }

    private static async Task<string> IntegrityAsync(string path)
    {
        await using var c = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        await c.OpenAsync();
        await using var q = c.CreateCommand();
        q.CommandText = "PRAGMA integrity_check;";
        return Convert.ToString(await q.ExecuteScalarAsync()) ?? "";
    }

    private static async Task SetBusinessNameAsync(string path, string value)
    {
        await using var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
        await c.OpenAsync();
        await using var q = c.CreateCommand();
        q.CommandText = "UPDATE BusinessIdentity SET BusinessName=$name WHERE Id=1;";
        q.Parameters.AddWithValue("$name", value);
        await q.ExecuteNonQueryAsync();
    }

    private static async Task<string> GetBusinessNameAsync(string path)
    {
        await using var c = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        await c.OpenAsync();
        await using var q = c.CreateCommand();
        q.CommandText = "SELECT BusinessName FROM BusinessIdentity WHERE Id=1;";
        return Convert.ToString(await q.ExecuteScalarAsync()) ?? "";
    }
}
