$ErrorActionPreference="Stop"
Add-Type -AssemblyName System.Drawing
$bmp=New-Object System.Drawing.Bitmap 1920,1080
$g=[System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen(0,0,0,0,$bmp.Size)
$path="C:\Users\pc\Desktop\SmartLedger_Run_2026-10-05\docs\runtime-final-capture.png"
$bmp.Save($path,[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose();Write-Output $path