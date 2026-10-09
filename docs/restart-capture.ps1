Get-Process SmartLedger.App -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
Start-Process 'C:\Users\pc\Desktop\SmartLedger_Run_2026-10-05\src\SmartLedger.App\bin\Debug\net10.0-windows\SmartLedger.App.exe'
Start-Sleep -Seconds 8
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
$bounds = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bitmap = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($bounds.Left, $bounds.Top, 0, 0, $bitmap.Size)
$bitmap.Save('C:\Users\pc\Desktop\SmartLedger_Run_2026-10-05\docs\qa-current.png', [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()
