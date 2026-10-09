using System.Windows;
using System.Windows.Controls;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Data;

namespace SmartLedger.App.Views;

public partial class RecoveryWindow : Window
{
    public RecoveryWindow(UserRepository users, PasswordHasher hasher, Action completed)
    {
        InitializeComponent();
        DataContext = new ViewModels.RecoveryViewModel(users, hasher, completed);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void RecoveryPhraseChanged(object sender, RoutedEventArgs e) => ((ViewModels.RecoveryViewModel)DataContext).RecoveryPhrase = ((PasswordBox)sender).Password;
    private void NewPasswordChanged(object sender, RoutedEventArgs e) => ((ViewModels.RecoveryViewModel)DataContext).NewPassword = ((PasswordBox)sender).Password;
    private void ConfirmationChanged(object sender, RoutedEventArgs e) => ((ViewModels.RecoveryViewModel)DataContext).Confirmation = ((PasswordBox)sender).Password;
}
