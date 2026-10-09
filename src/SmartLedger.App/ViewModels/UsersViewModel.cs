using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Data;

namespace SmartLedger.App.ViewModels;

public partial class UsersViewModel : ObservableObject
{
    private readonly UserRepository repo;
    private readonly IAppSession session;
    private readonly PasswordHasher hasher = new();

    [ObservableProperty] private IReadOnlyList<User> users = Array.Empty<User>();
    [ObservableProperty] private User? selected;
    [ObservableProperty] private string userName = "";
    [ObservableProperty] private string displayName = "";
    [ObservableProperty] private string password = "";
    [ObservableProperty] private string confirmPassword = "";
    [ObservableProperty] private bool isAdministrator;
    [ObservableProperty] private bool isActive = true;
    [ObservableProperty] private bool isDialogOpen;
    [ObservableProperty] private string statusText = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool canManage;

    [ObservableProperty] private bool allowView = true;
    [ObservableProperty] private bool allowCreate;
    [ObservableProperty] private bool allowEdit;
    [ObservableProperty] private bool allowDelete;
    [ObservableProperty] private bool allowReports;
    [ObservableProperty] private bool allowUsers;
    [ObservableProperty] private bool allowSettings;
    [ObservableProperty] private bool allowBackup;

    public UsersViewModel(UserRepository repo, IAppSession session)
    {
        this.repo = repo;
        this.session = session;
    }

    public async Task LoadAsync()
    {
        if (session.CurrentUser is null) return;
        CanManage = session.CurrentUser.IsAdministrator;
        Users = await repo.GetAllAsync();
        if (Selected is not null)
            await LoadPermissionsAsync(Selected);
    }

    partial void OnSelectedChanged(User? value)
    {
        if (value is not null)
            _ = LoadSelectedAsync(value);
    }

    private async Task LoadSelectedAsync(User user)
    {
        UserName = user.UserName;
        DisplayName = user.DisplayName;
        Password = "";
        ConfirmPassword = "";
        IsAdministrator = user.IsAdministrator;
        IsActive = user.IsActive;
        StatusText = "";
        await LoadPermissionsAsync(user);
    }

    private async Task LoadPermissionsAsync(User user)
    {
        var p = await repo.GetPermissionsAsync(user.Id);
        AllowView = p.Contains(PermissionKeys.View) || user.IsAdministrator;
        AllowCreate = p.Contains(PermissionKeys.Create) || user.IsAdministrator;
        AllowEdit = p.Contains(PermissionKeys.Edit) || user.IsAdministrator;
        AllowDelete = p.Contains(PermissionKeys.Delete) || user.IsAdministrator;
        AllowReports = p.Contains(PermissionKeys.Reports) || user.IsAdministrator;
        AllowUsers = p.Contains(PermissionKeys.Users) || user.IsAdministrator;
        AllowSettings = p.Contains(PermissionKeys.Settings) || user.IsAdministrator;
        AllowBackup = p.Contains(PermissionKeys.Backup) || user.IsAdministrator;
    }

    [RelayCommand]
    private void New()
    {
        if (!CanManage) { StatusText = "لا تملك صلاحية إدارة المستخدمين."; return; }
        Selected = null;
        UserName = "";
        DisplayName = "";
        Password = "";
        ConfirmPassword = "";
        IsAdministrator = false;
        IsActive = true;
        AllowView = true;
        AllowCreate = false;
        AllowEdit = false;
        AllowDelete = false;
        AllowReports = false;
        AllowUsers = false;
        AllowSettings = false;
        AllowBackup = false;
        StatusText = "";
        IsDialogOpen = true;
    }

    [RelayCommand]
    private async Task Edit()
    {
        if (Selected is null) { StatusText = "اختر مستخدماً أولاً."; return; }
        if (!CanManage && Selected.Id != session.CurrentUser?.Id) { StatusText = "لا تملك صلاحية تعديل هذا المستخدم."; return; }
        await LoadSelectedAsync(Selected);
        IsDialogOpen = true;
    }

    [RelayCommand]
    private void Cancel() => IsDialogOpen = false;

    [RelayCommand]
    private async Task Save()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(UserName) || UserName.Trim().Length < 3) { StatusText = "اسم المستخدم مطلوب (3 أحرف على الأقل)."; return; }
        if (string.IsNullOrWhiteSpace(DisplayName)) { StatusText = "اسم العرض مطلوب."; return; }
        if (Selected is null && string.IsNullOrWhiteSpace(Password)) { StatusText = "كلمة المرور مطلوبة للمستخدم الجديد."; return; }
        if (!string.IsNullOrEmpty(Password) && Password != ConfirmPassword) { StatusText = "تأكيد كلمة المرور غير مطابق."; return; }
        if (session.CurrentUser is null) { StatusText = "انتهت جلسة المستخدم."; return; }

        IsBusy = true;
        try
        {
            if (Selected is null)
            {
                if (!CanManage) throw new UnauthorizedAccessException("هذه العملية متاحة للمدير فقط.");
                var user = new User(Guid.NewGuid(), UserName.Trim(), DisplayName.Trim(), hasher.Hash(Password), "", "", IsAdministrator, IsActive);
                await repo.CreateManagedUserAsync(user, session.CurrentUser.Id, SelectedPermissions());
                StatusText = "تم إنشاء المستخدم بنجاح.";
            }
            else
            {
                var current = Selected;
                var adminChange = session.CurrentUser.IsAdministrator;
                var admin = adminChange ? IsAdministrator : current.IsAdministrator;
                var active = adminChange ? IsActive : current.IsActive;
                var user = current with { UserName = UserName.Trim(), DisplayName = DisplayName.Trim(), IsAdministrator = admin, IsActive = active };
                await repo.UpdateUserAsync(user, session.CurrentUser.Id, string.IsNullOrWhiteSpace(Password) ? null : hasher.Hash(Password));
                if (session.CurrentUser.IsAdministrator && !user.IsAdministrator)
                    await repo.SetPermissionsAsync(user.Id, SelectedPermissions(), session.CurrentUser.Id);
                StatusText = "تم حفظ بيانات المستخدم.";
            }

            Users = await repo.GetAllAsync();
            IsDialogOpen = false;
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleActive()
    {
        if (Selected is null || session.CurrentUser is null) { StatusText = "اختر مستخدماً أولاً."; return; }
        if (!session.CurrentUser.IsAdministrator) { StatusText = "تعطيل المستخدمين متاح للمدير فقط."; return; }
        try
        {
            await repo.SetActiveAsync(Selected.Id, !Selected.IsActive, session.CurrentUser.Id);
            Users = await repo.GetAllAsync();
            Selected = Users.FirstOrDefault(x => x.Id == Selected.Id);
            StatusText = "تم تحديث حالة المستخدم.";
        }
        catch (Exception ex) { StatusText = ex.Message; }
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (Selected is null || session.CurrentUser is null) { StatusText = "اختر مستخدماً أولاً."; return; }
        if (!session.CurrentUser.IsAdministrator) { StatusText = "حذف المستخدمين متاح للمدير فقط."; return; }
        var confirmation = System.Windows.MessageBox.Show(
            $"هل أنت متأكد من حذف المستخدم «{Selected.DisplayName}» ({Selected.UserName})؟ لا يمكن التراجع عن هذه العملية.",
            "تأكيد حذف المستخدم",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No);
        if (confirmation != System.Windows.MessageBoxResult.Yes) return;
        try
        {
            await repo.DeleteUserAsync(Selected.Id, session.CurrentUser.Id);
            Selected = null;
            Users = await repo.GetAllAsync();
            StatusText = "تم حذف المستخدم.";
        }
        catch (Exception ex) { StatusText = ex.Message; }
    }

    private List<string> SelectedPermissions()
    {
        var list = new List<string>();
        if (AllowView) list.Add(PermissionKeys.View);
        if (AllowCreate) list.Add(PermissionKeys.Create);
        if (AllowEdit) list.Add(PermissionKeys.Edit);
        if (AllowDelete) list.Add(PermissionKeys.Delete);
        if (AllowReports) list.Add(PermissionKeys.Reports);
        if (AllowUsers) list.Add(PermissionKeys.Users);
        if (AllowSettings) list.Add(PermissionKeys.Settings);
        if (AllowBackup) list.Add(PermissionKeys.Backup);
        return list;
    }
}
