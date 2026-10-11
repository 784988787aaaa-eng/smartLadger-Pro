$ErrorActionPreference = 'Stop'
$root = 'C:\Users\pc\SmartLedger-Build-Clean-20261010'
$changes = [System.Collections.Generic.List[string]]::new()
function Replace-One([string]$path, [string]$old, [string]$new, [string]$label) {
  $full = Join-Path $root $path
  $text = [System.IO.File]::ReadAllText($full)
  $count = ([regex]::Matches($text, [regex]::Escape($old))).Count
  if ($count -ne 1) { throw "Expected exactly one match for $label, found $count in $path" }
  $text = $text.Replace($old, $new)
  [System.IO.File]::WriteAllText($full, $text, [System.Text.UTF8Encoding]::new($false))
  $changes.Add("$path :: $label")
}
function Append-BeforeRootClose([string]$path, [string]$addition, [string]$label) {
  $full = Join-Path $root $path
  $text = [System.IO.File]::ReadAllText($full)
  $idx = $text.LastIndexOf('</ResourceDictionary>')
  if ($idx -lt 0) { throw "ResourceDictionary closing tag not found in $path" }
  $text = $text.Insert($idx, $addition + [Environment]::NewLine)
  [System.IO.File]::WriteAllText($full, $text, [System.Text.UTF8Encoding]::new($false))
  $changes.Add("$path :: $label")
}
$mw = 'src\SmartLedger.App\Views\MainWindow.xaml'
$labels = @{
'Text="الرئيسية"' = 'Text="{DynamicResource Nav.Home}"'
'Text="الدفاتر والحسابات"' = 'Text="{DynamicResource Nav.Accounts}"'
'Text="المخزون"' = 'Text="{DynamicResource Nav.Inventory}"'
'Text="المبيعات"' = 'Text="{DynamicResource Nav.Sales}"'
'Text="المشتريات"' = 'Text="{DynamicResource Nav.Purchases}"'
'Text="المصروفات"' = 'Text="{DynamicResource Nav.Expenses}"'
'Text="التقارير"' = 'Text="{DynamicResource Nav.Reports}"'
'Text="المستخدمون والصلاحيات"' = 'Text="{DynamicResource Nav.Users}"'
'Text="سجل التدقيق"' = 'Text="{DynamicResource Nav.Audit}"'
'Text="سلة المحذوفات"' = 'Text="{DynamicResource Nav.RecycleBin}"'
'Text="الإعدادات"' = 'Text="{DynamicResource Nav.Settings}"'
'Text="عن التطبيق والهوية / About"' = 'Text="{DynamicResource Nav.About}"'
}
foreach ($pair in $labels.GetEnumerator()) { Replace-One $mw $pair.Key $pair.Value "localize $($pair.Key)" }
# Add root pixel snapping only if not already present.
$full = Join-Path $root $mw
$text = [System.IO.File]::ReadAllText($full)
if ($text -notmatch 'UseLayoutRounding="True"') {
  $text = $text.Replace('WindowStyle="None" ResizeMode="CanResize"', 'WindowStyle="None" ResizeMode="CanResize" UseLayoutRounding="True" SnapsToDevicePixels="True"')
  [System.IO.File]::WriteAllText($full,$text,[System.Text.UTF8Encoding]::new($false))
  $changes.Add("$mw :: DPI layout rounding")
}
# Insert account empty state without replacing existing account UI.
$path = Join-Path $root 'src\SmartLedger.App\Views\ModuleTemplates.xaml'
$text = [System.IO.File]::ReadAllText($path)
$anchor = @'
        </DataGrid>
        <Border Grid.Row="1" Background="#0B132B" BorderBrush="#1E293B" BorderThickness="1,1,1,0" CornerRadius="0,0,8,8" Padding="10,0">
'@
$overlay = @'
        </DataGrid>
        <Grid Grid.Row="0" Panel.ZIndex="5" Background="#F20F172A" Visibility="Collapsed">
          <Grid.Style>
            <Style TargetType="Grid">
              <Setter Property="Visibility" Value="Collapsed"/>
              <Style.Triggers><DataTrigger Binding="{Binding People.Count}" Value="0"><Setter Property="Visibility" Value="Visible"/></DataTrigger></Style.Triggers>
            </Style>
          </Grid.Style>
          <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center" MaxWidth="420" Margin="24">
            <TextBlock Text="◇" FontSize="42" Foreground="{StaticResource BrushMuted}" HorizontalAlignment="Center" TextAlignment="Center"/>
            <TextBlock Text="لا توجد حسابات في هذا العرض" FontSize="18" FontWeight="SemiBold" HorizontalAlignment="Center" TextAlignment="Center" Margin="0,8,0,4"/>
            <TextBlock Text="أضف حسابًا جديدًا أو غيّر مرشح التصنيف لعرض الحسابات المتاحة." Style="{StaticResource CaptionText}" TextWrapping="Wrap" HorizontalAlignment="Center" TextAlignment="Center" Margin="0,0,0,16"/>
            <Button Content="+ إضافة حساب جديد" Command="{Binding OpenAddCommand}" Style="{StaticResource PrimaryButton}" Width="190" Height="40" HorizontalAlignment="Center"/>
          </StackPanel>
        </Grid>
        <Border Grid.Row="1" Background="#0B132B" BorderBrush="#1E293B" BorderThickness="1,1,1,0" CornerRadius="0,0,8,8" Padding="10,0">
'@
$count = ([regex]::Matches($text,[regex]::Escape($anchor))).Count
if ($count -ne 1) { throw "Accounts grid empty-state insertion anchor count: $count" }
$text = $text.Replace($anchor,$overlay)
[System.IO.File]::WriteAllText($path,$text,[System.Text.UTF8Encoding]::new($false))
$changes.Add('src\SmartLedger.App\Views\ModuleTemplates.xaml :: accounts empty state overlay')
Replace-One 'src\SmartLedger.App\Themes\Typography.xaml' '<FontFamily x:Key="ArabicFont">Segoe UI</FontFamily>' '<FontFamily x:Key="ArabicFont">Segoe UI Variable Text, Cairo, Segoe UI, Tahoma</FontFamily>' 'commercial Arabic font stack'
Append-BeforeRootClose 'src\SmartLedger.App\Themes\Colors.xaml' @'
    <SolidColorBrush x:Key="BrushInputBorder" Color="#344761"/>
    <SolidColorBrush x:Key="BrushFocusRing" Color="#00B4D8"/>
    <SolidColorBrush x:Key="BrushDestructive" Color="#DC3545"/>
    <SolidColorBrush x:Key="BrushCardBorder" Color="#263A5A"/>
'@ 'semantic enterprise color tokens'
Append-BeforeRootClose 'src\SmartLedger.App\Themes\Styles.xaml' @'
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
'@ 'enterprise input/button/card styles'
Append-BeforeRootClose 'src\SmartLedger.App\Resources\Strings.ar.xaml' @'
  <sys:String x:Key="Nav.Reports">التقارير</sys:String>
  <sys:String x:Key="Nav.Users">المستخدمون والصلاحيات</sys:String>
  <sys:String x:Key="Nav.RecycleBin">سلة المحذوفات</sys:String>
  <sys:String x:Key="Nav.About">عن التطبيق والهوية / About</sys:String>
'@ 'Arabic navigation resource keys'
Append-BeforeRootClose 'src\SmartLedger.App\Resources\Strings.en.xaml' @'
  <sys:String x:Key="Nav.Reports">Reports</sys:String>
  <sys:String x:Key="Nav.Users">Users &amp; Permissions</sys:String>
  <sys:String x:Key="Nav.RecycleBin">Recycle Bin</sys:String>
  <sys:String x:Key="Nav.About">About &amp; Identity</sys:String>
'@ 'English navigation resource keys'
Replace-One 'src\SmartLedger.App\Views\MainWindow.xaml.cs' @'
    private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed && !_fullScreen)
            DragMove();
    }
'@ @'
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
'@ 'titlebar drag and double-click maximize/restore'
Replace-One 'src\SmartLedger.App\ViewModels\AccountsViewModel.cs' 'const string fallback = "مرحباً {CustomerName}،\\nسند رقم: #{InvoiceNumber} من {StoreName}.\\nالمبلغ: {TotalAmount} {Currency}.\\nالبيان: {Description}.\\nالرصيد المتبقي: {RemainingAmount} {Currency}.\\nالتاريخ: {Date}."; ' 'const string fallback = "السلام عليكم ورحمة الله وبركاته، عزيزنا {CustomerName}، نود إحاطتكم بتفاصيل المعاملة رقم {InvoiceNumber} بتاريخ {Date}: المبلغ: {TotalAmount} {Currency} - البيان: {Description} - الرصيد الحالي: {RemainingAmount} {Currency}. شكراً لتعاملكم معنا."; ' 'formal Arabic voucher fallback'
$report = @'
# SmartLedger Phase 1 UI/UX Implementation Report

## Execution scope
- Workspace: C:\Users\pc\SmartLedger-Build-Clean-20261010
- Branch: main
- Preservation: BackupService.cs was not edited or reverted; unrelated pre-existing worktree changes were preserved.

## Implemented changes
'@ + [Environment]::NewLine + (($changes | ForEach-Object { "- $_" }) -join [Environment]::NewLine) + @'

## UI/UX details
- Main window retains layout rounding and device-pixel snapping; titlebar supports drag and double-click maximize/restore.
- Main navigation now uses DynamicResource keys backed by Arabic and English resource dictionaries.
- Arabic font fallback stack: Segoe UI Variable Text, Cairo, Segoe UI, Tahoma.
- Added semantic input-border, focus-ring, destructive, and card-border brushes, plus reusable 38px enterprise input/button/card styles without removing existing styles.
- Accounts list now presents a centered empty state with a direct “+ إضافة حساب جديد” action when no accounts match the current filter.
- Existing account form includes name, category/book, opening balance, phone, debit/credit direction, account nature, credit limit, address, secondary phone, and WhatsApp opt-in. Existing keyboard navigation and Escape/Ctrl+S support remain.
- Voucher WhatsApp fallback now uses the requested formal Arabic business wording. Existing WhatsAppService normalizes phone numbers with Yemen default country code 967, validates length, URL-encodes the message, and opens a draft via shell execution. Configured message templates remain supported.
- Account statement already has LTR reference numbers, aligned debit/credit values, cumulative balance, and a date converter that formats Arabic weekday/date/time.

## Verification
'@
$reportPath = Join-Path $root 'PHASE1_IMPLEMENTATION_REPORT.md'
[System.IO.File]::WriteAllText($reportPath,$report,[System.Text.UTF8Encoding]::new($false))
Write-Output 'MODIFICATION_SCRIPT_OK'
$changes | ForEach-Object { Write-Output $_ }
Write-Output "REPORT=$reportPath"
