using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Data;

namespace SmartLedger.App.ViewModels;

public partial class RecoveryViewModel : ObservableObject
{
    private readonly UserRepository users;
    private readonly PasswordHasher hasher;
    private readonly Action completed;

    [ObservableProperty] private string userName = "";
    [ObservableProperty] private string recoveryPhrase = "";
    [ObservableProperty] private string newPassword = "";
    [ObservableProperty] private string confirmation = "";
    [ObservableProperty] private string recoveryHint = "";
    [ObservableProperty] private string error = "";
    [ObservableProperty] private string success = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool recoveryVerified;

    public RecoveryViewModel(UserRepository users, PasswordHasher hasher, Action completed)
    {
        this.users = users; this.hasher = hasher; this.completed = completed;
    }

    [RelayCommand]
    private async Task BeginRecoveryAsync()
    {
        if (IsBusy) return;
        Error = ""; Success = ""; RecoveryHint = ""; RecoveryVerified = false;
        if (string.IsNullOrWhiteSpace(UserName)) { Error = "أدخل اسم المستخدم أولاً."; return; }
        IsBusy = true;
        try
        {
            var hint = await users.GetRecoveryHintAsync(UserName);
            if (hint is null) { Error = "تعذر بدء الاستعادة. تحقق من البيانات وحاول مرة أخرى."; return; }
            RecoveryHint = hint; RecoveryVerified = true;
        }
        catch { Error = "تعذر بدء الاستعادة. تحقق من البيانات وحاول مرة أخرى."; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ResetPasswordAsync()
    {
        if (IsBusy || !RecoveryVerified) return;
        Error = ""; Success = "";
        if (string.IsNullOrWhiteSpace(RecoveryPhrase)) { Error = "أدخل عبارة الاسترداد."; return; }
        var validationError = RecoveryPasswordPolicy.Validate(NewPassword, Confirmation);
        if (validationError is not null) { Error = validationError; return; }
        IsBusy = true;
        try
        {
            var changed = await users.RecoverPasswordAsync(UserName, RecoveryPhrase, hasher.Hash(NewPassword));
            if (!changed) { Error = "عبارة الاسترداد غير صحيحة أو لم يعد الحساب متاحاً."; return; }
            RecoveryPhrase = ""; NewPassword = ""; Confirmation = "";
            Success = "تم تغيير كلمة المرور بنجاح. يمكنك الآن تسجيل الدخول.";
        }
        catch { Error = "تعذر تغيير كلمة المرور. حاول مرة أخرى."; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void BackToLogin() => completed();
}
