$ErrorActionPreference='Continue'
$root='C:\Users\pc\SmartLedger-Build-Clean-20261010'
Set-Location $root
'--- APP STARTUP ---'
Get-Content 'src\SmartLedger.App\App.xaml.cs'
'--- LOGIN REFERENCES ---'
Get-ChildItem src,tests -Recurse -Include *.cs,*.md,*.json,*.config | Select-String -Pattern 'admin.*password|password.*admin|DefaultAdmin|SeedAdmin|RememberMe|LoginSubmit|LoginWindow|admin@|PasswordHash|CreateUser' -CaseSensitive:$false | Select-Object -First 100 | ForEach-Object { "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" }
'--- ENGLISH HEADERS ---'
Get-ChildItem 'src\SmartLedger.App' -Recurse -Include *.xaml,*.resx | Select-String -Pattern 'Product Name|Available Stock|Cost Price|Retail Price|Wholesale Price' | ForEach-Object { "$($_.Path):$($_.LineNumber): $($_.Line.Trim())" }
'--- NAV IDS ---'
Select-String 'src\SmartLedger.App\Views\MainWindow.xaml' -Pattern 'AutomationProperties.AutomationId="NavBtn_' | ForEach-Object { $_.Line.Trim() }
'--- PROCESSES ---'
Get-Process SmartLedger.App,notepad -ErrorAction SilentlyContinue | Select-Object ProcessName,Id,MainWindowTitle
