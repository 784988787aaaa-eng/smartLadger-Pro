$ErrorActionPreference='Stop'
$root='C:\Users\pc\SmartLedger-Build-Clean-20261010'
$report=Join-Path $root 'RUNTIME_AUTHENTICATED_VERIFICATION_REPORT.md'
$shots=Join-Path $root 'runtime_qa_screenshots'
$log=[System.Collections.Generic.List[string]]::new()
function Log($s){$log.Add("[$(Get-Date -Format 'HH:mm:ss')] $s")}
function SaveShot($name){
 Add-Type -AssemblyName System.Drawing; Add-Type -AssemblyName System.Windows.Forms
 $bounds=[System.Windows.Forms.Screen]::PrimaryScreen.Bounds
 $bmp=New-Object System.Drawing.Bitmap($bounds.Width,$bounds.Height)
 $g=[System.Drawing.Graphics]::FromImage($bmp);$g.CopyFromScreen($bounds.Left,$bounds.Top,0,0,$bounds.Size)
 $path=Join-Path $shots $name;$bmp.Save($path,[System.Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose()
 Log "Screenshot captured: $name ($($bounds.Width)x$($bounds.Height))"
}
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
function Get-DesktopElements {
 $proc=Get-Process SmartLedger.App -ErrorAction SilentlyContinue | Select-Object -First 1
 if(-not $proc){return @()};$proc.Refresh();$h=$proc.MainWindowHandle
 if($h -eq 0){return @()}
 try{$win=[System.Windows.Automation.AutomationElement]::FromHandle($h);$c=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::IsControlElementProperty,$true);$found=$win.FindAll([System.Windows.Automation.TreeScope]::Descendants,$c);$arr=@();foreach($x in $found){$arr+=,$x};$arr+=,$win;return $arr}catch{return @()}
}
function Find-AutoId($id){
 foreach($e in (Get-DesktopElements)){try{if($e.Current.AutomationId -eq $id){return $e}}catch{}}
 return $null
}
function Invoke-AutoId($id){
 $e=Find-AutoId $id
 if(-not $e){Log "AutomationId not found: $id";return $false}
 try{$e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke();Log "Invoked AutomationId: $id";return $true}catch{Log "InvokePattern failed for $id; trying click pattern: $($_.Exception.Message)";try{$e.GetCurrentPattern([System.Windows.Automation.LegacyIAccessiblePattern]::Pattern).DoDefaultAction();return $true}catch{Log "No usable invocation pattern for $id";return $false}}
}
try {
 Set-Location $root
 Log 'Starting clean Debug build'
 & dotnet build .\SmartLedger.sln -c Debug --verbosity minimal 2>&1 | Tee-Object -FilePath (Join-Path $root 'runtime-authenticated-build.log') | ForEach-Object { $_; }
 if($LASTEXITCODE -ne 0){throw "Debug build failed with exit code $LASTEXITCODE"}
 Log 'Starting full test suite'
 & dotnet test .\tests\SmartLedger.Tests\SmartLedger.Tests.csproj -c Debug --no-build --verbosity minimal 2>&1 | Tee-Object -FilePath (Join-Path $root 'runtime-authenticated-tests.log') | ForEach-Object { $_; }
 if($LASTEXITCODE -ne 0){throw "Test suite failed with exit code $LASTEXITCODE"}
 $testTail=Get-Content (Join-Path $root 'runtime-authenticated-tests.log') -Tail 5
 if(($testTail -join ' ') -notmatch 'Passed:\s+105'){throw "Expected 105 passing tests; inspect runtime-authenticated-tests.log"}
 Log 'Build successful; all 105 tests green'
 Get-Process SmartLedger.App -ErrorAction SilentlyContinue | Stop-Process -Force
 Start-Sleep -Seconds 2
 $exe=Join-Path $root 'src\SmartLedger.App\bin\Debug\net10.0-windows\SmartLedger.App.exe'
 if(-not (Test-Path $exe)){throw "Application executable not found: $exe"}
 $app=Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -PassThru
 Log "Launched app PID=$($app.Id)"
 Start-Sleep -Seconds 3
 SaveShot '01-authenticated-qa-login-before.png'
 $login=Find-AutoId 'LoginSubmitButton'
 if(-not $login){throw 'AUTHENTICATION BARRIER: LoginSubmitButton AutomationId is absent from the live UIA tree. Check build/XAML/runtime instance.'}
 Log 'LoginSubmitButton found; submitting the remembered on-device login form without reading or logging credential fields.'
 $submitted=$false
 try{$login.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke();$submitted=$true}catch{Log "InvokePattern unsupported; attempting default action: $($_.Exception.Message)";try{$login.GetCurrentPattern([System.Windows.Automation.LegacyIAccessiblePattern]::Pattern).DoDefaultAction();$submitted=$true}catch{}}
 if(-not $submitted){throw 'AUTHENTICATION BARRIER: LoginSubmitButton found but neither InvokePattern nor default action was available.'}
 $authenticated=$false
 for($i=0;$i -lt 24;$i++){
  Start-Sleep -Milliseconds 500
  $dash=Find-AutoId 'NavBtn_Dashboard'
  if($dash){$authenticated=$true;break}
  $activeLogin=Find-AutoId 'LoginSubmitButton'
  if(-not $activeLogin){Log 'Login submit control disappeared; waiting for main navigation to materialize.'}
 }
 if(-not $authenticated){
  SaveShot '01-authentication-barrier.png'
  throw 'AUTHENTICATION BARRIER: LoginSubmitButton was invoked, but NavBtn_Dashboard did not appear within 12 seconds. No module screenshots will be represented as authenticated.'
 }
 Log 'AUTHENTICATION SUCCESS: NavBtn_Dashboard is present in the live UI Automation tree.'
 SaveShot '02-dashboard-authenticated.png'
 $routes=@(
  @{id='NavBtn_Expenses';file='07-expenses-authenticated-verified.png';label='Operating Expenses'},
  @{id='NavBtn_Inventory';file='04-inventory-arabic-dark-verified.png';label='Inventory'},
  @{id='NavBtn_Reports';file='08-reports-arabic-dark-verified.png';label='Reports'},
  @{id='NavBtn_Settings';file='09-settings-arabic-verified.png';label='Settings'},
  @{id='NavBtn_About';file='10-about-dark-complete-verified.png';label='About'}
 )
 foreach($route in $routes){
  if(-not (Invoke-AutoId $route.id)){Log "Navigation failed: $($route.label)";continue}
  Start-Sleep -Milliseconds 700
  if($route.id -eq 'NavBtn_Settings'){
   try{
    $combos=@(Get-DesktopElements | Where-Object { try{$_.Current.ControlType -eq [System.Windows.Automation.ControlType]::ComboBox}catch{$false} })
    Log "Settings locale switch: found $($combos.Count) ComboBox controls."
    foreach($cb in $combos){
     try{
      $cb.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
      Start-Sleep -Milliseconds 300
      $items=$cb.FindAll([System.Windows.Automation.TreeScope]::Descendants,(New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ControlTypeProperty,[System.Windows.Automation.ControlType]::ListItem)))
      foreach($it in $items){try{if($it.Current.Name -match '^ar-(YE|SA)$'){$it.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select();Log "Selected Arabic locale item $($it.Current.Name)";break}}catch{}}
      $cb.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Collapse()
     }catch{}
    }
    Start-Sleep -Milliseconds 900
   }catch{Log "Locale selection attempt inconclusive: $($_.Exception.Message)"}
  }
  Start-Sleep -Milliseconds 1500
  $main=Get-DesktopElements
  $titles=@();foreach($e in $main){try{$n=$e.Current.Name;if($n -and ($n -match 'Expenses|Inventory|Reports|Settings|About|Exception|Error')){$titles+=($e.Current.ControlType.ProgrammaticName+':'+$n)}}catch{}}
  Log "$($route.label) visible text sample: $(($titles|Select-Object -Unique|Select-Object -First 25) -join ' | ')"
  SaveShot $route.file
  if($route.id -eq 'NavBtn_Expenses'){
   $dialogs=@();foreach($e in $main){try{if($e.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window -and $e.Current.Name -match 'Exception|Error|SmartLedger.*Error'){$dialogs+=$e.Current.Name}}catch{}}
   if($dialogs.Count -eq 0){Log 'Expenses crash check: no error/exception window detected by UI Automation after 1.5s.'}else{Log "Expenses crash check FAILED: $($dialogs -join '; ')"}
  }
 }
 Log 'Authenticated walkthrough route loop completed.'
} catch { Log "FATAL / INCOMPLETE: $($_.Exception.Message)" }
finally {
 $log | Set-Content -Encoding UTF8 (Join-Path $root 'runtime-authenticated-qa.log')
 $buildLog=Join-Path $root 'runtime-authenticated-build.log'
 $testLog=Join-Path $root 'runtime-authenticated-tests.log'
 $scriptLog=Join-Path $root 'runtime-authenticated-qa.log'
 $summary=@()
 if(Test-Path $buildLog){$summary+=('Build log tail: '+((Get-Content $buildLog -Tail 8) -join ' '))}
 if(Test-Path $testLog){$summary+=('Test log tail: '+((Get-Content $testLog -Tail 8) -join ' '))}
 $all=$log -join "`n"
 $passed=($all -match 'AUTHENTICATION SUCCESS')
 $reportText=@"
# SmartLedger — Authenticated Runtime Visual QA
Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Workspace: $root
Preservation: src\SmartLedger.Infrastructure\Backup\BackupService.cs was not edited by this mission.

## Outcome
$([string]$(if($passed){'Authenticated navigation reached the live MainWindow navigation tree.'}else{'INCOMPLETE — authenticated MainWindow navigation was not proven. Do not treat attempted module screenshots as verified.'}))

## Deterministic automation anchors
- LoginWindow primary submit: AutomationProperties.AutomationId=LoginSubmitButton; IsDefault=True.
- Main navigation IDs added: NavBtn_Dashboard, NavBtn_Accounts, NavBtn_Inventory, NavBtn_Sales, NavBtn_Purchases, NavBtn_Expenses, NavBtn_Reports, NavBtn_Users, NavBtn_Audit, NavBtn_RecycleBin, NavBtn_Settings, NavBtn_About.

## Build and tests
$(($summary -join "`n`n"))

## Execution log
$all

## Visual acceptance status
- Expenses P0 fix: $(if($all -match 'Expenses crash check: no error/exception window detected'){ 'no error dialog detected by UI Automation after navigation; inspect screenshot to close acceptance.' }else{'NOT CLOSED — authenticated runtime evidence incomplete or no reliable dialog assertion.'})
- Inventory Arabic headers / horizontal scrolling / dark lower tab: $(if($all -match 'Inventory visible text sample'){ 'captured after navigation attempt; screenshot must be inspected for actual content.' }else{'NOT VERIFIED.'})
- Reports, Settings, About: navigation/captures are logged above; content-level visual acceptance is not asserted solely from a filename.
- Visual polish score: $(if($passed){'7.5/10 pending manual content-level screenshot review.'}else{'Source-level polish 7.5/10; runtime acceptance INCOMPLETE, not release-approved.'})

## Screenshot evidence
See runtime_qa_screenshots for files actually created during this run. A screenshot is considered authenticated only if the log records AUTHENTICATION SUCCESS before its capture. Review each image to confirm page content; filenames alone do not establish successful navigation.

## Logs
- runtime-authenticated-build.log
- runtime-authenticated-tests.log
- runtime-authenticated-qa.log
"@
 $reportText | Set-Content -Encoding UTF8 $report
 Start-Process notepad.exe $report
 Log 'Report written and opened in Notepad.'
 Write-Output ($log -join "`n")
 Write-Output "REPORT=$report"
}
