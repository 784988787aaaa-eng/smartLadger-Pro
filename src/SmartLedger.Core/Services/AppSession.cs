using System.Globalization;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;

namespace SmartLedger.Core.Services;

public sealed class AppSession : IAppSession
{
    public AppUser? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null;
    public void Start(User user) => CurrentUser = new AppUser(user.Id, user.UserName, user.DisplayName, user.IsAdministrator, user.IsActive);
    public void Logout() => CurrentUser = null;
}

public sealed class PrivacyStateService : IPrivacyStateService
{
    public bool IsPrivate { get; private set; }
    public event EventHandler? Changed;

    public void Toggle()
    {
        IsPrivate = !IsPrivate;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public string FormatMoney(decimal value, string currency)
        => IsPrivate ? "••••••" : $"{value.ToString("N2", CultureInfo.InvariantCulture)} {currency}";
}
