using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Data;
using SmartLedger.App.Security;
using SmartLedger.App.ViewModels;

namespace SmartLedger.App.Views;

public partial class LoginWindow : Window
{
    private readonly UserRepository _users;
    private readonly PasswordHasher _hasher;
    private readonly RememberedLoginStore _rememberedLoginStore;
    private bool _fullScreen;
    private RECT _restoreBounds;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hWnd);

    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    public LoginWindow(UserRepository users, PasswordHasher hasher, SmartLedger.Core.Abstractions.IAppSession session, Action completed, SmartLedger.Infrastructure.Audit.AuditRepository audit)
    {
        _users = users;
        _hasher = hasher;
        _rememberedLoginStore = new RememberedLoginStore();
        InitializeComponent();
        DataContext = new LoginViewModel(users, session, completed, audit, _rememberedLoginStore);
        Loaded += LoginWindow_Loaded;
        PreviewKeyDown += LoginWindow_PreviewKeyDown;
    }

    private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
    {
        EnterFullScreen();

        if (DataContext is not LoginViewModel vm) return;
        vm.LoadRememberedLogin();
        PasswordBox.Password = vm.Password;

        if (vm.RememberMe && !string.IsNullOrEmpty(vm.Password))
            Keyboard.Focus(PasswordBox);
        else
            Keyboard.Focus(UserNameBox);
    }

    private void LoginWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullScreen();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && _fullScreen)
        {
            ExitFullScreen();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter) return;

        if (ReferenceEquals(e.OriginalSource, UserNameBox))
        {
            Keyboard.Focus(PasswordBox);
            e.Handled = true;
            return;
        }

        if (ReferenceEquals(e.OriginalSource, PasswordBox) || ReferenceEquals(e.OriginalSource, VisiblePasswordBox))
        {
            if (DataContext is LoginViewModel vm && vm.LoginCommand.CanExecute(null))
                vm.LoginCommand.Execute(null);

            e.Handled = true;
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ForgotPassword_Click(object sender, RoutedEventArgs e)
    {
        var recovery = new RecoveryWindow(_users, _hasher, () => Activate());
        recovery.Owner = this;
        recovery.ShowDialog();
    }

    private void PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm) vm.Password = PasswordBox.Password;
    }

    private void RememberMe_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && !vm.RememberMe)
            _rememberedLoginStore.Clear();
    }

    private void TogglePassword_Click(object sender, RoutedEventArgs e)
    {
        if (PasswordBox.Visibility == Visibility.Visible)
        {
            VisiblePasswordBox.Text = PasswordBox.Password;
            PasswordBox.Visibility = Visibility.Collapsed;
            VisiblePasswordBox.Visibility = Visibility.Visible;
            VisiblePasswordBox.Focus();
            VisiblePasswordBox.CaretIndex = VisiblePasswordBox.Text.Length;
        }
        else
        {
            PasswordBox.Password = VisiblePasswordBox.Text;
            PasswordBox.Visibility = Visibility.Visible;
            VisiblePasswordBox.Visibility = Visibility.Collapsed;
            PasswordBox.Focus();
        }
    }

    private void ToggleFullScreen() { if (_fullScreen) ExitFullScreen(); else EnterFullScreen(); }

    private void EnterFullScreen()
    {
        if (_fullScreen) return;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (!GetWindowRect(hwnd, out _restoreBounds)) return;
        WindowState = WindowState.Normal; ResizeMode = ResizeMode.NoResize; Topmost = true;
        var x = GetSystemMetrics(SmXVirtualScreen); var y = GetSystemMetrics(SmYVirtualScreen); var width = GetSystemMetrics(SmCxVirtualScreen); var height = GetSystemMetrics(SmCyVirtualScreen); var scale = GetDpiForWindow(hwnd) / 96.0;
        Left = x / scale; Top = y / scale; Width = width / scale; Height = height / scale;
        SetWindowPos(hwnd, HwndTopmost, x, y, width, height, SwpNoActivate | SwpShowWindow); Activate(); _fullScreen = true;
    }

    private void ExitFullScreen()
    {
        if (!_fullScreen) return;
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle; Topmost = false; ResizeMode = ResizeMode.CanResize;
        SetWindowPos(hwnd, IntPtr.Zero, _restoreBounds.Left, _restoreBounds.Top, Math.Max((int)MinWidth, _restoreBounds.Right - _restoreBounds.Left), Math.Max((int)MinHeight, _restoreBounds.Bottom - _restoreBounds.Top), SwpNoActivate | SwpShowWindow);
        _fullScreen = false;
    }
}
