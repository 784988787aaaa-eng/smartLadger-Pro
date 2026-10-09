using Microsoft.Data.Sqlite;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Audit;
using SmartLedger.Infrastructure.Data;
using Xunit;

namespace SmartLedger.Tests;

public sealed class RecoveryTests
{
    [Fact]
    public async Task Recovery_Success_ChangesPasswordAndAuditsWithoutSecrets()
    {
        var root = NewRoot();
        const string oldPassword = "Old!12345";
        const string recovery = "My Recovery Phrase 2026";
        const string newPassword = "New!12345";
        try
        {
            var db = new Database(root);
            await db.InitializeAsync();
            var hasher = new PasswordHasher();
            var users = new UserRepository(db);
            await users.CreateAdministratorAsync(new SmartLedger.Core.Models.AdminSetup("admin", hasher.Hash(oldPassword), hasher.Hash(recovery), "اسم المدرسة الأولى"));

            Assert.Equal("اسم المدرسة الأولى", await users.GetRecoveryHintAsync("admin"));
            Assert.True(await users.RecoverPasswordAsync("admin", recovery, hasher.Hash(newPassword)));

            var user = await users.FindAsync("admin");
            Assert.NotNull(user);
            Assert.False(PasswordHasher.Verify(oldPassword, user!.PasswordHash));
            Assert.True(PasswordHasher.Verify(newPassword, user.PasswordHash));

            var audit = new AuditRepository(db);
            var entries = await audit.SearchAsync(action: "PASSWORD_RECOVERY");
            var details = Assert.Single(entries).Details;
            Assert.DoesNotContain(recovery, details, StringComparison.Ordinal);
            Assert.DoesNotContain(newPassword, details, StringComparison.Ordinal);
            Assert.DoesNotContain("RecoveryHash", details, StringComparison.OrdinalIgnoreCase);
        }
        finally { SqliteConnection.ClearAllPools(); DeleteRoot(root); }
    }

    [Fact]
    public async Task Recovery_WrongPhrase_DoesNotChangePassword()
    {
        var root = NewRoot();
        const string oldPassword = "Old!12345";
        try
        {
            var db = new Database(root);
            await db.InitializeAsync();
            var hasher = new PasswordHasher();
            var users = new UserRepository(db);
            await users.CreateAdministratorAsync(new SmartLedger.Core.Models.AdminSetup("admin", hasher.Hash(oldPassword), hasher.Hash("correct recovery"), "hint"));

            Assert.False(await users.RecoverPasswordAsync("admin", "wrong recovery", hasher.Hash("New!12345")));
            var user = await users.FindAsync("admin");
            Assert.NotNull(user);
            Assert.True(PasswordHasher.Verify(oldPassword, user!.PasswordHash));
        }
        finally { SqliteConnection.ClearAllPools(); DeleteRoot(root); }
    }

    [Fact]
    public async Task Recovery_UnknownUser_DoesNotRevealRecoveryHint()
    {
        var root = NewRoot();
        try
        {
            var db = new Database(root);
            await db.InitializeAsync();
            var users = new UserRepository(db);
            Assert.Null(await users.GetRecoveryHintAsync("missing-user"));
        }
        finally { SqliteConnection.ClearAllPools(); DeleteRoot(root); }
    }

    [Fact]
    public void Recovery_PasswordConfirmationMismatch_IsRejectedBeforeHashing()
    {
        Assert.Equal("تأكيد كلمة المرور غير مطابق.", RecoveryPasswordPolicy.Validate("New!12345", "Different!123"));
    }

    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "SL_Recovery_" + Guid.NewGuid().ToString("N"));
    private static void DeleteRoot(string root) { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
}
