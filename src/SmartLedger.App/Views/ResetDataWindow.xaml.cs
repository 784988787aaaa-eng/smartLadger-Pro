using System.Windows;
using Microsoft.Win32;
using SmartLedger.Core.Abstractions;
using SmartLedger.Infrastructure.Data;

namespace SmartLedger.App.Views;

public partial class ResetDataWindow : Window
{
    private readonly ResetDataService resetService;
    private readonly IAppSession session;
    private string? verifiedBackupPath;
    private bool busy;

    public ResetDataWindow(ResetDataService resetService, IAppSession session)
    {
        this.resetService = resetService;
        this.session = session;
        InitializeComponent();
        Closed += (_, _) => AdminPassword.Clear();
    }

    private async void Prepare_Click(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        StatusText.Text = string.Empty;
        var user = session.CurrentUser;
        if (user is null || !user.IsAdministrator)
        {
            StatusText.Text = "هذه العملية متاحة للمسؤول المخوّل فقط.";
            return;
        }
        if (!string.Equals(ConfirmationInput.Text, ResetDataService.ConfirmationPhrase, StringComparison.Ordinal))
        {
            StatusText.Text = "عبارة التأكيد غير مطابقة؛ لم تُنفذ أي عملية.";
            return;
        }
        if (string.IsNullOrEmpty(AdminPassword.Password))
        {
            StatusText.Text = "أدخل كلمة مرور المسؤول للتحقق من هويته.";
            return;
        }

        var folderDialog = new OpenFolderDialog { Title = "اختر موقعًا آمنًا خارج مجلد بيانات التطبيق لحفظ النسخة الاحتياطية" };
        if (folderDialog.ShowDialog() != true) return;

        SetBusy(true);
        try
        {
            var info = await resetService.CreateVerifiedBackupAsync(user.Id, AdminPassword.Password, folderDialog.FolderName);
            verifiedBackupPath = info.FilePath;
            BackupStatus.Text = "نجح إنشاء النسخة الاحتياطية واجتازت فحص البصمة والمخطط وسلامة SQLite.";
            BackupPathText.Text = info.FilePath;
            BackupStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
            ExecuteButton.IsEnabled = true;
            PrepareButton.IsEnabled = false;
            AdminPassword.IsEnabled = false;
            ConfirmationInput.IsEnabled = false;
            StatusText.Text = "راجع مسار النسخة أعلاه. لن يبدأ الحذف إلا بعد ضغط زر التنفيذ وتأكيده مرة أخيرة.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "تعذر تجهيز النسخة الاحتياطية أو التحقق الأمني. لم تُحذف أي بيانات. " + ex.Message;
        }
        finally { SetBusy(false); }
    }

    private async void Execute_Click(object sender, RoutedEventArgs e)
    {
        if (busy || string.IsNullOrWhiteSpace(verifiedBackupPath)) return;
        var user = session.CurrentUser;
        if (user is null) { StatusText.Text = "انتهت الجلسة الحالية؛ أعد تسجيل الدخول."; return; }
        var answer = MessageBox.Show(
            "تم التحقق من النسخة الاحتياطية التالية:\n" + verifiedBackupPath + "\n\nسيتم الآن حذف بيانات العمل وإلغاء صلاحية الجلسات القديمة. لا يمكن التراجع عن العملية. هل تريد التنفيذ النهائي؟",
            "تأكيد نهائي لتهيئة البيانات", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        SetBusy(true);
        try
        {
            await resetService.ResetAsync(user.Id, AdminPassword.Password, ConfirmationInput.Text, verifiedBackupPath);
            MessageBox.Show("اكتملت تهيئة البيانات، واجتازت قاعدة البيانات فحوص السلامة. النسخة الاحتياطية محفوظة في:\n" + verifiedBackupPath + "\n\nسيبدأ الآن إعداد المسؤول الأول.", "اكتملت التهيئة", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            StatusText.Text = "لم تكتمل التهيئة. عند فشل التحقق بعد الحذف تُستعاد النسخة الاحتياطية تلقائيًا. " + ex.Message;
        }
        finally { SetBusy(false); }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; }
    private void SetBusy(bool value) { busy = value; PrepareButton.IsEnabled = !value && verifiedBackupPath is null; ExecuteButton.IsEnabled = !value && verifiedBackupPath is not null; }
}