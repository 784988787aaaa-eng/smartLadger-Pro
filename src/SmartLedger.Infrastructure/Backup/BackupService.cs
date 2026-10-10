using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace SmartLedger.Infrastructure.Backup;

public sealed record BackupInfo(string FilePath, string Sha256, int Schema, DateTimeOffset CreatedAt);

public sealed class BackupService
{
    private const int SupportedSchema = 13;
    private readonly string rootPath;
    private readonly Action<string, string> replaceFile;

    public BackupService(string rootPath, Action<string, string>? replaceFile = null)
    {
        this.rootPath = Path.GetFullPath(rootPath);
        this.replaceFile = replaceFile ?? ((source, destination) => File.Move(source, destination, true));
    }

    public async Task<BackupInfo> CreateAsync(string directory, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(rootPath);
        var databasePath = Path.Combine(rootPath, "smartledger.db");
        if (!File.Exists(databasePath)) throw new FileNotFoundException("SmartLedger database was not found.", databasePath);

        var createdAt = DateTimeOffset.UtcNow;
        var stamp = createdAt.ToLocalTime().ToString("yyyy-MM-dd_HH-mm-ss");
        var outputPath = Path.Combine(Path.GetFullPath(directory), $"SmartLedger_{stamp}.zip");
        var tempRoot = Path.Combine(Path.GetTempPath(), "SmartLedgerBackup_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var snapshotPath = Path.Combine(tempRoot, "smartledger.db");
            SqliteConnection.ClearAllPools();
            await using (var source = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath, Mode = SqliteOpenMode.ReadWrite, Pooling = false
            }.ToString()))
            {
                await source.OpenAsync(ct);
                await using var command = source.CreateCommand();
                command.CommandText = "PRAGMA wal_checkpoint(FULL);";
                await command.ExecuteNonQueryAsync(ct);
                await using var target = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = snapshotPath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false
                }.ToString());
                await target.OpenAsync(ct);
                source.BackupDatabase(target);
            }

            var hash = await HashFileAsync(snapshotPath, ct);
            var schema = await ReadSchemaAsync(snapshotPath, ct);
            if (schema != SupportedSchema) throw new InvalidDataException($"Unsupported database schema: {schema}.");
            await File.WriteAllTextAsync(Path.Combine(tempRoot, "metadata.json"),
                JsonSerializer.Serialize(new { schema, createdAt, sha256 = hash }), ct);

            var branding = Path.Combine(rootPath, "Branding");
            if (Directory.Exists(branding))
            {
                var targetBranding = Path.Combine(tempRoot, "Branding");
                Directory.CreateDirectory(targetBranding);
                foreach (var file in Directory.EnumerateFiles(branding, "business-logo.*"))
                {
                    ct.ThrowIfCancellationRequested();
                    File.Copy(file, Path.Combine(targetBranding, Path.GetFileName(file)), true);
                }
            }

            if (File.Exists(outputPath)) outputPath = Path.Combine(Path.GetFullPath(directory), $"SmartLedger_{stamp}_{Guid.NewGuid():N}.zip");
            ZipFile.CreateFromDirectory(tempRoot, outputPath, CompressionLevel.Optimal, false);
            return new BackupInfo(outputPath, hash, schema, createdAt);
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }

    public async Task<string> VerifyBackupAsync(string archivePath, CancellationToken ct = default)
    {
        var temp = Path.Combine(Path.GetTempPath(), "SmartLedgerVerify_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            ZipFile.ExtractToDirectory(archivePath, temp);
            var db = Path.Combine(temp, "smartledger.db");
            var metadataPath = Path.Combine(temp, "metadata.json");
            if (!File.Exists(db) || !File.Exists(metadataPath)) throw new InvalidDataException("Backup archive is missing required files.");
            using var metadata = JsonDocument.Parse(await File.ReadAllTextAsync(metadataPath, ct));
            var root = metadata.RootElement;
            var schema = root.GetProperty("schema").GetInt32();
            if (schema != SupportedSchema || await ReadSchemaAsync(db, ct) != SupportedSchema)
                throw new InvalidDataException("Backup schema is not supported.");
            var actual = await HashFileAsync(db, ct);
            var expected = root.GetProperty("sha256").GetString();
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Backup SHA-256 verification failed.");
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = db, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            await connection.OpenAsync(ct);
            await using var integrity = connection.CreateCommand();
            integrity.CommandText = "PRAGMA integrity_check;";
            if (!string.Equals(Convert.ToString(await integrity.ExecuteScalarAsync(ct)), "ok", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Backup database integrity check failed.");
            return actual;
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    public async Task RestoreAsync(string archivePath, CancellationToken ct = default)
    {
        await VerifyBackupAsync(archivePath, ct);
        ct.ThrowIfCancellationRequested();
        var temp = Path.Combine(Path.GetTempPath(), "SmartLedgerRestore_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var source = Path.Combine(temp, "smartledger.db");
        var destination = Path.Combine(rootPath, "smartledger.db");
        var emergency = Path.Combine(rootPath, "SmartLedger_PreRestore_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".db");
        var hasEmergency = false;
        try
        {
            ZipFile.ExtractToDirectory(archivePath, temp);
            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(rootPath);
            SqliteConnection.ClearAllPools();
            if (File.Exists(destination))
            {
                File.Copy(destination, emergency, true);
                hasEmergency = true;
            }
            var replacement = destination + ".restore-" + Guid.NewGuid().ToString("N");
            File.Copy(source, replacement, true);
            try
            {
                ct.ThrowIfCancellationRequested();
                replaceFile(replacement, destination);
                if (File.Exists(destination + "-wal")) File.Delete(destination + "-wal");
                if (File.Exists(destination + "-shm")) File.Delete(destination + "-shm");
            }
            catch
            {
                if (hasEmergency && File.Exists(emergency))
                {
                    var recovery = destination + ".recovery-" + Guid.NewGuid().ToString("N");
                    File.Copy(emergency, recovery, true);
                    File.Move(recovery, destination, true);
                }
                throw;
            }
            finally
            {
                if (File.Exists(replacement)) File.Delete(replacement);
            }

            var restoredHash = await HashFileAsync(destination, ct);
            var sourceHash = await HashFileAsync(source, ct);
            if (!string.Equals(restoredHash, sourceHash, StringComparison.OrdinalIgnoreCase))
            {
                if (hasEmergency && File.Exists(emergency)) File.Copy(emergency, destination, true);
                throw new IOException("Restored database verification failed.");
            }

            // Restore branding files from the same verified archive as the database.
            var sourceBranding = Path.Combine(temp, "Branding");
            var destinationBranding = Path.Combine(rootPath, "Branding");
            Directory.CreateDirectory(destinationBranding);
            foreach (var existingLogo in Directory.EnumerateFiles(destinationBranding, "business-logo.*"))
                File.Delete(existingLogo);
            if (Directory.Exists(sourceBranding))
            {
                foreach (var logo in Directory.EnumerateFiles(sourceBranding, "business-logo.*"))
                {
                    ct.ThrowIfCancellationRequested();
                    File.Copy(logo, Path.Combine(destinationBranding, Path.GetFileName(logo)), true);
                }
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(temp)) Directory.Delete(temp, true);
        }
    }

    private static async Task<int> ReadSchemaAsync(string path, CancellationToken ct)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct));
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash);
    }
}