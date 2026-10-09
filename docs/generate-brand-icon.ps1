Add-Type -AssemblyName PresentationCore,PresentationFramework,WindowsBase
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "src\SmartLedger.App\Assets\SmartLedger.ico"
$svg = "M5,3 L19,5 L19,20 L6,20 L5,18 Z M8,6 L8,17 M10,14 L13,11 L15,13 L18,9 M15,9 L18,9 L18,12"
$sizes = 16,20,24,32,48,64,128,256
$frames = @()
foreach ($size in $sizes) {
  $visual = New-Object System.Windows.Media.DrawingVisual
  $ctx = $visual.RenderOpen()
  $bg = New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.ColorConverter]::ConvertFromString("#0B1830"))
  $ctx.DrawRoundedRectangle($bg, $null, (New-Object System.Windows.Rect(0,0,$size,$size)), [Math]::Max(2,$size/6), [Math]::Max(2,$size/6))
  $g = [System.Windows.Media.Geometry]::Parse($svg).Clone()
  [double]$scale = ([double]$size * 0.72) / 24.0
  [double]$shift = ([double]$size - ([double]$size * 0.72)) / 2.0
  $group = New-Object System.Windows.Media.TransformGroup
  $group.Children.Add([System.Windows.Media.ScaleTransform]::new($scale,$scale)) | Out-Null
  $group.Children.Add([System.Windows.Media.TranslateTransform]::new($shift,$shift)) | Out-Null
  $g.Transform = $group
  $pen = New-Object System.Windows.Media.Pen((New-Object System.Windows.Media.SolidColorBrush ([System.Windows.Media.ColorConverter]::ConvertFromString("#55B7FF"))), [Math]::Max(1.2,$size/18))
  $ctx.DrawGeometry($null,$pen,$g)
  $ctx.Close()
  $bmp = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($size,$size,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
  $bmp.Render($visual)
  $enc = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
  $enc.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bmp))
  $ms = New-Object System.IO.MemoryStream
  $enc.Save($ms)
  $frames += ,$ms.ToArray()
  $ms.Dispose()
}
$fs = New-Object System.IO.FileStream($out,[System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$frames.Count)
$offset = 6 + 16*$frames.Count
for($i=0;$i -lt $frames.Count;$i++){
  $s=$sizes[$i]
  $bw.Write([byte]$(if($s -ge 256){0}else{$s}))
  $bw.Write([byte]$(if($s -ge 256){0}else{$s}))
  $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([uint16]1); $bw.Write([uint16]32)
  $bw.Write([uint32]$frames[$i].Length); $bw.Write([uint32]$offset)
  $offset += $frames[$i].Length
}
foreach($frame in $frames){$bw.Write($frame)}
$bw.Flush(); $bw.Dispose(); $fs.Dispose()
Write-Output "Wrote $out ($((Get-Item $out).Length) bytes) with sizes: $($sizes -join ',')"
