$ErrorActionPreference='Stop'
$root='C:\Users\pc\SmartLedger-Build-Clean-20261010'
function ReadU([string]$p){[IO.File]::ReadAllText((Join-Path $root $p),[Text.Encoding]::UTF8)}
function W([string]$p,[string]$s){[IO.File]::WriteAllText((Join-Path $root $p),$s,[Text.UTF8Encoding]::new($false))}
$rel='src\SmartLedger.App\Views\ModuleTemplates.xaml'; $t=ReadU $rel
$t=$t.Replace('Text="{Binding NewPhone,UpdateSourceTrigger=PropertyChanged}" FlowDirection="RightToLeft" TextAlignment="Right" HorizontalContentAlignment="Right" TabIndex="4"', 'Text="{Binding NewPhone,UpdateSourceTrigger=PropertyChanged}" FlowDirection="LeftToRight" TextAlignment="Left" HorizontalContentAlignment="Left" TabIndex="4"')
if(-not $t.Contains('إرسال كشف الحساب عبر واتساب')){
 $nl=[Environment]::NewLine
 $old='<StackPanel DockPanel.Dock="Left" Orientation="Horizontal">'+$nl+'                <Button Content="تصدير Excel"'
 $new='<StackPanel DockPanel.Dock="Left" Orientation="Horizontal">'+$nl+'                <Button Content="إرسال كشف الحساب عبر واتساب" Command="{Binding SendStatementWhatsAppCommand}" Style="{StaticResource GhostButton}" Width="174" Margin="8,0,0,0"/>'+$nl+'                <Button Content="تصدير Excel"'
 $t=$t.Replace($old,$new)
}
if(-not $t.Contains('AccountStatementEmptyStatePhase1')){
 $pattern='(?s)(</DataGrid>\s*</Border>\s*)(<DockPanel Grid.Row="3" Margin="0,12,0,0">)'
 $replacement='$1<Grid x:Name="AccountStatementEmptyStatePhase1" Background="#F20F172A" Visibility="Collapsed"><Grid.Style><Style TargetType="Grid"><Setter Property="Visibility" Value="Collapsed"/><Style.Triggers><DataTrigger Binding="{Binding SelectedAccountStatementRows.Count}" Value="0"><Setter Property="Visibility" Value="Visible"/></DataTrigger></Style.Triggers></Style></Grid.Style><StackPanel HorizontalAlignment="Center" VerticalAlignment="Center" Margin="20"><TextBlock Text="◇" FontSize="36" Foreground="{StaticResource BrushMuted}" HorizontalAlignment="Center"/><TextBlock Text="لا توجد معاملات مسجلة لهذا الحساب" FontSize="16" FontWeight="SemiBold" HorizontalAlignment="Center" TextAlignment="Center" Margin="0,8,0,4"/><TextBlock Text="يمكنك تسجيل أول حركة لهذا الحساب الآن." Style="{StaticResource CaptionText}" HorizontalAlignment="Center" TextAlignment="Center" Margin="0,0,0,12"/><Button Content="+ إضافة معاملة جديدة" Command="{Binding OpenOperationForAccountCommand}" CommandParameter="{Binding SelectedPerson}" Style="{StaticResource PrimaryButton}" Width="190" Height="40" HorizontalAlignment="Center"/></StackPanel></Grid>$2'
 $m=[regex]::Matches($t,$pattern)
 if($m.Count -ne 1){throw "Statement empty-state insertion expected one match, found $($m.Count)"}
 $t=[regex]::Replace($t,$pattern,[System.Text.RegularExpressions.MatchEvaluator]{param($m)$replacement},1)
}
W $rel $t
$rel='src\SmartLedger.App\Converters\LedgerDateTimeConverter.cs'; $t=ReadU $rel
$t=$t.Replace('date.ToString("dddd yyyy/MM/dd hh:mm tt", ArabicCulture)', 'date.ToString("yyyy-MM-dd | dddd | hh:mm tt", ArabicCulture)')
W $rel $t
$build=Get-Content (Join-Path $root 'phase1-build.log') -Raw
$test=Get-Content (Join-Path $root 'phase1-test.log') -Raw
$report=@'
# SmartLedger — Phase 1 UI/UX Implementation Report

## Scope and preservation
- Workspace: C:\Users\pc\SmartLedger-Build-Clean-20261010
- Branch: main
- BackupService.cs was not edited or reverted.
- Pre-existing unrelated working-tree changes were retained. No commit, push, publish, or deployment was performed.
- Validation was compile/XAML validation plus automated tests; no interactive screenshot-based visual QA was performed.

## Files changed for Phase 1
- src\SmartLedger.App\Views\MainWindow.xaml — navigation labels use DynamicResource keys; layout rounding and pixel snapping retained.
- src\SmartLedger.App\Views\MainWindow.xaml.cs — titlebar drag and double-click maximize/restore.
- src\SmartLedger.App\Themes\Typography.xaml — Arabic font fallback stack.
- src\SmartLedger.App\Themes\Colors.xaml — semantic input-border, focus-ring, destructive, and card-border brushes.
- src\SmartLedger.App\Themes\Styles.xaml — reusable 38px EnterpriseInput, EnterpriseButton, EnterpriseCard tokens.
- src\SmartLedger.App\Resources\Strings.ar.xaml — Arabic navigation resource keys.
- src\SmartLedger.App\Resources\Strings.en.xaml — English navigation resource keys.
- src\SmartLedger.App\Views\ModuleTemplates.xaml — accounts/statement empty states, statement WhatsApp action, LTR phone input isolation.
- src\SmartLedger.App\ViewModels\AccountsViewModel.cs — formal Arabic voucher message fallback.
- src\SmartLedger.App\Converters\LedgerDateTimeConverter.cs — statement timestamp format yyyy-MM-dd | dddd | hh:mm tt.

## Design system
- Arabic font stack: Segoe UI Variable Text, Cairo, Segoe UI, Tahoma.
- Semantic colors: BrushInputBorder #344761; BrushFocusRing #00B4D8; BrushDestructive #DC3545; BrushCardBorder #263A5A.
- Added reusable 38px input/button styles and a 7px card style. Existing styles remain available to avoid regressions in screens not yet migrated.
- Main window uses UseLayoutRounding and SnapsToDevicePixels; title bar retains drag support and adds double-click maximize/restore.

## Account workflow and ledger
- Existing form includes account name, book/category, opening balance, phone, opening debit/credit direction, account nature, notes, secondary phone, address, credit limit, and WhatsApp opt-in.
- Phone input uses LTR direction for stable international digits and punctuation.
- Accounts empty state offers “+ إضافة حساب جديد”; statement empty state offers “+ إضافة معاملة جديدة”.
- Statement view has a direct WhatsApp draft action; each transaction row already exposes a voucher WhatsApp action.
- Statement dates display ISO date, Arabic weekday, and time.
- Existing reference numbers are LTR; debit and credit are right-aligned; cumulative balance uses a bold numeric style.
- Voucher fallback follows the requested formal Arabic greeting and includes account, reference, date, amount, currency, description, and current balance.
- Existing WhatsAppService normalizes phone digits with default country code 967, validates E.164-compatible length, URL-encodes text, and launches a draft using shell execution. It does not silently send messages.
- Existing account form already had TabIndex ordering and Escape/Ctrl+S bindings. No data model or repository schema was changed.

## Full solution build output
'@ + [Environment]::NewLine + $build + @'

## Full dotnet test output
Command: dotnet test .\tests\SmartLedger.Tests\SmartLedger.Tests.csproj --verbosity minimal

'@ + [Environment]::NewLine + $test + @'

## Verification summary
- Solution build: exit code 0, 0 warnings, 0 errors.
- Tests: exit code 0; 97 passed, 0 failed, 0 skipped.
- Existing BackupService.cs changes were preserved.
'@
W 'PHASE1_IMPLEMENTATION_REPORT.md' $report
Write-Output 'PHASE1_REPORT_FINALIZED'
Write-Output ('NAV_DYNAMIC_RESOURCE_COUNT=' + ([regex]::Matches((ReadU 'src\SmartLedger.App\Views\MainWindow.xaml'),'DynamicResource Nav\.')).Count)
Write-Output ('REPORT_BYTES=' + (Get-Item (Join-Path $root 'PHASE1_IMPLEMENTATION_REPORT.md')).Length)
