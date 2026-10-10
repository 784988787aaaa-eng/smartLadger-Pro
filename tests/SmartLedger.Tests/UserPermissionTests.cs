using Microsoft.Data.Sqlite;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Data;
using Xunit;

namespace SmartLedger.Tests;

public sealed class UserPermissionTests
{
    [Fact]
    public async Task Admin_CanCreateEditDisableReactivateAndSetPermissions()
    {
        var root = NewRoot();
        try
        {
            var db = new Database(root);
            await db.InitializeAsync();
            var users = new UserRepository(db);
            var hasher = new PasswordHasher();
            await users.CreateAdministratorAsync(new AdminSetup("admin", hasher.Hash("Admin!123"), "", ""));
            var admin = await users.FindAsync("admin");
            Assert.NotNull(admin);

            var user = new User(Guid.NewGuid(), "operator", "موظف", hasher.Hash("User!123"), "", "", false, true);
            await users.CreateManagedUserAsync(user, admin!.Id, new[] { PermissionKeys.View, PermissionKeys.Create });
            Assert.True(await users.CanAsync(user.Id, PermissionKeys.View));
            Assert.False(await users.CanAsync(user.Id, PermissionKeys.Delete));

            await users.UpdateUserAsync(user with { DisplayName = "موظف معدل" }, admin.Id, null);
            await users.SetPermissionsAsync(user.Id, new[] { PermissionKeys.View, PermissionKeys.Reports }, admin.Id);
            await users.SetActiveAsync(user.Id, false, admin.Id);
            Assert.False(await users.CanAsync(user.Id, PermissionKeys.View));
            await users.SetActiveAsync(user.Id, true, admin.Id);
            Assert.True(await users.CanAsync(user.Id, PermissionKeys.Reports));
        }
        finally { SqliteConnection.ClearAllPools(); DeleteRoot(root); }
    }

    [Fact]
    public async Task NonAdmin_CannotRaiseOwnPrivilegesOrModifyAnotherUser()
    {
        var root = NewRoot();
        try
        {
            var db = new Database(root);
            await db.InitializeAsync();
            var users = new UserRepository(db);
            var h = new PasswordHasher();
            await users.CreateAdministratorAsync(new AdminSetup("admin", h.Hash("Admin!123"), "", ""));
            var admin = await users.FindAsync("admin");
            var user = new User(Guid.NewGuid(), "operator", "موظف", h.Hash("User!123"), "", "", false, true);
            await users.CreateManagedUserAsync(user, admin!.Id, new[] { PermissionKeys.View });

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => users.UpdateUserAsync(user with { IsAdministrator = true }, user.Id, null));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => users.UpdateUserAsync(admin with { DisplayName = "اختراق" }, user.Id, null));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => users.SetPermissionsAsync(user.Id, PermissionKeys.All, user.Id));
        }
        finally { SqliteConnection.ClearAllPools(); DeleteRoot(root); }
    }

    [Fact]
    public async Task LastActiveAdministrator_CannotBeDisabledDemotedOrDeleted()
    {
        var root = NewRoot();
        try
        {
            var db = new Database(root);
            await db.InitializeAsync();
            var users = new UserRepository(db);
            var h = new PasswordHasher();
            await users.CreateAdministratorAsync(new AdminSetup("admin", h.Hash("Admin!123"), "", ""));
            var admin = await users.FindAsync("admin");
            Assert.NotNull(admin);

            await Assert.ThrowsAsync<InvalidOperationException>(() => users.SetActiveAsync(admin!.Id, false, admin.Id));
            await Assert.ThrowsAsync<InvalidOperationException>(() => users.UpdateUserAsync(admin with { IsAdministrator = false }, admin.Id, null));
            var other = new User(Guid.NewGuid(), "operator", "موظف", h.Hash("User!123"), "", "", false, true);
            await users.CreateManagedUserAsync(other, admin.Id, new[] { PermissionKeys.View });
            await Assert.ThrowsAsync<InvalidOperationException>(() => users.DeleteUserAsync(admin.Id, admin.Id));
        }
        finally { SqliteConnection.ClearAllPools(); DeleteRoot(root); }
    }

    [Fact]
    public async Task PermissionTable_ExistsAndAdministratorHasAllPermissions()
    {
        var root = NewRoot();
        try
        {
            var db = new Database(root);
            await db.InitializeAsync();
            var users = new UserRepository(db);
            var h = new PasswordHasher();
            await users.CreateAdministratorAsync(new AdminSetup("admin", h.Hash("Admin!123"), "", ""));
            var admin = await users.FindAsync("admin");
            var permissions = await users.GetPermissionsAsync(admin!.Id);
            Assert.All(PermissionKeys.All, p => Assert.Contains(p, permissions));

            await using var c = db.Open();
            await c.OpenAsync();
            await using var q = c.CreateCommand();
            q.CommandText = "PRAGMA user_version;";
            Assert.Equal(10L, Convert.ToInt64(await q.ExecuteScalarAsync()));
        }
        finally { SqliteConnection.ClearAllPools(); DeleteRoot(root); }
    }

    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "SL_User_" + Guid.NewGuid().ToString("N"));
    private static void DeleteRoot(string root) { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
}
