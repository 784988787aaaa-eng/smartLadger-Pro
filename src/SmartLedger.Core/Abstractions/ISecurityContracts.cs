using SmartLedger.Core.Models;

namespace SmartLedger.Core.Abstractions;

public static class PermissionKeys
{
    public const string View = "view";
    public const string Create = "create";
    public const string Edit = "edit";
    public const string Delete = "delete";
    public const string Reports = "reports";
    public const string Users = "users";
    public const string Settings = "settings";
    public const string Backup = "backup";
    public const string ResetData = "reset_data";
    public static readonly string[] All = new[] { View, Create, Edit, Delete, Reports, Users, Settings, Backup, ResetData };
}

public interface IUserRepository
{
    Task<bool> HasAdministratorAsync(CancellationToken ct = default);
    Task CreateAdministratorAsync(AdminSetup setup, CancellationToken ct = default);
    Task<User?> FindAsync(string userName, CancellationToken ct = default);
    Task<string?> GetRecoveryHintAsync(string userName, CancellationToken ct = default);
    Task<bool> RecoverPasswordAsync(string userName, string recoveryPhrase, string newPasswordHash, CancellationToken ct = default);
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default);
    Task CreateUserAsync(User user, CancellationToken ct = default);
    Task<User> CreateManagedUserAsync(User user, Guid actorId, IReadOnlyCollection<string> permissions, CancellationToken ct = default);
    Task<User> UpdateUserAsync(User user, Guid actorId, string? newPasswordHash, CancellationToken ct = default);
    Task SetActiveAsync(Guid userId, bool isActive, Guid actorId, CancellationToken ct = default);
    Task DeleteUserAsync(Guid userId, Guid actorId, CancellationToken ct = default);
    Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId, CancellationToken ct = default);
    Task SetPermissionsAsync(Guid userId, IReadOnlyCollection<string> permissions, Guid actorId, CancellationToken ct = default);
    Task<bool> CanAsync(Guid userId, string permission, CancellationToken ct = default);
}

public interface IAppSession
{
    AppUser? CurrentUser { get; }
    bool IsAuthenticated { get; }
    void Start(User user);
    void Logout();
}

public interface IPrivacyStateService
{
    bool IsPrivate { get; }
    event EventHandler? Changed;
    void Toggle();
    string FormatMoney(decimal value, string currency);
}

public interface IBalanceEngine
{
    decimal CalculateNetBalance(IEnumerable<LedgerOperation> operations);
    (decimal OwedToUs, decimal WeOwe) Split(decimal netBalance);
}

public interface IInventoryEngine
{
    decimal ToSmallUnits(decimal quantity, decimal conversionFactor);
    decimal ToLargeUnits(decimal smallUnits, decimal conversionFactor);
    bool IsLow(decimal stockSmallUnits, decimal alertThreshold);
}

public interface IBackupService
{
    string? FindRecommendedDirectory();
    Task<BackupInfo> CreateAsync(string directory, CancellationToken ct = default);
    Task RestoreAsync(string filePath, CancellationToken ct = default);
}
