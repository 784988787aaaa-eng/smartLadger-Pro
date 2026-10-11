using System.IO;
using System.Globalization;
using System.Threading;
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
            var savedLanguage = await new AppRepository(db).GetPreferenceAsync("language", CancellationToken.None) ?? "ar-YE";
            ApplyLanguage(savedLanguage);
            var users = new UserRepository(db);
            if (await users.HasAdministratorAsync()) OpenLogin(db, users); else OpenSetup(db, users);
        }
        catch (Exception ex) { MessageBox.Show($"تعذر تشغيل دفتر الأعمال الذكي.\n\n{ex.Message}", "SmartLedger", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1); }
    }
    public static void ApplyLanguage(string language)
    {
        var isEnglish = language.StartsWith("en", StringComparison.OrdinalIgnoreCase);
        var culture = CultureInfo.GetCultureInfo(isEnglish ? "en-US" : language is "ar-SA" ? "ar-SA" : "ar-YE");
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        var dictionaries = Current.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(d => d.Source is not null && d.Source.OriginalString.Contains("Resources/Strings.", StringComparison.OrdinalIgnoreCase));
        if (existing is not null) dictionaries.Remove(existing);
        var file = isEnglish ? "Strings.en.xaml" : "Strings.ar.xaml";
        dictionaries.Add(new ResourceDictionary { Source = new Uri($"/SmartLedger.App;component/Resources/{file}", UriKind.Relative) });
        var direction = isEnglish ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
        foreach (Window window in Current.Windows) window.FlowDirection = direction;
    }
    private static void OpenLogin(Database db, UserRepository users) { var session = new AppSession(); var audit = new AuditRepository(db); var w = new Views.LoginWindow(users, new PasswordHasher(), session, () => OpenMain(db, session), audit); Current.MainWindow = w; w.Show(); }
    private static void OpenSetup(Database db, UserRepository users) { var w = new Views.BootstrapWindow(users, new PasswordHasher(), () => OpenLogin(db, users)); Current.MainWindow = w; w.Show(); }
    private static void OpenMain(Database db, AppSession session) { var users = new UserRepository(db); var reset = new ResetDataService(db, users); var vm = new MainViewModel(new AppRepository(db, session), users, session, new PrivacyStateService(), new AuditRepository(db), reset, () => { var old = Current.MainWindow; OpenSetup(db, users); if (old is not null && old != Current.MainWindow) old.Close(); }); var w = new Views.MainWindow(vm); Current.MainWindow = w; w.Show(); foreach (Window x in Current.Windows) if (x != w) x.Close(); }
}
