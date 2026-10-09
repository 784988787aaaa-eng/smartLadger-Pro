using System.Windows;
using SmartLedger.App.ViewModels;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Data;
namespace SmartLedger.App.Views;
public partial class BootstrapWindow:Window
{
    private readonly SetupViewModel _vm;
    public BootstrapWindow(UserRepository users,PasswordHasher hasher,Action completed){InitializeComponent();_vm=new SetupViewModel(users,hasher,completed);DataContext=_vm;}
    private void PassChanged(object sender,RoutedEventArgs e)=>_vm.Password=Pass.Password;
    private void ConfirmChanged(object sender,RoutedEventArgs e)=>_vm.Confirmation=Confirm.Password;
}
