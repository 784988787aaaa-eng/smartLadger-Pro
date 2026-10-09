using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Data;
namespace SmartLedger.App.ViewModels;
public partial class SetupViewModel:ObservableObject
{
    private readonly UserRepository _users;private readonly PasswordHasher _hasher;private readonly Action _completed;
    [ObservableProperty]private string userName="admin";[ObservableProperty]private string password="";[ObservableProperty]private string confirmation="";[ObservableProperty]private string recoveryPhrase="";[ObservableProperty]private string recoveryHint="";[ObservableProperty]private string error="";
    public SetupViewModel(UserRepository users,PasswordHasher hasher,Action completed){_users=users;_hasher=hasher;_completed=completed;}
    [RelayCommand]private async Task CreateAsync(){Error="";if(UserName.Trim().Length<3){Error="اسم المستخدم يجب أن يكون 3 أحرف على الأقل.";return;}if(Password.Length<8){Error="كلمة المرور يجب أن تكون 8 أحرف على الأقل.";return;}if(Password!=Confirmation){Error="تأكيد كلمة المرور غير مطابق.";return;}if(RecoveryPhrase.Trim().Length<6){Error="عبارة الاسترداد قصيرة جداً.";return;}if(RecoveryHint.Trim().Length<2){Error="أدخل تلميحاً مناسباً.";return;}await _users.CreateAdministratorAsync(new AdminSetup(UserName,_hasher.Hash(Password),_hasher.Hash(RecoveryPhrase),RecoveryHint));_completed();}
}
