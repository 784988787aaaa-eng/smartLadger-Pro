using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;
using SmartLedger.Infrastructure.Audit;
using SmartLedger.Infrastructure.Backup;
using SmartLedger.Infrastructure.Data;
using SmartLedger.Infrastructure.Security;
using SmartLedger.App.Security;
using SmartLedger.App.Views;

namespace SmartLedger.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IAppRepository repo;
    private readonly UserRepository users;
    private readonly IAppSession session;
    private readonly AuditRepository audit;
    private readonly BackupService backup = new(LocalAppPaths.Root);
    private string logoPath = "";

    [ObservableProperty] private string businessName = "";
    [ObservableProperty] private string phone = "";
    [ObservableProperty] private string address = "";
    [ObservableProperty] private string currency = "ريال";
    [ObservableProperty] private string taxRegistrationNumber = "";
    [ObservableProperty] private string commercialRegistrationNumber = "";
    [ObservableProperty] private string invoiceTerms = "";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string statusText = "";
    [ObservableProperty] private string lastBackupText = "لم تُنفذ عملية نسخ احتياطي من هذه الجلسة.";
    [ObservableProperty] private BitmapImage? logoPreview;
    [ObservableProperty] private bool showLogo;
    [ObservableProperty] private string selectedLanguage = "ar-YE";
    [ObservableProperty] private IReadOnlyList<string> languages = new[] { "ar-YE", "ar-SA", "en-US" };
    [ObservableProperty] private IReadOnlyList<MessageTemplate> messageTemplates = Array.Empty<MessageTemplate>();
    [ObservableProperty] private MessageTemplate? selectedTemplate;
    [ObservableProperty] private string templateName = "";
    [ObservableProperty] private string templateBody = "";
    [ObservableProperty] private string diagnosticsText = "اضغط تحديث لعرض تشخيصات النظام.";
    [ObservableProperty] private bool canResetData;
    private readonly ResetDataService? resetService;
    private readonly Action? resetCompleted;

    public SettingsViewModel(IAppRepository repo, UserRepository users, IAppSession session, AuditRepository audit, ResetDataService? resetService = null, Action? resetCompleted = null)
    {
        this.repo = repo;
        this.users = users;
        this.session = session;
        this.audit = audit;
        this.resetService = resetService;
        this.resetCompleted = resetCompleted;
    }

    public async Task LoadAsync()
    {
        try
        {
            var x = await repo.GetIdentityAsync();
            BusinessName = x.BusinessName;
            Phone = x.Phone;
            Address = x.Address;
            Currency = x.Currency;
            TaxRegistrationNumber = x.TaxRegistrationNumber;
            CommercialRegistrationNumber = x.CommercialRegistrationNumber;
            InvoiceTerms = x.InvoiceTerms;
            logoPath = x.LogoPath;
            LoadLogoPreview();
            SelectedLanguage = await repo.GetPreferenceAsync("language", CancellationToken.None) ?? "ar-YE";
            MessageTemplates = await repo.GetMessageTemplatesAsync();
            if (MessageTemplates.Count == 0)
            {
                var defaults = new[]
                {
                    new MessageTemplate(Guid.NewGuid(), "رسالة كشف حساب العميل", "مرحباً {CustomerName}، كشف حساب من {StoreName} بتاريخ {Date}. الرصيد: {RemainingAmount} {Currency}.", true),
                    new MessageTemplate(Guid.NewGuid(), "رسالة سند قبض / صرف", "مرحباً {CustomerName}، سند رقم {InvoiceNumber} من {StoreName}. المبلغ: {TotalAmount} {Currency}. التاريخ: {Date}.", true),
                    new MessageTemplate(Guid.NewGuid(), "رسالة فاتورة مبيعات", "مرحباً {CustomerName}، فاتورة {InvoiceNumber} من {StoreName}. الإجمالي: {TotalAmount} {Currency}، المدفوع: {PaidAmount}، المتبقي: {RemainingAmount}. التاريخ: {Date}.", true)
                };
                var saved = new List<MessageTemplate>();
                foreach (var template in defaults)
                {
                    try { saved.Add(await repo.SaveMessageTemplateAsync(template)); }
                    catch { break; }
                }
                if (saved.Count > 0) MessageTemplates = saved;
            }
            var initialCulture = CultureInfo.GetCultureInfo(SelectedLanguage);
            CultureInfo.DefaultThreadCurrentCulture = initialCulture;
            CultureInfo.DefaultThreadCurrentUICulture = initialCulture;
            var initialDirection = SelectedLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            foreach (Window window in Application.Current.Windows) window.FlowDirection = initialDirection;
            await RefreshDiagnosticsAsync();
            var current = session.CurrentUser;
            CanResetData = current is { IsAdministrator: true } && await users.CanAsync(current.Id, PermissionKeys.ResetData);
            StatusText = "";
        }
        catch (Exception ex)
        {
            StatusText = $"تعذر تحميل الإعدادات: {ex.Message}";
        }
    }

    partial void OnSelectedTemplateChanged(MessageTemplate? value)
    {
        TemplateName = value?.Name ?? string.Empty;
        TemplateBody = value?.Body ?? string.Empty;
    }

    [RelayCommand]
    private async Task ApplyLanguageAsync()
    {
        if (!new[] { "ar-YE", "ar-SA", "en-US" }.Contains(SelectedLanguage, StringComparer.OrdinalIgnoreCase))
        {
            StatusText = "اختر لغة مدعومة.";
            return;
        }
        try
        {
            await repo.SavePreferenceAsync("language", SelectedLanguage);
            var culture = CultureInfo.GetCultureInfo(SelectedLanguage);
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            var direction = SelectedLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;
            foreach (Window window in Application.Current.Windows) window.FlowDirection = direction;
            StatusText = SelectedLanguage == "en-US" ? "Language and layout direction updated. Some screen labels remain Arabic." : "تم تحديث اللغة واتجاه الواجهة دون إعادة تشغيل.";
        }
        catch (Exception ex) { StatusText = $"تعذر تطبيق اللغة: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task SaveTemplateAsync()
    {
        if (string.IsNullOrWhiteSpace(TemplateName) || string.IsNullOrWhiteSpace(TemplateBody))
        {
            StatusText = "أدخل اسم القالب ومحتواه.";
            return;
        }
        try
        {
            var template = new MessageTemplate(SelectedTemplate?.Id ?? Guid.NewGuid(), TemplateName.Trim(), TemplateBody.Trim(), SelectedTemplate?.IsSystem ?? false);
            var saved = await repo.SaveMessageTemplateAsync(template);
            MessageTemplates = await repo.GetMessageTemplatesAsync();
            SelectedTemplate = MessageTemplates.FirstOrDefault(x => x.Id == saved.Id);
            StatusText = "تم حفظ قالب الرسائل.";
        }
        catch (Exception ex) { StatusText = ex.Message; }
    }

    [RelayCommand]
    private async Task RefreshDiagnosticsAsync()
    {
        try
        {
            var dbPath = Path.Combine(LocalAppPaths.Root, "smartledger.db");
            var bytes = File.Exists(dbPath) ? new FileInfo(dbPath).Length : 0L;
            var db = new Database(LocalAppPaths.Root);
            await using var connection = db.Open();
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";
            var schema = Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            command.CommandText = "SELECT (SELECT COUNT(*) FROM Products)+(SELECT COUNT(*) FROM Invoices)+(SELECT COUNT(*) FROM People)+(SELECT COUNT(*) FROM Expenses);";
            var records = Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            DiagnosticsText = $"SmartLedger Pro — Enterprise Edition | Runtime: {Environment.Version} | Schema: v{schema} | DB: {bytes / 1024d / 1024d:N2} MB | سجلات الأصناف والفواتير والحسابات والمصروفات: {records:N0} | الذاكرة المُدارة: {GC.GetTotalMemory(false) / 1024d / 1024d:N2} MB";
        }
        catch (Exception ex) { DiagnosticsText = $"تعذر جمع التشخيصات: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        var name = BusinessName.Trim();
        var phoneValue = Phone.Trim();
        var addressValue = Address.Trim();
        var currencyValue = Currency.Trim();
        if (name.Length is < 2 or > 120)
        {
            StatusText = "اسم المنشأة مطلوب، ويجب ألا يتجاوز 120 حرفًا.";
            return;
        }
        if (phoneValue.Length > 40 || phoneValue.Any(char.IsControl))
        {
            StatusText = "رقم الهاتف طويل جدًا أو يحتوي على محارف غير صالحة.";
            return;
        }
        if (addressValue.Length > 250 || addressValue.Any(char.IsControl))
        {
            StatusText = "العنوان طويل جدًا أو يحتوي على محارف غير صالحة.";
            return;
        }
        if (currencyValue.Length is < 1 or > 12 || currencyValue.Any(char.IsControl))
        {
            StatusText = "أدخل اسمًا أو رمزًا صالحًا للعملة (حتى 12 حرفًا).";
            return;
        }

        IsBusy = true;
        StatusText = "جارٍ حفظ إعدادات المنشأة...";
        try
        {
            await repo.SaveIdentityAsync(new BusinessIdentity(name, logoPath, phoneValue, addressValue, currencyValue) { TaxRegistrationNumber = TaxRegistrationNumber.Trim(), CommercialRegistrationNumber = CommercialRegistrationNumber.Trim(), InvoiceTerms = InvoiceTerms.Trim() });
            BusinessName = name;
            Phone = phoneValue;
            Address = addressValue;
            Currency = currencyValue;
            TaxRegistrationNumber = TaxRegistrationNumber.Trim();
            CommercialRegistrationNumber = CommercialRegistrationNumber.Trim();
            InvoiceTerms = InvoiceTerms.Trim();
            StatusText = "تم حفظ إعدادات المنشأة بنجاح. ستستخدم التقارير هذه البيانات عند إنشائها.";
        }
        catch (Exception ex)
        {
            StatusText = $"فشل الحفظ؛ لم تُعتمد التغييرات: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void ChooseLogo()
    {
        var dialog = new OpenFileDialog
        {
            Title = "اختر شعار المنشأة",
            Filter = "ملفات الصور المدعومة (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var file = new FileInfo(dialog.FileName);
            if (file.Length is <= 0 or > 5 * 1024 * 1024)
                throw new InvalidDataException("يجب ألا يتجاوز حجم الشعار 5 ميغابايت.");
            var extension = file.Extension.ToLowerInvariant();
            if (extension is not (".png" or ".jpg" or ".jpeg" or ".bmp"))
                throw new InvalidDataException("نوع ملف الشعار غير مدعوم.");

            var probe = new BitmapImage();
            probe.BeginInit();
            probe.CacheOption = BitmapCacheOption.OnLoad;
            probe.UriSource = new Uri(file.FullName);
            probe.DecodePixelWidth = 600;
            probe.EndInit();
            probe.Freeze();

            var folder = Path.Combine(LocalAppPaths.Root, "Branding");
            Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, "business-logo" + extension);
            var temporary = target + ".tmp";
            File.Copy(file.FullName, temporary, true);
            File.Move(temporary, target, true);
            foreach (var old in Directory.EnumerateFiles(folder, "business-logo.*"))
                if (!string.Equals(old, target, StringComparison.OrdinalIgnoreCase)) try { File.Delete(old); } catch { }
            logoPath = target;
            LogoPreview = probe;
            ShowLogo = true;
            StatusText = "تم تجهيز الشعار. اضغط «حفظ الإعدادات» لاعتماده.";
        }
        catch (Exception ex)
        {
            StatusText = $"تعذر استخدام الشعار: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RemoveLogo()
    {
        logoPath = "";
        LogoPreview = null;
        ShowLogo = false;
        StatusText = "تم تحديد إزالة الشعار. اضغط «حفظ الإعدادات» لاعتماد التغيير.";
    }

    private void LoadLogoPreview()
    {
        LogoPreview = null;
        ShowLogo = false;
        if (string.IsNullOrWhiteSpace(logoPath) || !File.Exists(logoPath)) return;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 240;
            image.UriSource = new Uri(logoPath);
            image.EndInit();
            image.Freeze();
            LogoPreview = image;
            ShowLogo = true;
        }
        catch { StatusText = "تعذر معاينة الشعار المحفوظ؛ يمكنك اختيار صورة أخرى."; }
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        if (!await CanBackupAsync()) return;
        var dialog = new OpenFolderDialog { Title = "اختر مجلد حفظ النسخة الاحتياطية" };
        if (dialog.ShowDialog() != true) return;

        IsBusy = true;
        StatusText = "جارٍ إنشاء النسخة الاحتياطية...";
        try
        {
            var info = await backup.CreateAsync(dialog.FolderName);
            await audit.LogActionAsync(session.CurrentUser?.Id, "BACKUP_CREATE", "Backup", null, new { info.FilePath, SizeBytes = new FileInfo(info.FilePath).Length });
            LastBackupText = $"آخر نسخة في هذه الجلسة: {info.CreatedAt:yyyy/MM/dd HH:mm} — {new FileInfo(info.FilePath).Length:N0} بايت";
            StatusText = $"تم إنشاء النسخة الاحتياطية والتحقق من الملف: {info.FilePath}";
        }
        catch (OperationCanceledException) { StatusText = "تم إلغاء النسخ الاحتياطي."; }
        catch (Exception ex)
        {
            try { await audit.LogActionAsync(session.CurrentUser?.Id, "BACKUP_CREATE_FAILED", "Backup", null, new { ErrorType = ex.GetType().Name }); } catch { }
            StatusText = $"فشل النسخ الاحتياطي: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RestoreBackupAsync()
    {
        if (!await CanBackupAsync()) return;
        var dialog = new OpenFileDialog
        {
            Title = "اختر نسخة SmartLedger الاحتياطية",
            Filter = "SmartLedger Backup (*.zip)|*.zip",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog() != true) return;
        var answer = MessageBox.Show("ستستبدل الاستعادة بيانات قاعدة العمل الحالية وفق آلية الاستعادة. يُنصح بإنشاء نسخة احتياطية أولًا. هل تريد المتابعة؟",
            "تأكيد استعادة النسخة", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        IsBusy = true;
        StatusText = "جارٍ التحقق من النسخة واستعادتها...";
        try
        {
            await backup.RestoreAsync(dialog.FileName);
            await audit.LogActionAsync(session.CurrentUser?.Id, "BACKUP_RESTORE", "Backup", null, new { FileName = Path.GetFileName(dialog.FileName) });
            StatusText = "تمت استعادة النسخة الاحتياطية بنجاح. أعد فتح التطبيق إذا طُلب ذلك.";
        }
        catch (OperationCanceledException) { StatusText = "تم إلغاء الاستعادة."; }
        catch (Exception ex)
        {
            try { await audit.LogActionAsync(session.CurrentUser?.Id, "BACKUP_RESTORE_FAILED", "Backup", null, new { ErrorType = ex.GetType().Name }); } catch { }
            StatusText = $"فشلت الاستعادة: {ex.Message}";
        }
        finally { IsBusy = false; }
    }


    [RelayCommand]
    private async Task ResetDataAsync()
    {
        if (IsBusy) return;
        var current = session.CurrentUser;
        if (current is null || !current.IsAdministrator || !await users.CanAsync(current.Id, PermissionKeys.ResetData))
        {
            StatusText = "لا تملك صلاحية تهيئة بيانات التطبيق.";
            return;
        }
        if (resetService is null)
        {
            StatusText = "خدمة التهيئة غير متاحة في هذا السياق.";
            return;
        }
        var dialog = new ResetDataWindow(resetService, session) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;
        new RememberedLoginStore().Clear();
        session.Logout();
        resetCompleted?.Invoke();
    }
    private async Task<bool> CanBackupAsync()
    {
        if (IsBusy) return false;
        var user = session.CurrentUser;
        if (user is null || !await users.CanAsync(user.Id, PermissionKeys.Backup))
        {
            StatusText = "لا تملك صلاحية النسخ الاحتياطي والاستعادة.";
            return false;
        }
        return true;
    }
}
