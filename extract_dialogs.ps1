$ErrorActionPreference='Stop'
$root='C:\Users\pc\SmartLedger-Build-Clean-20261010'
Set-Location $root
$backup='C:\Users\pc\SmartLedger-Build-Clean-20261010\src\SmartLedger.Infrastructure\Backup\BackupService.cs'
$backupHash=(Get-FileHash $backup -Algorithm SHA256).Hash
$targets=@(@{File='AccountsView.xaml';Binding='IsDialogOpen';Name='AccountDialogControl';Key='AccountDialogControlTemplate'},@{File='InventoryView.xaml';Binding='IsDialogOpen';Name='ProductDialogControl';Key='ProductDialogControlTemplate'},@{File='ExpensesView.xaml';Binding='IsDialogOpen';Name='ExpenseVoucherDialogControl';Key='ExpenseVoucherDialogTemplate'})
$moduleDir='C:\Users\pc\SmartLedger-Build-Clean-20261010\src\SmartLedger.App\Views\Modules'
foreach($t in $targets){
 $path=Join-Path $moduleDir $t.File; $text=[IO.File]::ReadAllText($path)
 $pattern='(?s)<Grid\b(?=[^>]*Style="\{StaticResource DialogOverlay\}")(?=[^>]*Visibility="\{Binding '+[regex]::Escape($t.Binding)+',Converter=\{StaticResource BoolToVisibility\}\}")[^>]*>'
 $m=[regex]::Match($text,$pattern); if(!$m.Success){throw "Could not locate dialog overlay in $($t.File)"}
 $tokens=[regex]::Matches($text.Substring($m.Index),'(?s)<Grid(?=\s|>)[^>]*>|</Grid>'); $depth=0; $end=-1
 foreach($tok in $tokens){ if($tok.Value -match '^</Grid>'){$depth--} elseif($tok.Value -notmatch '/>$'){$depth++}; if($depth -eq 0){$end=$m.Index+$tok.Index+$tok.Length;break} }
 if($end -lt 0){throw "Could not balance dialog Grid in $($t.File)"}; $overlay=$text.Substring($m.Index,$end-$m.Index)
 $component='<?xml version="1.0" encoding="utf-8"?>' + "`r`n" + '<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:vm="clr-namespace:SmartLedger.App.ViewModels" xmlns:behaviors="clr-namespace:SmartLedger.App.Behaviors" xmlns:local="clr-namespace:SmartLedger.App.Converters" xmlns:core="clr-namespace:SmartLedger.Core.Models;assembly=SmartLedger.Core">' + "`r`n  <DataTemplate x:Key=" + '"'+$t.Key+'">' + "`r`n" + $overlay + "`r`n  </DataTemplate>`r`n</ResourceDictionary>"
 [IO.File]::WriteAllText((Join-Path $moduleDir ($t.Name+'.xaml')),$component,[Text.UTF8Encoding]::new($false))
 $replacement='<ContentControl Style="{StaticResource DialogOverlay}" Content="{Binding}" ContentTemplate="{StaticResource '+$t.Key+'}" />'
 [IO.File]::WriteAllText($path,($text.Substring(0,$m.Index)+$replacement+$text.Substring($end)),[Text.UTF8Encoding]::new($false)); Write-Output "EXTRACTED $($t.Name) chars=$($overlay.Length)"
}
$appPath='C:\Users\pc\SmartLedger-Build-Clean-20261010\src\SmartLedger.App\App.xaml';$app=[IO.File]::ReadAllText($appPath)
foreach($t in $targets){$source='Views/Modules/'+$t.Name+'.xaml';if($app -notmatch [regex]::Escape($source)){$needle='<ResourceDictionary Source="Views/Modules/ExpensesView.xaml" />';if($app.Contains($needle)){$app=$app.Replace($needle,$needle+"`r`n                    <ResourceDictionary Source=""$source"" />")}else{throw "Missing App.xaml insertion anchor: $source"}}}
[IO.File]::WriteAllText($appPath,$app,[Text.UTF8Encoding]::new($false));$after=(Get-FileHash $backup -Algorithm SHA256).Hash;if($backupHash -ne $after){throw 'BackupService hash mismatch'};Write-Output "BACKUP_SHA256=$after"