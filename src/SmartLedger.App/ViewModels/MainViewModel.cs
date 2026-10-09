using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Input;
using SmartLedger.App.Converters;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;
using SmartLedger.Infrastructure.Data;
using SmartLedger.Infrastructure.Audit;

namespace SmartLedger.App.ViewModels;

public enum AppPage
{
    Dashboard, Accounts, Operations, Sales, Purchases, Inventory, Expenses,
    Reports, Users, AuditLogs, RecycleBin, Settings
}

public partial class MainViewModel : ObservableObject
{
    private readonly IAppRepository _repo;
    private readonly UserRepository _users;
    private readonly IAppSession _session;
    private readonly IPrivacyStateService _privacy;

    [ObservableProperty] private AppPage currentPage = AppPage.Dashboard;
    [ObservableProperty] private object? currentViewModel;
    [ObservableProperty] private bool sidebarCollapsed;
    [ObservableProperty] private string globalSearch = "";
    [ObservableProperty] private bool isGlobalSearchOpen;
    [ObservableProperty] private IReadOnlyList<GlobalSearchResult> globalSearchResults = Array.Empty<GlobalSearchResult>();
    [ObservableProperty] private bool isNotificationsOpen;
    [ObservableProperty] private IReadOnlyList<string> notifications = Array.Empty<string>();

    public double SidebarWidth => SidebarCollapsed ? 72 : 250;
    public System.Windows.Visibility SidebarTextVisibility =>
        SidebarCollapsed ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
    public bool PrivacyEnabled => _privacy.IsPrivate;
    public string PageTitle => GetPageTitle(CurrentPage);
    public AppUser User => _session.CurrentUser!;

    public int NotificationCount => Notifications.Count;
    public ObservableCollection<QuickActionItem> QuickActions { get; } = new();

    partial void OnGlobalSearchChanged(string value) => ApplyCurrentScreenSearch(value);

    public void ApplyCurrentScreenSearch(string value)
    {
        var query = value.Trim();
        switch (CurrentPage)
        {
            case AppPage.Accounts:
                Accounts.Search = query;
                break;
            case AppPage.Inventory:
                Inventory.Search = query;
                break;
        }
    }

    public MainViewModel(
        IAppRepository repo,
        UserRepository users,
        IAppSession session,
        IPrivacyStateService privacy,
        AuditRepository auditRepository, ResetDataService? resetService = null, Action? resetCompleted = null)
    {
        _repo = repo;
        _users = users;
        _session = session;
        _privacy = privacy;

        PrivacyMoneyConverter.IsPrivate = privacy.IsPrivate;
        _privacy.Changed += (_, _) =>
        {
            PrivacyMoneyConverter.IsPrivate = _privacy.IsPrivate;
            OnPropertyChanged(nameof(PrivacyEnabled));
        };

        Dashboard = new(repo);
        Accounts = new(repo, session);
        Operations = new(repo, session);
        Inventory = new(repo);
        Sales = new(repo, session);
        Purchases = new(repo, session);
        Expenses = new(repo, session);
        Reports = new(repo);
        Users = new(users, session);
        AuditLogs = new(auditRepository);
        RecycleBin = new(repo);
        Settings = new(repo, users, session, auditRepository, resetService, resetCompleted);

        QuickActions.Add(new QuickActionItem("حساب جديد", "F7", QuickAddAccountCommand));
        QuickActions.Add(new QuickActionItem("عملية جديدة", "F8", QuickOperationCommand));
        QuickActions.Add(new QuickActionItem("فاتورة بيع", "F9", QuickSaleCommand));
        QuickActions.Add(new QuickActionItem("فاتورة شراء", "F10", QuickPurchaseCommand));
    }

    public DashboardViewModel Dashboard { get; }
    public AccountsViewModel Accounts { get; }
    public OperationsViewModel Operations { get; }
    public InventoryViewModel Inventory { get; }
    public SalesViewModel Sales { get; }
    public PurchasesViewModel Purchases { get; }
    public ExpensesViewModel Expenses { get; }
    public ReportsViewModel Reports { get; }
    public UsersViewModel Users { get; }
    public AuditLogViewModel AuditLogs { get; }
    public RecycleBinViewModel RecycleBin { get; }
    public SettingsViewModel Settings { get; }

    private readonly object _navigationSync = new();
    private Task<bool> _navigationTail = Task.FromResult(true);
    private int _navigationVersion;

    [RelayCommand]
    private Task<bool> Navigate(AppPage page)
    {
        var version = Interlocked.Increment(ref _navigationVersion);
        Task<bool> scheduled;

        lock (_navigationSync)
        {
            scheduled = RunNavigationAsync(_navigationTail, page, version);
            _navigationTail = scheduled;
        }

        return scheduled;
    }

    private async Task<bool> RunNavigationAsync(Task<bool> previous, AppPage page, int version)
    {
        try
        {
            await previous;

            // Rapid clicks are coalesced: only the newest request may publish a page.
            if (version != _navigationVersion)
                return false;

            if (!await CanNavigateAsync(page))
                return false;

            if (CurrentPage != page && HasOpenInputDialog())
            {
                System.Windows.MessageBox.Show(
                    "لديك نافذة إدخال مفتوحة. أغلقها أو احفظ البيانات قبل الانتقال إلى صفحة أخرى.",
                    "SmartLedger",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return false;
            }

            if (CurrentPage == page && CurrentViewModel is not null)
            {
                IsNotificationsOpen = false;
                return true;
            }

            var target = page switch
            {
                AppPage.Dashboard => (object)Dashboard,
                AppPage.Accounts => Accounts,
                AppPage.Operations => Operations,
                AppPage.Sales => Sales,
                AppPage.Purchases => Purchases,
                AppPage.Inventory => Inventory,
                AppPage.Expenses => Expenses,
                AppPage.Reports => Reports,
                AppPage.Users => Users,
                AppPage.AuditLogs => AuditLogs,
                AppPage.RecycleBin => RecycleBin,
                AppPage.Settings => Settings,
                _ => Dashboard
            };

            await LoadViewModelAsync(target);

            if (version != _navigationVersion)
                return false;

            // Publish CurrentPage and CurrentViewModel only after LoadAsync succeeds.
            CurrentPage = page;
            CurrentViewModel = target;
            IsNotificationsOpen = false;
            OnPropertyChanged(nameof(PageTitle));

            await RefreshNotificationsAsync();
            return true;
        }
        catch (Exception ex)
        {
            LogNavigationFailure(page, ex);
            System.Windows.MessageBox.Show(
                $"تعذر تحميل صفحة {GetPageTitle(page)}.\n\n{ex.Message}",
                "SmartLedger",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return false;
        }
    }

    private async Task<bool> CanNavigateAsync(AppPage page)
    {
        var user = _session.CurrentUser;
        if (user is null || !user.IsActive)
            return false;

        if (user.IsAdministrator)
            return true;

        var requiredPermission = page switch
        {
            AppPage.Reports => PermissionKeys.Reports,
            AppPage.Users => PermissionKeys.Users,
            AppPage.Settings => PermissionKeys.Settings,
            _ => PermissionKeys.View
        };

        if (await _users.CanAsync(user.Id, requiredPermission))
            return true;

        System.Windows.MessageBox.Show(
            $"لا تملك صلاحية الوصول إلى {GetPageTitle(page)}.",
            "SmartLedger",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
        return false;
    }

    private bool HasOpenInputDialog() => CurrentViewModel switch
    {
        AccountsViewModel a => a.IsDialogOpen || a.IsBookDialogOpen,
        OperationsViewModel o => o.IsDialogOpen,
        SalesViewModel s => s.IsDialogOpen,
        PurchasesViewModel p => p.IsDialogOpen,
        InventoryViewModel i => i.IsDialogOpen,
        ExpensesViewModel e => e.IsDialogOpen,
        UsersViewModel u => u.IsDialogOpen,
        _ => false
    };

    private static async Task LoadViewModelAsync(object viewModel)
    {
        switch (viewModel)
        {
            case DashboardViewModel d: await d.LoadAsync(); break;
            case AccountsViewModel a: await a.LoadAsync(); break;
            case OperationsViewModel o: await o.LoadAsync(); break;
            case SalesViewModel s: await s.LoadAsync(); break;
            case PurchasesViewModel p: await p.LoadAsync(); break;
            case InventoryViewModel i: await i.LoadAsync(); break;
            case ExpensesViewModel e: await e.LoadAsync(); break;
            case ReportsViewModel r: await r.LoadAsync(); break;
            case UsersViewModel u: await u.LoadAsync(); break;
            case AuditLogViewModel al: await al.LoadAsync(); break;
            case RecycleBinViewModel b: await b.LoadAsync(); break;
            case SettingsViewModel st: await st.LoadAsync(); break;
        }
    }

    private static void LogNavigationFailure(AppPage page, Exception ex)
    {
        try
        {
            var root = SmartLedger.Infrastructure.Security.LocalAppPaths.Root;
            System.IO.Directory.CreateDirectory(root);
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(root, "navigation-error.log"),
                $"[{DateTimeOffset.Now:O}] Page={page}; Exception={ex}\n");
        }
        catch
        {
            // Diagnostics must never become a second application failure.
        }
    }

    [RelayCommand]
    private async Task ExecuteGlobalSearch()
    {
        var q = GlobalSearch.Trim();
        ApplyCurrentScreenSearch(q);

        if (q.Length < 2)
        {
            GlobalSearchResults = Array.Empty<GlobalSearchResult>();
            IsGlobalSearchOpen = false;
            return;
        }

        // الحسابات والمخزون يستخدمان بحث الشاشة نفسه مباشرة؛ لا حاجة لفتح قائمة نتائج عامة فوق الجدول.
        if (CurrentPage is AppPage.Accounts or AppPage.Inventory)
        {
            GlobalSearchResults = Array.Empty<GlobalSearchResult>();
            IsGlobalSearchOpen = false;
            return;
        }

        try
        {
            GlobalSearchResults = await _repo.SearchGlobalAsync(q, 6);
            IsGlobalSearchOpen = true;
        }
        catch
        {
            GlobalSearchResults = Array.Empty<GlobalSearchResult>();
            IsGlobalSearchOpen = false;
        }
    }

    [RelayCommand]
    private async Task OpenGlobalSearchResult(GlobalSearchResult result)
    {
        if (result is null) return;

        IsGlobalSearchOpen = false;
        GlobalSearch = result.Title;

        switch (result.EntityType)
        {
            case "Person":
                Accounts.Search = result.Title;
                if (CurrentPage != AppPage.Accounts) await Navigate(AppPage.Accounts);
                break;
            case "Product":
                Inventory.Search = result.Title;
                if (CurrentPage != AppPage.Inventory) await Navigate(AppPage.Inventory);
                break;
            case "Book":
                Accounts.Search = result.Title;
                if (CurrentPage != AppPage.Accounts) await Navigate(AppPage.Accounts);
                break;
            case "Operation":
                await Navigate(AppPage.Operations);
                break;
            case "Invoice":
                await Navigate(result.Title.Contains("شراء", StringComparison.Ordinal) ? AppPage.Purchases : AppPage.Sales);
                break;
            case "Expense":
                await Navigate(AppPage.Expenses);
                break;
        }
    }

    [RelayCommand]
    private async Task QuickAddAccount()
    {
        if (!await Navigate(AppPage.Accounts)) return;
        Accounts.OpenAddCommand.Execute(null);
    }

    public async Task OpenOperationForPersonAsync(Guid personId)
    {
        if (!await Navigate(AppPage.Operations)) return;
        Operations.SelectedPersonId = personId;
        Operations.IsDialogOpen = true;
    }

    [RelayCommand]
    private async Task QuickOperation()
    {
        if (!await Navigate(AppPage.Operations)) return;
        Operations.OpenNewCommand.Execute(null);
    }

    [RelayCommand]
    private async Task QuickSale()
    {
        if (!await Navigate(AppPage.Sales)) return;
        Sales.OpenNewCommand.Execute(null);
    }

    [RelayCommand]
    private async Task QuickPurchase()
    {
        if (!await Navigate(AppPage.Purchases)) return;
        Purchases.OpenNewCommand.Execute(null);
    }

    [RelayCommand]
    private async Task QuickAddProduct()
    {
        if (!await Navigate(AppPage.Inventory)) return;
        Inventory.OpenAddCommand.Execute(null);
    }

    [RelayCommand]
    private async Task QuickAddExpense()
    {
        if (!await Navigate(AppPage.Expenses)) return;
        Expenses.OpenAddCommand.Execute(null);
    }

    [RelayCommand]
    private async Task QuickSettleUs()
    {
        if (!await Navigate(AppPage.Operations)) return;
        Operations.SelectedType = OperationType.Receive;
        Operations.OpenNewCommand.Execute(null);
    }

    [RelayCommand]
    private async Task QuickSettleAgainstUs()
    {
        if (!await Navigate(AppPage.Operations)) return;
        Operations.SelectedType = OperationType.Payment;
        Operations.OpenNewCommand.Execute(null);
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        SidebarCollapsed = !SidebarCollapsed;
        OnPropertyChanged(nameof(SidebarWidth));
        OnPropertyChanged(nameof(SidebarTextVisibility));
    }

    [RelayCommand]
    private void TogglePrivacy() => _privacy.Toggle();

    [RelayCommand]
    private async Task ToggleNotificationsAsync()
    {
        if (!IsNotificationsOpen)
            await RefreshNotificationsAsync();
        IsNotificationsOpen = !IsNotificationsOpen;
    }

    [RelayCommand]
    private void DismissNotifications()
    {
        IsNotificationsOpen = false;
    }

    private async Task RefreshNotificationsAsync()
    {
        var list = new List<string>();

        try
        {
            var summary = await _repo.GetDashboardAsync();
            if (summary.OperationsToday > 0)
                list.Add($"تم تسجيل {summary.OperationsToday.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} حركة اليوم.");

            var products = await _repo.GetProductsAsync();
            foreach (var product in products
                         .Where(x => x.AlertThreshold > 0 && x.StockSmallUnits <= x.AlertThreshold)
                         .OrderBy(x => x.StockSmallUnits)
                         .Take(8))
            {
                list.Add($"تنبيه مخزون: «{product.Name}» وصل إلى حد التنبيه.");
            }
        }
        catch
        {
            // Notifications must never block the main application.
        }

        Notifications = list;
        OnPropertyChanged(nameof(NotificationCount));
    }

    public string GetPageTitle(AppPage p) => p switch
    {
        AppPage.Dashboard => "الرئيسية",
        AppPage.Accounts => "الدفاتر والحسابات",
        AppPage.Operations => "العمليات",
        AppPage.Sales => "المبيعات",
        AppPage.Purchases => "المشتريات",
        AppPage.Inventory => "المخزون",
        AppPage.Expenses => "المصروفات",
        AppPage.Reports => "التقارير والتحليلات",
        AppPage.Users => "المستخدمون والصلاحيات",
        AppPage.AuditLogs => "سجل التدقيق",
        AppPage.RecycleBin => "سلة المحذوفات",
        AppPage.Settings => "الإعدادات",
        _ => "الرئيسية"
    };
}

public sealed record QuickActionItem(string Label, string Shortcut, ICommand Command);
