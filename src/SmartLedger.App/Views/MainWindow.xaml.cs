using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Data;
using SmartLedger.App.ViewModels;
using SmartLedger.Core.Models;

namespace SmartLedger.App.Views;

public partial class MainWindow : Window
{
    private bool _fullScreen;
    private RECT _restoreBounds;
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hWnd);
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.PrivacyEnabled))
                Dispatcher.BeginInvoke(RefreshPrivacyBindings, DispatcherPriority.DataBind);
        };

        Loaded += async (_, _) =>
        {
            try
            {
                await vm.NavigateCommand.ExecuteAsync(AppPage.Dashboard);
                EnterFullScreen();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "SmartLedger", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        PreviewKeyDown += MainWindow_PreviewKeyDown;
    }

    private void GlobalSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.ApplyCurrentScreenSearch(GlobalSearchBox.Text);
    }

    private async void GlobalSearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await ((MainViewModel)DataContext).ExecuteGlobalSearchCommand.ExecuteAsync(null);
            e.Handled = true;
        }
    }

    private async void GlobalSearchResult_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is GlobalSearchResult result)
        {
            await ((MainViewModel)DataContext).OpenGlobalSearchResultCommand.ExecuteAsync(result);
            e.Handled = true;
        }
    }

    private async void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.K)
        {
            GlobalSearchBox.Focus();
            GlobalSearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N)
        {
            await OpenContextualCreateAsync();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.P)
        {
            PrintCurrentPage();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
        {
            await SaveContextualAsync();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F5)
        {
            await RefreshCurrentAsync();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.F3)
        {
            GlobalSearchBox.Focus();
            GlobalSearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.F2)
        {
            var mainVm = (MainViewModel)DataContext;
            await mainVm.NavigateCommand.ExecuteAsync(AppPage.Sales);
            if (mainVm.CurrentViewModel is SalesViewModel salesVm)
                salesVm.OpenNewCommand.Execute(null);
            _ = Dispatcher.BeginInvoke(FocusFirstInput, DispatcherPriority.Input);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None)
        {
            var vm = (MainViewModel)DataContext;
            if (e.Key == Key.F7)
            {
                await vm.QuickAddAccountCommand.ExecuteAsync(null);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.F8)
            {
                await vm.QuickOperationCommand.ExecuteAsync(null);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.F9)
            {
                await vm.QuickSaleCommand.ExecuteAsync(null);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.F10)
            {
                if (vm.CurrentViewModel is SalesViewModel || vm.CurrentViewModel is PurchasesViewModel)
                    await SaveContextualAsync();
                else
                    await vm.QuickPurchaseCommand.ExecuteAsync(null);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.Escape)
        {
            if (((MainViewModel)DataContext).IsGlobalSearchOpen)
            {
                ((MainViewModel)DataContext).IsGlobalSearchOpen = false;
                e.Handled = true;
                return;
            }

            if (((MainViewModel)DataContext).IsNotificationsOpen)
            {
                ((MainViewModel)DataContext).IsNotificationsOpen = false;
                e.Handled = true;
                return;
            }

            if (TryCloseActiveDialog())
            {
                e.Handled = true;
                return;
            }

            if (GlobalSearchBox.IsKeyboardFocusWithin && !string.IsNullOrWhiteSpace(GlobalSearchBox.Text))
            {
                GlobalSearchBox.Clear();
                e.Handled = true;
            }
        }
        else if (e.Key == Key.F11)
        {
            ToggleFullScreen();
            e.Handled = true;
        }
    }

    private async Task OpenContextualCreateAsync()
    {
        var vm = (MainViewModel)DataContext;
        switch (vm.CurrentViewModel)
        {
            case AccountsViewModel a:
                a.OpenAddCommand.Execute(null);
                break;
            case InventoryViewModel i:
                i.OpenAddCommand.Execute(null);
                break;
            case ExpensesViewModel e:
                e.OpenAddCommand.Execute(null);
                break;
            default:
                await vm.NavigateCommand.ExecuteAsync(AppPage.Operations);
                break;
        }

        _ = Dispatcher.BeginInvoke(FocusFirstInput, DispatcherPriority.Input);
    }

    private async System.Threading.Tasks.Task SaveContextualAsync()
    {
        switch (((MainViewModel)DataContext).CurrentViewModel)
        {
            case AccountsViewModel a when a.IsDialogOpen:
                await a.SaveNewCommand.ExecuteAsync(null);
                break;
            case InventoryViewModel i when i.IsDialogOpen:
                await i.SaveNewCommand.ExecuteAsync(null);
                break;
            case ExpensesViewModel e when e.IsDialogOpen:
                await e.SaveCommand.ExecuteAsync(null);
                break;
            case OperationsViewModel o:
                await o.SaveCommand.ExecuteAsync(null);
                break;
            case SalesViewModel s:
                await s.SaveCommand.ExecuteAsync(null);
                break;
            case PurchasesViewModel p:
                await p.SaveCommand.ExecuteAsync(null);
                break;
            case SettingsViewModel st:
                await st.SaveCommand.ExecuteAsync(null);
                break;
        }
    }

    private async System.Threading.Tasks.Task RefreshCurrentAsync()
    {
        switch (((MainViewModel)DataContext).CurrentViewModel)
        {
            case DashboardViewModel x: await x.LoadAsync(); break;
            case AccountsViewModel x: await x.LoadAsync(); break;
            case OperationsViewModel x: await x.LoadAsync(); break;
            case SalesViewModel x: await x.LoadAsync(); break;
            case PurchasesViewModel x: await x.LoadAsync(); break;
            case InventoryViewModel x: await x.LoadAsync(); break;
            case ExpensesViewModel x: await x.LoadAsync(); break;
            case ReportsViewModel x: await x.LoadAsync(); break;
            case UsersViewModel x: await x.LoadAsync(); break;
            case RecycleBinViewModel x: await x.LoadAsync(); break;
            case SettingsViewModel x: await x.LoadAsync(); break;
        }
    }

    private bool TryCloseActiveDialog()
    {
        switch (((MainViewModel)DataContext).CurrentViewModel)
        {
            case AccountsViewModel a when a.IsBookDialogOpen:
                a.CloseBookDialogCommand.Execute(null); return true;
            case AccountsViewModel a when a.IsDialogOpen:
                a.CloseDialogCommand.Execute(null); return true;
            case InventoryViewModel i when i.IsDialogOpen:
                i.CloseDialogCommand.Execute(null); return true;
            case ExpensesViewModel x when x.IsDialogOpen:
                x.CloseDialogCommand.Execute(null); return true;
            case OperationsViewModel o when o.IsDialogOpen:
                o.CloseDialogCommand.Execute(null); return true;
            case SalesViewModel s when s.IsDialogOpen:
                s.CloseDialogCommand.Execute(null); return true;
            case PurchasesViewModel p when p.IsDialogOpen:
                p.CloseDialogCommand.Execute(null); return true;
            case UsersViewModel u when u.IsDialogOpen:
                u.CancelCommand.Execute(null); return true;
            default:
                return false;
        }
    }

    private void PrintCurrentPage()
    {
        try
        {
            var dialog = new System.Windows.Controls.PrintDialog();
            if (dialog.ShowDialog() == true)
                dialog.PrintVisual(CurrentContent, $"SmartLedger — {((MainViewModel)DataContext).PageTitle}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "SmartLedger", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void RefreshPrivacyBindings()
    {
        RefreshPrivacyBindings(this);
    }

    private static void RefreshPrivacyBindings(DependencyObject root)
    {
        if (root is TextBlock tb)
            BindingOperations.GetBindingExpressionBase(tb, TextBlock.TextProperty)?.UpdateTarget();
        else if (root is TextBox box)
            BindingOperations.GetBindingExpressionBase(box, TextBox.TextProperty)?.UpdateTarget();

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            RefreshPrivacyBindings(VisualTreeHelper.GetChild(root, i));
    }

    private void FocusFirstInput()
    {
        var input = FindFirstInput(this);
        input?.Focus();
    }

    private static Control? FindFirstInput(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is TextBox textBox && textBox.IsEnabled && textBox.IsTabStop)
                return textBox;

            if (child is ComboBox comboBox && comboBox.IsEnabled && comboBox.IsTabStop)
                return comboBox;

            var nested = FindFirstInput(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed)
            return;

        if (e.ClickCount == 2)
        {
            if (_fullScreen)
                ExitFullScreen();
            else
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            e.Handled = true;
            return;
        }

        if (!_fullScreen && WindowState == WindowState.Normal)
        {
            try { DragMove(); }
            catch (InvalidOperationException) { /* Mouse capture can be released before drag starts. */ }
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleFullScreen();

    private void ToggleFullScreen()
    {
        if (_fullScreen) ExitFullScreen();
        else EnterFullScreen();
    }

    private void EnterFullScreen()
    {
        if (_fullScreen) return;

        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        GetWindowRect(hwnd, out _restoreBounds);

        // Maximize through WPF so the Windows taskbar remains visible.
        // Do not use a topmost virtual-screen rectangle: that was the source of the taskbar being covered.
        WindowState = WindowState.Maximized;
        ResizeMode = ResizeMode.NoResize;
        Topmost = false;
        UpdateLayout();
        Activate();
        _fullScreen = true;
    }

    private void ExitFullScreen()
    {
        if (!_fullScreen) return;

        Topmost = false;
        ResizeMode = ResizeMode.CanResize;
        WindowState = WindowState.Normal;

        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        SetWindowPos(
            hwnd,
            IntPtr.Zero,
            _restoreBounds.Left,
            _restoreBounds.Top,
            Math.Max((int)MinWidth, _restoreBounds.Right - _restoreBounds.Left),
            Math.Max((int)MinHeight, _restoreBounds.Bottom - _restoreBounds.Top),
            SWP_NOACTIVATE | SWP_SHOWWINDOW);

        _fullScreen = false;
    }
}
