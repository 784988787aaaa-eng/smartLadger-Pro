using SmartLedger.Infrastructure.Audit;
using Xunit;

namespace SmartLedger.Tests;

public sealed class AuditSecurityTests
{
    [Fact]
    public async Task Audit_SanitizesSecrets_AndKeepsSearchReadable()
    {
        var root = CreateTempRoot();
        try
        {
            var db = await SmartLedger.Infrastructure.Infrastructure.CreateDatabaseAsync(root);
            var audit = new AuditRepository(db);
            await audit.LogActionAsync(
                null,
                "TEST_AUDIT",
                "Security",
                null,
                new
                {
                    UserName = "tester",
                    Password = "do-not-store",
                    Token = "do-not-store",
                    Nested = new { Secret = "do-not-store", Safe = "yes" }
                });

            var entries = await audit.SearchAsync(text: "TEST_AUDIT");
            var details = Assert.Single(entries).Details;
            Assert.Contains("tester", details);
            Assert.Contains("Safe", details);
            Assert.DoesNotContain("do-not-store", details);
            Assert.DoesNotContain("Password", details, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Token", details, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Secret", details, StringComparison.OrdinalIgnoreCase);
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public async Task Audit_FailureIsSurfaced_ForMandatoryAuditCallers()
    {
        var root = CreateTempRoot();
        try
        {
            var db = await SmartLedger.Infrastructure.Infrastructure.CreateDatabaseAsync(root);
            var audit = new AuditRepository(db);

            await using (var c = db.Open())
            {
                await c.OpenAsync();
                await using var cmd = c.CreateCommand();
                cmd.CommandText = "ALTER TABLE AuditLogs RENAME TO AuditLogs_Broken;";
                await cmd.ExecuteNonQueryAsync();
            }

            await Assert.ThrowsAnyAsync<Exception>(() =>
                audit.LogActionAsync(null, "MANDATORY_TEST", "Security", null, new { Safe = true }));
        }
        finally { TryDelete(root); }
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "SmartLedger_AuditTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void TryDelete(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
    }
}
