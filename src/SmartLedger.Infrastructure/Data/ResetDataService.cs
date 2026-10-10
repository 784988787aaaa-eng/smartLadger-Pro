using Microsoft.Data.Sqlite;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using SmartLedger.Core.Models;
using BackupInfo = SmartLedger.Core.Models.BackupInfo;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Backup;
using SmartLedger.Infrastructure.Security;

namespace SmartLedger.Infrastructure.Data;

public sealed class ResetDataService
{
    private readonly Database database;
    private readonly UserRepository users;
    private readonly BackupService backup;
    private readonly string rootPath;
    private static readonly SemaphoreSlim ResetGate = new(1, 1);
    private readonly ConcurrentDictionary<Guid, PreparedBackup> preparedBackups = new();
    private sealed record PreparedBackup(string Path, string DatabaseHash, string Fingerprint, DateTimeOffset ExpiresAt);
    public const string ConfirmationPhrase = "تهيئة البيانات";

    public ResetDataService(Database database, UserRepository users, string? rootPath = null)
    {
        this.database = database;
        this.users = users;
        this.rootPath = Path.GetFullPath(rootPath ?? LocalAppPaths.Root);
        backup = new BackupService(this.rootPath);
    }

    public async Task<BackupInfo> CreateVerifiedBackupAsync(Guid actorId, string password, string directory, CancellationToken ct = default)
    {
        await ValidateAdministratorAsync(actorId, password, ct);
        var fullDirectory = Path.GetFullPath(directory);
        EnsureBackupOutsideDatabaseRoot(fullDirectory);
        var info = await backup.CreateAsync(fullDirectory, ct);
        var verifiedHash = await backup.VerifyBackupAsync(info.FilePath, ct);
        if (!string.Equals(info.Sha256, verifiedHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Backup hash differs from the created snapshot.");
        var fingerprint = await ComputeBackupFingerprintAsync(info.FilePath, ct);
        preparedBackups[actorId] = new PreparedBackup(Path.GetFullPath(info.FilePath), verifiedHash, fingerprint, DateTimeOffset.UtcNow.AddMinutes(30));
        return new BackupInfo(info.FilePath, info.CreatedAt, new FileInfo(info.FilePath).Length, false, info.Sha256);
    }

    public async Task ResetAsync(Guid actorId, string password, string confirmation, string verifiedBackupPath, CancellationToken ct = default)
    {
        if (!string.Equals(confirmation, ConfirmationPhrase, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("The typed confirmation phrase does not match.");
        await ValidateAdministratorAsync(actorId, password, ct);
        if (string.IsNullOrWhiteSpace(verifiedBackupPath) || !File.Exists(verifiedBackupPath)) throw new InvalidDataException("A verified backup is required before reset.");
        if (!preparedBackups.TryGetValue(actorId, out var prepared) || prepared.ExpiresAt < DateTimeOffset.UtcNow || !string.Equals(Path.GetFullPath(verifiedBackupPath), prepared.Path, StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("A freshly prepared backup for this administrator is required.");

        EnsureBackupOutsideDatabaseRoot(Path.GetDirectoryName(Path.GetFullPath(verifiedBackupPath))!);
        var verifiedHash = await backup.VerifyBackupAsync(verifiedBackupPath, ct);
        if (!string.Equals(prepared.DatabaseHash, verifiedHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Prepared backup hash changed.");

        await ResetGate.WaitAsync(ct);
        var committed = false;
        try
        {
            SqliteConnection.ClearAllPools();
            await using (var connection = database.Open())
            {
                await connection.OpenAsync(ct);
                await ExecuteAsync(connection, null, "PRAGMA wal_checkpoint(TRUNCATE);", ct);
                await ExecuteAsync(connection, null, "PRAGMA foreign_keys=OFF;", ct);
                try
                {
                await using (var transaction = await connection.BeginTransactionAsync(ct))
                {
                    var liveFingerprint = await ComputeFingerprintAsync(connection, (SqliteTransaction)transaction, ct);
                    if (!string.Equals(liveFingerprint, prepared.Fingerprint, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Database contents changed after the backup was created. Create a fresh backup before resetting.");
                    var tables = new List<string>();
                    await using (var query = connection.CreateCommand())
                    {
                        query.Transaction = (SqliteTransaction)transaction;
                        query.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
                        await using var reader = await query.ExecuteReaderAsync(ct);
                        while (await reader.ReadAsync(ct)) tables.Add(reader.GetString(0));
                    }
                    foreach (var table in tables)
                    {
                        var quoted = "\"" + table.Replace("\"", "\"\"") + "\"";
                        await ExecuteAsync(connection, (SqliteTransaction)transaction, $"DELETE FROM {quoted};", ct);
                    }
                    await ExecuteAsync(connection, (SqliteTransaction)transaction, """
                        INSERT INTO Books VALUES
                        ('00000000000000000000000000000001','كل الحسابات',0,0,1,'#00B4D8','all'),
                        ('00000000000000000000000000000002','العملاء',1,1,1,'#06D6A0','users'),
                        ('00000000000000000000000000000003','الموردون',2,2,1,'#FFD166','truck'),
                        ('00000000000000000000000000000004','العاملون',4,3,1,'#9B8AFB','users'),
                        ('00000000000000000000000000000005','أخرى',5,4,1,'#7C8AA5','folder');
                        INSERT INTO BusinessIdentity(Id,BusinessName,LogoPath,Phone,Address,Currency) VALUES(1,'الدفتر الذكي','','','','ريال');
                        INSERT INTO MessageTemplates VALUES
                        ('00000000000000000000000000000001','تذكير رصيد','السلام عليكم {الاسم}، نذكركم بأن الرصيد الحالي هو {الرصيد}. نشكركم.',1),
                        ('00000000000000000000000000000002','تأكيد عملية','السلام عليكم {الاسم}، تم تسجيل العملية رقم {رقم العملية} بتاريخ {التاريخ} بمبلغ {المبلغ}.',1);
                        """, ct);
                    await transaction.CommitAsync(ct);
                    committed = true;
                }
                await ExecuteAsync(connection, null, "PRAGMA foreign_keys=ON;", ct);
                await ExecuteAsync(connection, null, "PRAGMA wal_checkpoint(TRUNCATE);", ct);
                await VerifyResetStateAsync(connection, ct);
                }
                finally
                {
                    try { await ExecuteAsync(connection, null, "PRAGMA foreign_keys=ON;", CancellationToken.None); } catch { }
                }
            }
            SqliteConnection.ClearAllPools();
            var brandingDirectory = Path.Combine(rootPath, "Branding");
            if (Directory.Exists(brandingDirectory)) foreach (var logo in Directory.EnumerateFiles(brandingDirectory, "business-logo.*")) File.Delete(logo);
            var rememberedLogin = Path.Combine(rootPath, "remembered-login.bin");
            if (File.Exists(rememberedLogin)) File.Delete(rememberedLogin);
            preparedBackups.TryRemove(actorId, out _);
        }
        catch
        {
            SqliteConnection.ClearAllPools();
            if (committed)
            {
                try { await backup.RestoreAsync(verifiedBackupPath, CancellationToken.None); }
                catch (Exception recoveryError) { throw new InvalidOperationException("Reset verification failed and automatic recovery also failed. Preserve the verified backup at: " + verifiedBackupPath, recoveryError); }
            }
            throw;
        }
        finally { ResetGate.Release(); }
    }


    private static async Task<string> ComputeBackupFingerprintAsync(string archivePath, CancellationToken ct)
    {
        var temp = Path.Combine(Path.GetTempPath(), "SmartLedgerFingerprint_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            ZipFile.ExtractToDirectory(archivePath, temp, true);
            var dbPath = Path.Combine(temp, "smartledger.db");
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            await connection.OpenAsync(ct);
            return await ComputeFingerprintAsync(connection, null, ct);
        }
        finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
    }

    private static async Task<string> ComputeFingerprintAsync(SqliteConnection connection, SqliteTransaction? transaction, CancellationToken ct)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Add(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            hash.AppendData(BitConverter.GetBytes(bytes.Length));
            hash.AppendData(bytes);
        }
        var tables = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) tables.Add(reader.GetString(0));
        }
        foreach (var table in tables)
        {
            ct.ThrowIfCancellationRequested();
            var quotedTable = "\"" + table.Replace("\"", "\"\"") + "\"";
            var columns = new List<string>();
            await using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "PRAGMA table_info(" + quotedTable + ");";
                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct)) columns.Add(reader.GetString(1));
            }
            Add("TABLE"); Add(table);
            foreach (var column in columns) { Add("COLUMN"); Add(column); }
            if (columns.Count == 0) continue;
            var order = string.Join(",", columns.Select(c => "\"" + c.Replace("\"", "\"\"") + "\""));
            await using var rows = connection.CreateCommand();
            rows.Transaction = transaction;
            rows.CommandText = "SELECT * FROM " + quotedTable + " ORDER BY " + order + ";";
            await using var data = await rows.ExecuteReaderAsync(ct);
            while (await data.ReadAsync(ct))
            {
                Add("ROW");
                for (var i = 0; i < data.FieldCount; i++)
                {
                    var value = data.GetValue(i);
                    if (value is DBNull) Add("NULL");
                    else if (value is byte[] blob) Add("BLOB:" + Convert.ToBase64String(blob));
                    else if (value is string text) Add("TEXT:" + text);
                    else if (value is long integer) Add("INTEGER:" + integer.ToString(CultureInfo.InvariantCulture));
                    else if (value is double real) Add("REAL:" + real.ToString("R", CultureInfo.InvariantCulture));
                    else Add(value.GetType().FullName + ":" + Convert.ToString(value, CultureInfo.InvariantCulture));
                }
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    private async Task ValidateAdministratorAsync(Guid actorId, string password, CancellationToken ct)
    {
        var actor = await users.FindAsync((await FindUserNameAsync(actorId, ct)) ?? string.Empty, ct);
        if (actor is null || actor.Id != actorId || !actor.IsActive || !actor.IsAdministrator || !PasswordHasher.Verify(password ?? string.Empty, actor.PasswordHash))
            throw new UnauthorizedAccessException("Administrator identity or password verification failed.");
        await using var connection = database.Open();
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM UserPermissions WHERE UserId=$id AND Permission=$permission);";
        command.Parameters.AddWithValue("$id", actorId.ToString("N"));
        command.Parameters.AddWithValue("$permission", SmartLedger.Core.Abstractions.PermissionKeys.ResetData);
        if (Convert.ToInt64(await command.ExecuteScalarAsync(ct)) != 1)
            throw new UnauthorizedAccessException("The administrator lacks the explicit data-reset permission.");
    }

    private async Task<string?> FindUserNameAsync(Guid actorId, CancellationToken ct)
    {
        await using var connection = database.Open();
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT UserName FROM Users WHERE Id=$id LIMIT 1;";
        command.Parameters.AddWithValue("$id", actorId.ToString("N"));
        return Convert.ToString(await command.ExecuteScalarAsync(ct));
    }

    private void EnsureBackupOutsideDatabaseRoot(string directory)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase) || full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Choose a backup location outside the application data folder.");
    }

    private static async Task VerifyResetStateAsync(SqliteConnection connection, CancellationToken ct)
    {
        await using (var check = connection.CreateCommand())
        {
            check.CommandText = "PRAGMA integrity_check;";
            if (!string.Equals(Convert.ToString(await check.ExecuteScalarAsync(ct)), "ok", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("SQLite integrity check failed after reset.");
        }
        await using (var fk = connection.CreateCommand())
        {
            fk.CommandText = "PRAGMA foreign_key_check;";
            await using var reader = await fk.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct)) throw new InvalidDataException("Foreign-key validation failed after reset.");
        }
        foreach (var table in new[] { "Users", "UserPermissions", "People", "Operations", "Products", "StockMovements", "Invoices", "InvoiceLines", "Expenses", "AuditLogs", "DeletedItems" })
        {
            await using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM \"" + table + "\";";
            if (Convert.ToInt64(await count.ExecuteScalarAsync(ct)) != 0) throw new InvalidDataException("Reset verification found remaining rows in " + table + ".");
        }
        await using var version = connection.CreateCommand();
        version.CommandText = "PRAGMA user_version;";
        if (Convert.ToInt32(await version.ExecuteScalarAsync(ct)) != 14) throw new InvalidDataException("Database schema version changed unexpectedly during reset.");
    }

    private static async Task ExecuteAsync(SqliteConnection connection, SqliteTransaction? transaction, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }
}