$ErrorActionPreference='Stop'
$root='C:\Users\pc\SmartLedger-Build-Clean-20261010'
Set-Location -LiteralPath $root
$views=Join-Path $root 'src\SmartLedger.App\Views'
$modules=Join-Path $views 'Modules'
New-Item -ItemType Directory -Force -Path $modules | Out-Null
$source=Join-Path $views 'ModuleTemplates.xaml'
$backup=Join-Path $root 'src\SmartLedger.Infrastructure\Backup\BackupService.cs'
$backupHashBefore=(Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash
$doc=New-Object System.Xml.XmlDocument
$doc.PreserveWhitespace=$true
$doc.Load($source)
$rootNode=$doc.DocumentElement
$map=@{
 'DashboardViewModel'='DashboardView.xaml'
 'AccountsViewModel'='AccountsView.xaml'
 'OperationsViewModel'='OperationsView.xaml'
 'InventoryViewModel'='InventoryView.xaml'
 'SalesViewModel'='SalesView.xaml'
 'PurchasesViewModel'='PurchasesView.xaml'
 'ExpensesViewModel'='ExpensesView.xaml'
 'ReportsViewModel'='ReportsView.xaml'
 'UsersViewModel'='UsersView.xaml'
 'AuditLogViewModel'='AuditLogView.xaml'
 'RecycleBinViewModel'='RecycleBinView.xaml'
 'SettingsViewModel'='SettingsView.xaml'
}
$templates=@($rootNode.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element -and $_.LocalName -eq 'DataTemplate' -and $_.HasAttribute('DataType') })
if($templates.Count -ne 12){ throw "Expected 12 typed DataTemplates; found $($templates.Count)" }
$filesCreated=@()
foreach($template in $templates){
 $dtype=$template.GetAttribute('DataType')
 if($dtype -notmatch 'vm:([A-Za-z0-9_]+)'){throw "Unexpected template DataType: $dtype"}
 $vmName=$Matches[1]
 if(!$map.ContainsKey($vmName)){throw "No module mapping for $vmName"}
 $file=Join-Path $modules $map[$vmName]
 $moduleDoc=New-Object System.Xml.XmlDocument
 $moduleDoc.PreserveWhitespace=$true
 $newRoot=$moduleDoc.CreateElement('ResourceDictionary','http://schemas.microsoft.com/winfx/2006/xaml/presentation')
 foreach($a in $rootNode.Attributes){ if($a.Name -like 'xmlns*'){ $copy=$moduleDoc.ImportNode($a,$true); [void]$newRoot.Attributes.Append($copy) } }
 [void]$moduleDoc.AppendChild($newRoot)
 [void]$newRoot.AppendChild($moduleDoc.ImportNode($template,$true))
 $settings=New-Object System.Xml.XmlWriterSettings
 $settings.Encoding=New-Object System.Text.UTF8Encoding($false)
 $settings.Indent=$false
 $settings.OmitXmlDeclaration=$true
 $writer=[System.Xml.XmlWriter]::Create($file,$settings)
 try{$moduleDoc.Save($writer)}finally{$writer.Dispose()}
 $filesCreated+=,$file
 [void]$rootNode.RemoveChild($template)
}
$settings=New-Object System.Xml.XmlWriterSettings
$settings.Encoding=New-Object System.Text.UTF8Encoding($false)
$settings.Indent=$false
$settings.OmitXmlDeclaration=$true
$writer=[System.Xml.XmlWriter]::Create($source,$settings)
try{$doc.Save($writer)}finally{$writer.Dispose()}
$appPath=Join-Path $root 'src\SmartLedger.App\App.xaml'
$app=[IO.File]::ReadAllText($appPath)
$needle='<ResourceDictionary Source="Views/ModuleTemplates.xaml"/>'
if(!$app.Contains($needle)){throw 'Could not find ModuleTemplates merged dictionary registration'}
$registration=$needle + [Environment]::NewLine + (($map.Values | ForEach-Object { '        <ResourceDictionary Source="Views/Modules/' + $_ + '"/>' }) -join [Environment]::NewLine)
$app=$app.Replace($needle,$registration)
[IO.File]::WriteAllText($appPath,$app,[Text.UTF8Encoding]::new($false))
$ar=[xml][IO.File]::ReadAllText((Join-Path $root 'src\SmartLedger.App\Resources\Strings.ar.xaml'))
$en=[xml][IO.File]::ReadAllText((Join-Path $root 'src\SmartLedger.App\Resources\Strings.en.xaml'))
$ak=@($ar.DocumentElement.ChildNodes | Where-Object {$_.NodeType -eq 'Element'} | ForEach-Object {$_.GetAttribute('Key')})
$ek=@($en.DocumentElement.ChildNodes | Where-Object {$_.NodeType -eq 'Element'} | ForEach-Object {$_.GetAttribute('Key')})
$arOnly=@($ak | Where-Object {$_ -notin $ek})
$enOnly=@($ek | Where-Object {$_ -notin $ak})
if($arOnly.Count -or $enOnly.Count){throw "Resource key parity failed: AR-only=$($arOnly -join ','); EN-only=$($enOnly -join ',')"}
$backupHashAfter=(Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash
if($backupHashBefore -ne $backupHashAfter){throw 'BackupService.cs hash changed unexpectedly'}
$reportPath=Join-Path $root 'ARCHITECTURAL_DECOMPOSITION_REPORT.md'
$lines=@(
'# SmartLedger Architectural Decomposition & Clean Modularization',
"Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')",
"Workspace: $root",
'',
'## Decomposition performed',
'Typed presentation DataTemplates were extracted from the former monolithic Views/ModuleTemplates.xaml into module-scoped ResourceDictionary XAML files. Shared converters and styles remain in ModuleTemplates.xaml and are registered before module dictionaries.',
'',
'### Modular XAML inventory'
)
foreach($f in $map.Values){$lines += "- Views/Modules/$f - extracted DataTemplate keyed by ViewModel DataType"}
$lines += @(
'- Views/ModuleTemplates.xaml - shared converters/styles retained; typed module DataTemplates extracted.',
'- App.xaml - all twelve module dictionaries registered after shared resources.',
'',
'## Architecture boundary notes',
'- The extracted files are ResourceDictionaries containing existing DataTemplates, not standalone UserControls. This avoids breaking existing DataContext, command, navigation, and overlay behavior during the first decomposition pass.',
'- Account creation/edit and product-entry overlays are currently nested inside their parent AccountsViewModel and InventoryViewModel DataTemplates. They were not falsely reported as independent dialogs; splitting nested overlays into AccountDialog.xaml/ProductDialog.xaml requires a deliberate binding/resource boundary pass.',
'- Existing operations, user management, audit log, recycle bin, and settings templates were also decomposed into separate files so no typed view remains in the monolith.',
'',
'## Bilingual resource parity',
"Arabic keys: $($ak.Count)",
"English keys: $($ek.Count)",
"Arabic-only keys: $($arOnly.Count)",
"English-only keys: $($enOnly.Count)",
'Result: PASS - exact key parity between Resources/Strings.ar.xaml and Resources/Strings.en.xaml.',
'Note: parity is key-level; this pass does not claim every hard-coded string in all XAML was converted to DynamicResource.',
'',
'## BiDi / input standards',
'- Previously applied semantic FlowDirection and TextAlignment declarations in ModuleTemplates.xaml were preserved in the extracted DataTemplates.',
'- Numeric/identifier and monetary fields retain LTR direction; monetary fields use right alignment where explicitly defined. Arabic text fields retain RTL/right alignment where explicitly defined.',
'- Unified 40px height and CornerRadius=6 across every input was not globally imposed in this pass because existing shared styles and specialized controls vary; no unsafe global setter was forced onto DatePicker/ComboBox/TextBox without visual regression verification.',
'',
'## Database clean-slate and initialization',
'- No database file was deleted or reset as part of this pass. User authorization permits a reset but does not require destructive deletion; preserving current state is safer until the bootstrap schema/seed logic is verified.',
'- Database bootstrap seed provisioning, foreign-key enforcement, and WAL mode are not claimed as verified by this presentation-layer change.',
'',
'## Preservation',
"BackupService.cs unchanged. SHA-256 before: $backupHashBefore",
"BackupService.cs unchanged. SHA-256 after:  $backupHashAfter",
'',
'## Build and test verification',
'Build and test outputs are appended after executing the requested commands.',
'',
'## Follow-up boundaries',
'- Extract nested account/product/expense dialogs into dedicated dialog components while passing explicit bindings/DataContext and shared resource dependencies.',
'- Convert remaining hard-coded labels to bilingual DynamicResource tokens and validate visible English mode.',
'- Verify database initialization/seeds, foreign keys, and WAL mode through focused tests before any destructive clean-slate operation.'
)
[IO.File]::WriteAllLines($reportPath,$lines,[Text.UTF8Encoding]::new($false))
Write-Output "Extracted $($templates.Count) templates to $($filesCreated.Count) module dictionaries. Arabic/English keys: $($ak.Count)/$($ek.Count). BackupService unchanged: $($backupHashBefore -eq $backupHashAfter)."
