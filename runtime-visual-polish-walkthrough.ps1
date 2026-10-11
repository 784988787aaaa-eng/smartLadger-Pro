$ErrorActionPreference='Continue'
$root='C:\Users\pc\SmartLedger-Build-Clean-20261010'; $out=Join-Path $root 'runtime_qa_screenshots'
Add-Type -AssemblyName System.Drawing; Add-Type -AssemblyName System.Windows.Forms; Add-Type -AssemblyName UIAutomationClient; Add-Type -AssemblyName UIAutomationTypes
function Shot($n){Start-Sleep -Seconds 2;$b=[System.Windows.Forms.Screen]::PrimaryScreen.Bounds;$bmp=New-Object System.Drawing.Bitmap($b.Width,$b.Height);$g=[System.Drawing.Graphics]::FromImage($bmp);$g.CopyFromScreen($b.Left,$b.Top,0,0,$b.Size);$bmp.Save((Join-Path $out $n),[System.Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose();$log.Add("Captured $n at $($b.Width)x$($b.Height)")}
function AllControls { $cond=New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::IsControlElementProperty,$true); return [System.Windows.Automation.AutomationElement]::RootElement.FindAll([System.Windows.Automation.TreeScope]::Descendants,$cond) }
function Navigate($label,$file,$needles){
 $els=AllControls; $found=$false
 foreach($e in $els){try{if($e.Current.ControlType -eq [System.Windows.Automation.ControlType]::Text -and $needles -contains $e.Current.Name){$p=[System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($e); while($p -and $p.Current.ControlType -ne [System.Windows.Automation.ControlType]::Button){$p=[System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($p)}; if($p){try{$p.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke();$found=$true;break}catch{}}}}catch{}}
 $log.Add("$label navigation invoked=$found")
 Start-Sleep -Seconds 2
 $els=AllControls; $matches=@(); foreach($e in $els){try{if($e.Current.Name -and ($e.Current.Name -match $label -or $needles -contains $e.Current.Name)){$matches+=($e.Current.ControlType.ProgrammaticName+':'+$e.Current.Name)}}catch{}}
 $log.Add("Visible matching UIA controls: "+(($matches | Select-Object -First 12) -join ' | '))
 Shot $file
}
$log=[System.Collections.Generic.List[string]]::new();$log.Add('# Runtime visual polish walkthrough '+(Get-Date -Format s))
Get-Process SmartLedger.App -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
$app=Join-Path $root 'src\SmartLedger.App\bin\Debug\net10.0-windows\SmartLedger.App.exe'
$proc=Start-Process -FilePath $app -WorkingDirectory (Split-Path $app) -PassThru
$log.Add("Fresh app launch PID=$($proc.Id)")
Start-Sleep -Seconds 7
$els=AllControls
$log.Add("Initial UIA controls=$($els.Count)")
foreach($e in $els){try{if($e.Current.Name -match 'SmartLedger|Sign In|Login|Continue|Dashboard'){$log.Add("Initial control $($e.Current.ControlType.ProgrammaticName): $($e.Current.Name)")}}catch{}}
Shot '01-phase2-fresh-launch.png'
foreach($e in $els){try{if($e.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and $e.Current.IsEnabled -and $e.Current.Name -match 'Login|Sign in|Continue|Enter|Open'){ $e.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke();$log.Add("Invoked non-credential continue button: $($e.Current.Name)");break}}catch{}}
Start-Sleep -Seconds 5
Navigate 'Inventory' '04-inventory-fixed.png' @('Inventory')
Navigate 'Expenses' '07-expenses-verified.png' @('Operating Expenses')
Navigate 'Reports' '08-reports-live.png' @('Reports')
Navigate 'Settings' '09-settings-live.png' @('System Settings')
Navigate 'About' '10-about-live.png' @('About & Identity')
$log | Set-Content -Encoding UTF8 (Join-Path $root 'runtime-visual-polish-walkthrough.log')
Write-Output "WALKTHROUGH_LOG=$root\runtime-visual-polish-walkthrough.log"
Write-Output "SCREENSHOT_DIR=$out"
