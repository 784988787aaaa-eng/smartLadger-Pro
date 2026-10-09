using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using Microsoft.Data.Sqlite;
using SmartLedger.Infrastructure.Audit;

namespace SmartLedger.Infrastructure.Data;

public sealed class UserRepository(Database database) : IUserRepository
{
    private readonly AuditRepository audit = new(database);

    private static void P(SqliteCommand c, string n, object v) => c.Parameters.AddWithValue(n, v);

    public async Task<bool> HasAdministratorAsync(CancellationToken ct = default)
    {
        await using var db = database.Open();
        await db.OpenAsync(ct);
        await using var c = db.CreateCommand();
        c.CommandText = "SELECT EXISTS(SELECT 1 FROM Users WHERE IsAdministrator=1 AND IsActive=1);";
        return Convert.ToInt64(await c.ExecuteScalarAsync(ct)) == 1;
    }

    public async Task CreateAdministratorAsync(AdminSetup s, CancellationToken ct = default)
    {
        ValidateUserName(s.UserName);
        if (string.IsNullOrWhiteSpace(s.PasswordHash))
            throw new ArgumentException("كلمة المرور مطلوبة.", nameof(s));

        var user = new User(Guid.NewGuid(), s.UserName.Trim(), s.UserName.Trim(), s.PasswordHash, s.RecoveryHash, s.RecoveryHint, true, true);
        await CreateUserCoreAsync(user, PermissionKeys.All, ct);
    }

    public async Task<User?> FindAsync(string name, CancellationToken ct = default)
    {
        await using var db = database.Open();
        await db.OpenAsync(ct);
        await using var c = db.CreateCommand();
        c.CommandText = "SELECT Id,UserName,DisplayName,PasswordHash,RecoveryHash,RecoveryHint,IsAdministrator,IsActive FROM Users WHERE UserName=$n LIMIT 1;";
        P(c, "$n", name.Trim());
        await using var r = await c.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Read(r) : null;
    }

    public async Task<string?> GetRecoveryHintAsync(string name, CancellationToken ct = default)
    {
        await using var db = database.Open();
        await db.OpenAsync(ct);
        await using var c = db.CreateCommand();
        c.CommandText = "SELECT RecoveryHash,RecoveryHint,IsActive FROM Users WHERE UserName=$n LIMIT 1;";
        P(c, "$n", name.Trim());
        await using var r = await c.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct) || !r.GetBoolean(2) || r.IsDBNull(0) || string.IsNullOrWhiteSpace(r.GetString(0))) return null;
        return r.IsDBNull(1) ? string.Empty : r.GetString(1);
    }

    public async Task<bool> RecoverPasswordAsync(string name, string recoveryPhrase, string newPasswordHash, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recoveryPhrase) || string.IsNullOrWhiteSpace(newPasswordHash)) return false;

        var user = await FindAsync(name, ct);
        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.RecoveryHash) || !PasswordHasher.Verify(recoveryPhrase, user.RecoveryHash))
        {
            await audit.LogActionAsync(null, "PASSWORD_RECOVERY_FAILURE", "User", user?.Id, new { UserName = name.Trim() }, ct);
            return false;
        }

        await using var db = database.Open();
        await db.OpenAsync(ct);
        await using var c = db.CreateCommand();
        c.CommandText = "UPDATE Users SET PasswordHash=$hash WHERE Id=$id;";
        P(c, "$hash", newPasswordHash);
        P(c, "$id", user.Id.ToString("N"));
        if (await c.ExecuteNonQueryAsync(ct) != 1) return false;

        await audit.LogActionAsync(user.Id, "PASSWORD_RECOVERY", "User", user.Id, new { user.UserName }, ct);
        return true;
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default)
    {
        var x = new List<User>();
        await using var db = database.Open();
        await db.OpenAsync(ct);
        await using var c = db.CreateCommand();
        c.CommandText = "SELECT Id,UserName,DisplayName,PasswordHash,RecoveryHash,RecoveryHint,IsAdministrator,IsActive FROM Users ORDER BY DisplayName;";
        await using var r = await c.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) x.Add(Read(r));
        return x;
    }

    public async Task CreateUserAsync(User u, CancellationToken ct = default) => await CreateUserCoreAsync(u, u.IsAdministrator ? PermissionKeys.All : new[] { PermissionKeys.View }, ct);

    public async Task<User> CreateManagedUserAsync(User u, Guid actorId, IReadOnlyCollection<string> permissions, CancellationToken ct = default)
    {
        await RequireAdministratorAsync(actorId, ct);
        ValidateUser(u);
        var normalized = NormalizePermissions(permissions);
        if (u.IsAdministrator) normalized = PermissionKeys.All;
        await CreateUserCoreAsync(u, normalized, ct);
        await audit.LogActionAsync(actorId, "USER_CREATE", "User", u.Id, new { u.UserName, u.DisplayName, u.IsAdministrator, permissions = normalized }, ct);
        return u;
    }

    public async Task<User> UpdateUserAsync(User user, Guid actorId, string? newPasswordHash, CancellationToken ct = default)
    {
        ValidateUser(user);
        var actor = await GetByIdAsync(actorId, ct) ?? throw new UnauthorizedAccessException("المستخدم المنفذ غير موجود.");
        var current = await GetByIdAsync(user.Id, ct) ?? throw new InvalidOperationException("المستخدم غير موجود.");

        if (!actor.IsAdministrator && actor.Id != current.Id)
            throw new UnauthorizedAccessException("لا تملك صلاحية تعديل هذا المستخدم.");
        if (!actor.IsAdministrator && user.IsAdministrator != current.IsAdministrator)
            throw new UnauthorizedAccessException("لا يمكنك رفع صلاحياتك بنفسك.");
        if (!actor.IsAdministrator && user.IsActive != current.IsActive)
            throw new UnauthorizedAccessException("لا يمكنك تغيير حالة حسابك من هنا.");
        if (current.IsAdministrator && !user.IsAdministrator && current.IsActive && await ActiveAdministratorCountAsync(ct) <= 1)
            throw new InvalidOperationException("لا يمكن خفض صلاحيات آخر مدير نشط.");

        await using var db = database.Open();
        await db.OpenAsync(ct);
        await using var c = db.CreateCommand();
        c.CommandText = newPasswordHash is null
            ? "UPDATE Users SET UserName=$name,DisplayName=$display,IsAdministrator=$admin,IsActive=$active WHERE Id=$id;"
            : "UPDATE Users SET UserName=$name,DisplayName=$display,PasswordHash=$hash,IsAdministrator=$admin,IsActive=$active WHERE Id=$id;";
        P(c, "$name", user.UserName.Trim()); P(c, "$display", user.DisplayName.Trim()); P(c, "$admin", user.IsAdministrator ? 1 : 0); P(c, "$active", user.IsActive ? 1 : 0); P(c, "$id", user.Id.ToString("N"));
        if (newPasswordHash is not null) P(c, "$hash", newPasswordHash);
        await c.ExecuteNonQueryAsync(ct);
        if (user.IsAdministrator) await SetPermissionsCoreAsync(user.Id, PermissionKeys.All, ct);
        await audit.LogActionAsync(actorId, "USER_UPDATE", "User", user.Id, new { user.UserName, user.DisplayName, user.IsAdministrator, user.IsActive, passwordChanged = newPasswordHash is not null }, ct);
        return user;
    }

    public async Task SetActiveAsync(Guid userId, bool isActive, Guid actorId, CancellationToken ct = default)
    {
        await RequireAdministratorAsync(actorId, ct);
        var target = await GetByIdAsync(userId, ct) ?? throw new InvalidOperationException("المستخدم غير موجود.");
        if (!isActive && target.IsAdministrator && target.IsActive && await ActiveAdministratorCountAsync(ct) <= 1) throw new InvalidOperationException("لا يمكن تعطيل آخر مدير نشط.");
        if (userId == actorId && !isActive && target.IsAdministrator && await ActiveAdministratorCountAsync(ct) <= 1) throw new InvalidOperationException("لا يمكن تعطيل حساب المدير الوحيد.");
        await using var db = database.Open(); await db.OpenAsync(ct); await using var c = db.CreateCommand(); c.CommandText = "UPDATE Users SET IsActive=$active WHERE Id=$id;"; P(c, "$active", isActive ? 1 : 0); P(c, "$id", userId.ToString("N")); await c.ExecuteNonQueryAsync(ct);
        await audit.LogActionAsync(actorId, isActive ? "USER_ACTIVATE" : "USER_DEACTIVATE", "User", userId, new { target.UserName }, ct);
    }

    public async Task DeleteUserAsync(Guid userId, Guid actorId, CancellationToken ct = default)
    {
        await RequireAdministratorAsync(actorId, ct);
        if (userId == actorId) throw new InvalidOperationException("لا يمكنك حذف حسابك الحالي.");
        var target = await GetByIdAsync(userId, ct) ?? throw new InvalidOperationException("المستخدم غير موجود.");
        if (target.IsAdministrator && target.IsActive && await ActiveAdministratorCountAsync(ct) <= 1) throw new InvalidOperationException("لا يمكن حذف آخر مدير نشط.");
        await using var db = database.Open(); await db.OpenAsync(ct); await using var c = db.CreateCommand(); c.CommandText = "DELETE FROM Users WHERE Id=$id;"; P(c, "$id", userId.ToString("N")); await c.ExecuteNonQueryAsync(ct);
        await audit.LogActionAsync(actorId, "USER_DELETE", "User", userId, new { target.UserName }, ct);
    }

    public async Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await GetByIdAsync(userId, ct); if (user is null) return new HashSet<string>(StringComparer.OrdinalIgnoreCase); if (user.IsAdministrator) return new HashSet<string>(PermissionKeys.All, StringComparer.OrdinalIgnoreCase);
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase); await using var db = database.Open(); await db.OpenAsync(ct); await using var c = db.CreateCommand(); c.CommandText = "SELECT Permission FROM UserPermissions WHERE UserId=$id;"; P(c, "$id", userId.ToString("N")); await using var r = await c.ExecuteReaderAsync(ct); while (await r.ReadAsync(ct)) permissions.Add(r.GetString(0)); return permissions;
    }

    public async Task SetPermissionsAsync(Guid userId, IReadOnlyCollection<string> permissions, Guid actorId, CancellationToken ct = default)
    {
        await RequireAdministratorAsync(actorId, ct); var target = await GetByIdAsync(userId, ct) ?? throw new InvalidOperationException("المستخدم غير موجود."); if (target.IsAdministrator) throw new InvalidOperationException("صلاحيات المدير الكاملة لا يمكن تقليصها من شاشة الصلاحيات.");
        var normalized = NormalizePermissions(permissions); await SetPermissionsCoreAsync(userId, normalized, ct); await audit.LogActionAsync(actorId, "USER_PERMISSIONS_SET", "User", userId, new { permissions = normalized }, ct);
    }

    public async Task<bool> CanAsync(Guid userId, string permission, CancellationToken ct = default)
    {
        if (!PermissionKeys.All.Contains(permission, StringComparer.OrdinalIgnoreCase)) return false; var user = await GetByIdAsync(userId, ct); if (user is null || !user.IsActive) return false; if (user.IsAdministrator) return true;
        await using var db = database.Open(); await db.OpenAsync(ct); await using var c = db.CreateCommand(); c.CommandText = "SELECT EXISTS(SELECT 1 FROM UserPermissions WHERE UserId=$id AND Permission=$permission);"; P(c, "$id", userId.ToString("N")); P(c, "$permission", permission); return Convert.ToInt64(await c.ExecuteScalarAsync(ct)) == 1;
    }

    private async Task CreateUserCoreAsync(User u, IReadOnlyCollection<string> permissions, CancellationToken ct)
    {
        ValidateUser(u); await using var db = database.Open(); await db.OpenAsync(ct); await using var tx = await db.BeginTransactionAsync(ct); await using var c = db.CreateCommand(); c.Transaction = (SqliteTransaction)tx; c.CommandText = "INSERT INTO Users VALUES($id,$name,$display,$hash,$rh,$hint,$admin,$active,$created);";
        P(c, "$id", u.Id.ToString("N")); P(c, "$name", u.UserName.Trim()); P(c, "$display", u.DisplayName.Trim()); P(c, "$hash", u.PasswordHash); P(c, "$rh", u.RecoveryHash); P(c, "$hint", u.RecoveryHint); P(c, "$admin", u.IsAdministrator ? 1 : 0); P(c, "$active", u.IsActive ? 1 : 0); P(c, "$created", DateTimeOffset.UtcNow.ToString("O")); await c.ExecuteNonQueryAsync(ct);
        c.Parameters.Clear(); c.CommandText = "INSERT INTO UserPermissions(UserId,Permission) VALUES($id,$permission);"; P(c, "$id", u.Id.ToString("N")); foreach (var permission in NormalizePermissions(permissions)) { P(c, "$permission", permission); await c.ExecuteNonQueryAsync(ct); c.Parameters.RemoveAt("$permission"); } await tx.CommitAsync(ct);
    }

    private async Task SetPermissionsCoreAsync(Guid userId, IReadOnlyCollection<string> permissions, CancellationToken ct)
    {
        var normalized = NormalizePermissions(permissions); await using var db = database.Open(); await db.OpenAsync(ct); await using var tx = await db.BeginTransactionAsync(ct); await using var c = db.CreateCommand(); c.Transaction = (SqliteTransaction)tx; c.CommandText = "DELETE FROM UserPermissions WHERE UserId=$id;"; P(c, "$id", userId.ToString("N")); await c.ExecuteNonQueryAsync(ct); c.Parameters.Clear(); c.CommandText = "INSERT INTO UserPermissions(UserId,Permission) VALUES($id,$permission);"; P(c, "$id", userId.ToString("N")); foreach (var permission in normalized) { P(c, "$permission", permission); await c.ExecuteNonQueryAsync(ct); c.Parameters.RemoveAt("$permission"); } await tx.CommitAsync(ct);
    }

    private async Task RequireAdministratorAsync(Guid actorId, CancellationToken ct)
    { var actor = await GetByIdAsync(actorId, ct); if (actor is null || !actor.IsActive || !actor.IsAdministrator) throw new UnauthorizedAccessException("هذه العملية متاحة للمدير فقط."); }

    private async Task<User?> GetByIdAsync(Guid id, CancellationToken ct)
    { await using var db = database.Open(); await db.OpenAsync(ct); await using var c = db.CreateCommand(); c.CommandText = "SELECT Id,UserName,DisplayName,PasswordHash,RecoveryHash,RecoveryHint,IsAdministrator,IsActive FROM Users WHERE Id=$id LIMIT 1;"; P(c, "$id", id.ToString("N")); await using var r = await c.ExecuteReaderAsync(ct); return await r.ReadAsync(ct) ? Read(r) : null; }
    private async Task<long> ActiveAdministratorCountAsync(CancellationToken ct) { await using var db = database.Open(); await db.OpenAsync(ct); await using var c = db.CreateCommand(); c.CommandText = "SELECT COUNT(*) FROM Users WHERE IsAdministrator=1 AND IsActive=1;"; return Convert.ToInt64(await c.ExecuteScalarAsync(ct)); }
    private static string[] NormalizePermissions(IEnumerable<string> permissions) => permissions.Where(x => PermissionKeys.All.Contains(x, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private static void ValidateUser(User u) { ValidateUserName(u.UserName); if (string.IsNullOrWhiteSpace(u.DisplayName)) throw new ArgumentException("اسم العرض مطلوب.", nameof(u)); if (string.IsNullOrWhiteSpace(u.PasswordHash)) throw new ArgumentException("كلمة المرور مطلوبة.", nameof(u)); }
    private static void ValidateUserName(string name) { var value = name.Trim(); if (value.Length < 3 || value.Length > 50 || value.Any(char.IsWhiteSpace)) throw new ArgumentException("اسم المستخدم يجب أن يكون من 3 إلى 50 حرفاً دون مسافات.", nameof(name)); }
    private static User Read(SqliteDataReader r) => new(Guid.Parse(r.GetString(0)), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.GetBoolean(6), r.GetBoolean(7));
}