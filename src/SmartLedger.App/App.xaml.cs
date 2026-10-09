using System.IO;
using System.Windows;
using SmartLedger.App.ViewModels;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure;
using SmartLedger.Infrastructure.Data;
using SmartLedger.Infrastructure.Security;
using SmartLedger.Infrastructure.Audit;
namespace SmartLedger.App;
public partial class App : Application
{
    public App() => InitializeComponent();
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, a) => { try { Directory.CreateDirectory(LocalAppPaths.Root); File.WriteAllText(Path.Combine(LocalAppPaths.Root, "startup-error.log"), a.Exception.ToString()); } catch { } a.Handled = true; MessageBox.Show(a.Exception.Message, "SmartLedger", MessageBoxButton.OK, MessageBoxImage.Error); };
        try
        {
            var db = await SmartLedger.Infrastructure.Infrastructure.CreateDatabaseAsync(LocalAppPaths.Root);
            var users = new UserRepository(db);
            if (await users.HasAdministratorAsync()) OpenLogin(db, users); else OpenSetup(db, users);
        }
        catch (Exception ex) { MessageBox.Show($"تعذر تشغيل دفتر الأعمال الذكي.\n\n{ex.Message}", "SmartLedger", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1); }
    }
    private static void OpenLogin(Database db, UserRepository users) { var session = new AppSession(); var audit = new AuditRepository(db); var w = new Views.LoginWindow(users, new PasswordHasher(), session, () => OpenMain(db, session), audit); Current.MainWindow = w; w.Show(); }
    private static void OpenSetup(Database db, UserRepository users) { var w = new Views.BootstrapWindow(users, new PasswordHasher(), () => OpenLogin(db, users)); Current.MainWindow = w; w.Show(); }
    private static void OpenMain(Database db, AppSession session) { var users = new UserRepository(db); var reset = new ResetDataService(db, users); var vm = new MainViewModel(new AppRepository(db, session), users, session, new PrivacyStateService(), new AuditRepository(db), reset, () => { var old = Current.MainWindow; OpenSetup(db, users); if (old is not null && old != Current.MainWindow) old.Close(); }); var w = new Views.MainWindow(vm); Current.MainWindow = w; w.Show(); foreach (Window x in Current.Windows) if (x != w) x.Close(); }
}
