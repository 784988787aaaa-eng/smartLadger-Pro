$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$root='C:\Users\pc\SmartLedger-Build-Clean-20261010'
$dir=Join-Path $root 'runtime_verified_walkthrough'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
Start-Sleep -Seconds 3
$bounds=[System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp=New-Object System.Drawing.Bitmap($bounds.Width,$bounds.Height)
$g=[System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($bounds.Location,[System.Drawing.Point]::Empty,$bounds.Size)
$shot=Join-Path $dir '00-current-runtime-screen.png'
$bmp.Save($shot,[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose()
$rootElement=[System.Windows.Automation.AutomationElement]::RootElement
$all=$rootElement.FindAll([System.Windows.Automation.TreeScope]::Children,[System.Windows.Automation.Condition]::TrueCondition)
$lines=New-Object System.Collections.Generic.List[string]
foreach($w in $all){
  $lines.Add("WINDOW: Name=$($w.Current.Name); Class=$($w.Current.ClassName); Id=$($w.Current.AutomationId); Bounds=$($w.Current.BoundingRectangle)")
  $nodes=$w.FindAll([System.Windows.Automation.TreeScope]::Descendants,[System.Windows.Automation.Condition]::TrueCondition)
  foreach($n in $nodes){$name=$n.Current.Name; $aid=$n.Current.AutomationId; $type=$n.Current.ControlType.ProgrammaticName; if($name -or $aid){$lines.Add("  $type | $name | $aid")}}
}
$lines | Set-Content -Encoding UTF8 (Join-Path $dir '00-current-runtime-uia.txt')
Write-Output "SCREEN=$shot"
Write-Output "WINDOWS=$($all.Count)"
Write-Output "UIA=$((Join-Path $dir '00-current-runtime-uia.txt'))"
