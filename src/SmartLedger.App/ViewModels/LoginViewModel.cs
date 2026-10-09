using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Audit;
using SmartLedger.Infrastructure.Data;
using SmartLedger.App.Security;

namespace SmartLedger.App.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly UserRepository users;
    private readonly IAppSession session;
    private readonly Action completed;
    private readonly AuditRepository audit;
    private readonly RememberedLoginStore rememberedLoginStore;

    [ObservableProperty] private string userName = "";
    [ObservableProperty] private string password = "";
    [ObservableProperty] private string error = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool rememberMe;

    public LoginViewModel(UserRepository users, IAppSession session, Action completed, AuditRepository audit, RememberedLoginStore rememberedLoginStore)
    {
        this.users = users;
        this.session = session;
        this.completed = completed;
        this.audit = audit;
        this.rememberedLoginStore = rememberedLoginStore;
    }

    public void LoadRememberedLogin()
    {
        var saved = rememberedLoginStore.Load();
        if (saved is null)
        {
            RememberMe = false;
            return;
        }

        UserName = saved.UserName;
        Password = saved.Password;
        RememberMe = true;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsBusy) return;
        Error = "";
        IsBusy = true;
        try
        {
            var u = await users.FindAsync(UserName);
            if (u is null || !u.IsActive || !PasswordHasher.Verify(Password, u.PasswordHash))
            {
                await audit.LogActionAsync(null, "LOGIN_FAILURE", "Login", null, new { UserName = UserName.Trim() });
                Error = "اسم المستخدم أو كلمة المرور غير صحيحة.";
                Password = "";
                if (RememberMe) rememberedLoginStore.Clear();
                return;
            }

            if (RememberMe)
            {
                try { rememberedLoginStore.Save(UserName, Password); }
                catch { /* Remember Me must never block a valid login. */ }
            }
            else
            {
                rememberedLoginStore.Clear();
            }

            await audit.LogActionAsync(u.Id, "LOGIN_SUCCESS", "Login", null, new { u.UserName });
            session.Start(u);
            completed();
        }
        catch (Exception ex)
        {
            try { await audit.LogActionAsync(null, "LOGIN_ERROR", "Login", null, new { ErrorType = ex.GetType().Name }); } catch { }
            Error = "تعذر تسجيل الدخول. تحقق من البيانات وحاول مرة أخرى.";
        }
        finally { IsBusy = false; }
    }
}
