$ErrorActionPreference='Stop'
$root='C:\Users\pc\SmartLedger-Build-Clean-20261010'
$changes=[System.Collections.Generic.List[string]]::new()
function Read-Utf8([string]$rel) { [IO.File]::ReadAllText((Join-Path $root $rel),[Text.Encoding]::UTF8) }
function Write-Utf8([string]$rel,[string]$text) { [IO.File]::WriteAllText((Join-Path $root $rel),$text,[Text.UTF8Encoding]::new($false)) }
function Append-Resource([string]$rel,[string]$addition,[string]$label) {
 $t=Read-Utf8 $rel
 if($t.Contains($label)){return}
 $i=$t.LastIndexOf('</ResourceDictionary>')
 if($i -lt 0){throw "No ResourceDictionary end in $rel"}
 Write-Utf8 $rel ($t.Insert($i,$addition+[Environment]::NewLine))
 $changes.Add("$rel :: $label")
}
# Add an account-list empty state; regex avoids newline-format assumptions.
$rel='src\SmartLedger.App\Views\ModuleTemplates.xaml'
$t=Read-Utf8 $rel
if(-not $t.Contains('AccountsEmptyStatePhase1')) {
 $pattern='(?s)(</DataGrid>\s*)(<Border Grid.Row="1" Background="#0B132B" BorderBrush="#1E293B" BorderThickness="1,1,1,0" CornerRadius="0,0,8,8" Padding="10,0">\s*<Grid FlowDirection="RightToLeft">\s*<TextBlock Text="\{Binding VisibleAccountsCount)'
 $replacement='$1<Grid x:Name="AccountsEmptyStatePhase1" Grid.Row="0" Panel.ZIndex="5" Background="#F20F172A" Visibility="Collapsed"><Grid.Style><Style TargetType="Grid"><Setter Property="Visibility" Value="Collapsed"/><Style.Triggers><DataTrigger Binding="{Binding People.Count}" Value="0"><Setter Property="Visibility" Value="Visible"/></DataTrigger></Style.Triggers></Style></Grid.Style><StackPanel HorizontalAlignment="Center" VerticalAlignment="Center" MaxWidth="420" Margin="24"><TextBlock Text="◇" FontSize="42" Foreground="{StaticResource BrushMuted}" HorizontalAlignment="Center" TextAlignment="Center"/><TextBlock Text="لا توجد حسابات في هذا العرض" FontSize="18" FontWeight="SemiBold" HorizontalAlignment="Center" TextAlignment="Center" Margin="0,8,0,4"/><TextBlock Text="أضف حسابًا جديدًا أو غيّر مرشح التصنيف لعرض الحسابات المتاحة." Style="{StaticResource CaptionText}" TextWrapping="Wrap" HorizontalAlignment="Center" TextAlignment="Center" Margin="0,0,0,16"/><Button Content="+ إضافة حساب جديد" Command="{Binding OpenAddCommand}" Style="{StaticResource PrimaryButton}" Width="190" Height="40" HorizontalAlignment="Center"/></StackPanel></Grid>$2'
 $m=[regex]::Matches($t,$pattern)
 if($m.Count -ne 1){throw "Accounts empty-state regex expected 1 match, found $($m.Count)"}
 $t=[regex]::Replace($t,$pattern,$replacement,1)
 Write-Utf8 $rel $t
 $changes.Add("$rel :: account list empty state and quick action")
}
Append-Resource 'src\SmartLedger.App\Themes\Colors.xaml' @'
    <SolidColorBrush x:Key="BrushInputBorder" Color="#344761"/>
    <SolidColorBrush x:Key="BrushFocusRing" Color="#00B4D8"/>
    <SolidColorBrush x:Key="BrushDestructive" Color="#DC3545"/>
    <SolidColorBrush x:Key="BrushCardBorder" Color="#263A5A"/>
'@ 'BrushInputBorder'
Append-Resource 'src\SmartLedger.App\Themes\Styles.xaml' @'
  <!-- Phase 1 reusable enterprise tokens; existing styles remain compatible. -->
  <Style x:Key="EnterpriseInput" TargetType="TextBox" BasedOn="{StaticResource Field}">
    <Setter Property="Height" Value="38"/>
    <Setter Property="BorderBrush" Value="{StaticResource BrushInputBorder}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="UseLayoutRounding" Value="True"/>
    <Setter Property="SnapsToDevicePixels" Value="True"/>
  </Style>
  <Style x:Key="EnterpriseButton" TargetType="Button" BasedOn="{StaticResource BaseButton}">
    <Setter Property="Height" Value="38"/>
    <Setter Property="MinWidth" Value="88"/>
    <Setter Property="Padding" Value="14,6"/>
    <Setter Property="UseLayoutRounding" Value="True"/>
    <Setter Property="SnapsToDevicePixels" Value="True"/>
  </Style>
  <Style x:Key="EnterpriseCard" TargetType="Border" BasedOn="{StaticResource Card}">
    <Setter Property="CornerRadius" Value="7"/>
    <Setter Property="BorderBrush" Value="{StaticResource BrushCardBorder}"/>
    <Setter Property="BorderThickness" Value="1"/>
  </Style>
'@ 'EnterpriseInput'
Append-Resource 'src\SmartLedger.App\Resources\Strings.ar.xaml' @'
  <sys:String x:Key="Nav.Reports">التقارير</sys:String>
  <sys:String x:Key="Nav.Users">المستخدمون والصلاحيات</sys:String>
  <sys:String x:Key="Nav.RecycleBin">سلة المحذوفات</sys:String>
  <sys:String x:Key="Nav.About">عن التطبيق والهوية / About</sys:String>
'@ 'Nav.Reports'
Append-Resource 'src\SmartLedger.App\Resources\Strings.en.xaml' @'
  <sys:String x:Key="Nav.Reports">Reports</sys:String>
  <sys:String x:Key="Nav.Users">Users &amp; Permissions</sys:String>
  <sys:String x:Key="Nav.RecycleBin">Recycle Bin</sys:String>
  <sys:String x:Key="Nav.About">About &amp; Identity</sys:String>
'@ 'Nav.Reports'
# Replace font token while preserving existing Typography edits.
$rel='src\SmartLedger.App\Themes\Typography.xaml'; $t=Read-Utf8 $rel
$t=$t.Replace('<FontFamily x:Key="ArabicFont">Segoe UI</FontFamily>','<FontFamily x:Key="ArabicFont">Segoe UI Variable Text, Cairo, Segoe UI, Tahoma</FontFamily>')
Write-Utf8 $rel $t; $changes.Add("$rel :: Arabic font fallback stack")
# Titlebar double-click support, regex over CRLF/LF.
$rel='src\SmartLedger.App\Views\MainWindow.xaml.cs'; $t=Read-Utf8 $rel
$pattern='(?s)    private void TopBar_MouseLeftButtonDown\(object sender, MouseButtonEventArgs e\)\s*\{.*?\n    \}'
$replacement=@'
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
'@
if($t -match 'e.ClickCount == 2'){}else{
 $n=[regex]::Matches($t,$pattern)
 if($n.Count -ne 1){throw "TopBar handler match count $($n.Count)"}
 $t=[regex]::Replace($t,$pattern,[System.Text.RegularExpressions.MatchEvaluator]{param($m)$replacement},1)
 Write-Utf8 $rel $t; $changes.Add("$rel :: titlebar drag and double-click maximize/restore")
}
# Formal Arabic voucher fallback, only update the fallback constant.
$rel='src\SmartLedger.App\ViewModels\AccountsViewModel.cs'; $t=Read-Utf8 $rel
$pattern='const string fallback = "مرحباً \{CustomerName\}،\\\\nسند رقم: #\{InvoiceNumber\} من \{StoreName\}\.\\\\nالمبلغ: \{TotalAmount\} \{Currency\}\.\\\\nالبيان: \{Description\}\.\\\\nالرصيد المتبقي: \{RemainingAmount\} \{Currency\}\.\\\\nالتاريخ: \{Date\}\.";'
if($t -match 'السلام عليكم ورحمة الله وبركاته، عزيزنا \{CustomerName\}'){
}else{
 $replacement='const string fallback = "السلام عليكم ورحمة الله وبركاته، عزيزنا {CustomerName}، نود إحاطتكم بتفاصيل المعاملة رقم {InvoiceNumber} بتاريخ {Date}: المبلغ: {TotalAmount} {Currency} - البيان: {Description} - الرصيد الحالي: {RemainingAmount} {Currency}. شكراً لتعاملكم معنا.";'
 if(-not [regex]::IsMatch($t,$pattern)){throw 'Voucher fallback constant not found'}
 $t=[regex]::Replace($t,$pattern,[System.Text.RegularExpressions.MatchEvaluator]{param($m)$replacement},1)
 Write-Utf8 $rel $t; $changes.Add("$rel :: formal Arabic voucher fallback")
}
# Report (verification appended only after build/test).
$report=@'
# SmartLedger Phase 1 UI/UX Implementation Report

## Execution scope
- Workspace: C:\Users\pc\SmartLedger-Build-Clean-20261010
- Branch: main
- Preservation: BackupService.cs was not edited or reverted; unrelated pre-existing worktree changes were preserved.

## Implemented changes
'@+[Environment]::NewLine+(($changes|ForEach-Object{"- $_"}) -join [Environment]::NewLine)+@'

## UI/UX details
- Main window retains layout rounding and device-pixel snapping; titlebar supports drag and double-click maximize/restore.
- Navigation labels use DynamicResource keys backed by Arabic and English resource dictionaries.
- Arabic font fallback stack: Segoe UI Variable Text, Cairo, Segoe UI, Tahoma.
- Added semantic input-border, focus-ring, destructive, and card-border brushes, plus reusable 38px enterprise input/button/card styles without removing existing styles.
- Accounts list now presents a centered empty state with a direct “+ إضافة حساب جديد” action when no accounts match the current filter.
- Existing account form includes name, book/category, opening balance, phone, debit/credit direction, account nature, credit limit, address, secondary phone, and WhatsApp opt-in. Existing keyboard navigation and Escape/Ctrl+S support remain.
- Voucher WhatsApp fallback uses the requested formal Arabic business wording. Existing WhatsAppService normalizes phone numbers with Yemen default country code 967, validates length, URL-encodes the message, and opens a draft via shell execution. Configured message templates remain supported.
- Account statement already has LTR reference numbers, aligned debit/credit values, cumulative balance, and a date converter that formats Arabic weekday/date/time.

## Verification
- Build and test results pending.
'@
Write-Utf8 'PHASE1_IMPLEMENTATION_REPORT.md' $report
Write-Output 'CONTINUATION_OK'
$changes|ForEach-Object{Write-Output $_}
