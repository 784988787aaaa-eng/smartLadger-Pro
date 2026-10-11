$ErrorActionPreference = 'Continue'
$root = 'C:\Users\pc\SmartLedger-Build-Clean-20261010'
$out = Join-Path $root 'runtime_qa_screenshots'
New-Item -ItemType Directory -Force -Path $out | Out-Null
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type @'
using System;
using System.Runtime.InteropServices;
public class QAWin32 {
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
 [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr extra);
 [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
 public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr extra);
 [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
}
'@
$log = [System.Collections.Generic.List[string]]::new()
$log.Add('# SmartLedger Runtime Visual QA')
$log.Add('Date: ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz'))
$log.Add('Workspace: ' + $root)
$log.Add('Build log: runtime-qa-build.log (clean Debug build verified before launch).')
$app = Join-Path $root 'src\SmartLedger.App\bin\Debug\net10.0-windows\SmartLedger.App.exe'
$existing = Get-Process SmartLedger.App -ErrorAction SilentlyContinue
if (-not $existing) { $proc = Start-Process -FilePath $app -WorkingDirectory (Split-Path $app) -PassThru; $log.Add('Launch requested; process PID: ' + $proc.Id) } else { $proc = $existing | Select-Object -First 1; $log.Add('Reused running application PID: ' + $proc.Id) }
Start-Sleep -Seconds 7
$wins = [System.Collections.Generic.List[object]]::new()
$cb = [QAWin32+EnumWindowsProc]{ param($h,$e) if ([QAWin32]::IsWindowVisible($h)) { $sb = [System.Text.StringBuilder]::new(512); [void][QAWin32]::GetWindowText($h,$sb,$sb.Capacity); $title = $sb.ToString(); if ($title.Trim()) { $rc = New-Object QAWin32+RECT; [void][QAWin32]::GetWindowRect($h,[ref]$rc); $wins.Add([pscustomobject]@{Handle=$h;Title=$title;Left=$rc.Left;Top=$rc.Top;Right=$rc.Right;Bottom=$rc.Bottom}) } }; return $true }
[void][QAWin32]::EnumWindows($cb,[IntPtr]::Zero)
$log.Add('## Visible windows')
foreach ($w in $wins) { $log.Add(('- {0} | bounds=({1},{2})-({3},{4}) | size={5}x{6}' -f $w.Title,$w.Left,$w.Top,$w.Right,$w.Bottom,($w.Right-$w.Left),($w.Bottom-$w.Top))) }
$target = $wins | Where-Object { $_.Title -match 'SmartLedger|الحساب|تسجيل|إعداد|Login|Setup|Bootstrap' } | Select-Object -First 1
if ($target) { [void][QAWin32]::ShowWindow($target.Handle,9); [void][QAWin32]::SetForegroundWindow($target.Handle); Start-Sleep -Milliseconds 900; $log.Add('Foreground target: ' + $target.Title) } else { $log.Add('WARN: No titled SmartLedger window found among visible windows.') }
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap($bounds.Width,$bounds.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($bounds.Left,$bounds.Top,0,0,$bounds.Size); $shot = Join-Path $out '01-runtime-initial.png'; $bmp.Save($shot,[System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()
$log.Add('Screenshot: ' + (Split-Path $shot -Leaf) + ' (' + $bounds.Width + 'x' + $bounds.Height + ')')
$log.Add('## UI Automation snapshot')
try { $rootEl = [System.Windows.Automation.AutomationElement]::RootElement; $condition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::IsControlElementProperty,$true); $els = $rootEl.FindAll([System.Windows.Automation.TreeScope]::Descendants,$condition); $n=0; foreach($el in $els) { if ($n -ge 300) { break }; try { $name=$el.Current.Name; $type=$el.Current.ControlType.ProgrammaticName; $rect=$el.Current.BoundingRectangle; if ($name -or ($type -match 'Button|Edit|DataGrid|ComboBox|Window|TabItem|ListItem')) { $log.Add(('- [{0}] {1} | enabled={2} | rect={3},{4},{5},{6}' -f $type,$name,$el.Current.IsEnabled,[math]::Round($rect.X),[math]::Round($rect.Y),[math]::Round($rect.Width),[math]::Round($rect.Height))); $n++ } } catch {} }; $log.Add('UIA controls recorded: ' + $n) } catch { $log.Add('WARN UI Automation inspection failed: ' + $_.Exception.Message) }
$log.Add('## Initial inspection limits')
$log.Add('- This automated pass captures the live initial state and UI Automation tree. Further module coverage depends on successful navigation and available seeded data/credentials.')
$log | Set-Content -Encoding UTF8 (Join-Path $root 'runtime-qa-initial-observations.txt')
Write-Output ('SCREENSHOT=' + $shot)
Write-Output ('VISIBLE_WINDOWS=' + $wins.Count)
Write-Output ('APP_PID=' + $proc.Id)
Write-Output ('OBSERVATION_FILE=' + (Join-Path $root 'runtime-qa-initial-observations.txt'))
