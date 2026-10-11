# SmartLedger Phase 4 Remote Execution Report
$ErrorActionPreference = 'Continue'
$root = 'C:\Users\pc\SmartLedger-Build-Clean-20261010'
$report = Join-Path $root 'REMOTE_PHASE4_COMPLETION_REPORT.md'
Set-Location $root
'# SmartLedger Phase 4 Remote Execution Report' | Set-Content -Path $report -Encoding UTF8
'' | Add-Content $report
"**Timestamp:** $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')" | Add-Content $report
"**Host:** $env:COMPUTERNAME" | Add-Content $report
"**OS:** $((Get-CimInstance Win32_OperatingSystem).Caption) ($((Get-CimInstance Win32_OperatingSystem).Version))" | Add-Content $report
"**SDK:** $(dotnet --version)" | Add-Content $report
"**Repository:** $(git remote get-url origin)" | Add-Content $report
"**Branch/commit:** $(git branch --show-current) / $((git rev-parse --short HEAD))" | Add-Content $report
function Run-Step([string]$title, [string]$exe, [string[]]$arguments) {
  '' | Add-Content $report
  "## $title" | Add-Content $report
  $output = & $exe @arguments 2>&1
  $code = $LASTEXITCODE
  $output | ForEach-Object { "$_" | Add-Content $report }
  "**Exit code:** $code" | Add-Content $report
  Write-Output "$title EXIT=$code"
  if ($code -ne 0) { return $false }
  return $true
}
$ok = Run-Step 'Restore' 'dotnet' @('restore','SmartLedger.sln')
if ($ok) { $ok = Run-Step 'Release build (warnings treated as errors)' 'dotnet' @('build','SmartLedger.sln','-c','Release','--no-restore','-warnaserror') }
if ($ok) { $ok = Run-Step 'Automated tests' 'dotnet' @('test','tests/SmartLedger.Tests/SmartLedger.Tests.csproj','-c','Release','--no-build','--logger','console;verbosity=normal') }
if ($ok) { $ok = Run-Step 'Publish win-x64 framework-dependent executable' 'dotnet' @('publish','src/SmartLedger.App/SmartLedger.App.csproj','-c','Release','-r','win-x64','--self-contained','false','-o','.\publish') }
'' | Add-Content $report
'## Publish artifact' | Add-Content $report
$exePath = Join-Path $root 'publish\SmartLedger.App.exe'
if (Test-Path $exePath) { $fi = Get-Item $exePath; 'EXE exists: YES' | Add-Content $report; "Path: $exePath" | Add-Content $report; "Size bytes: $($fi.Length)" | Add-Content $report; "Last write: $($fi.LastWriteTime)" | Add-Content $report; Write-Output "EXE_EXISTS size=$($fi.Length)" } else { 'EXE exists: NO' | Add-Content $report; Write-Output 'EXE_MISSING'; $ok = $false }
'' | Add-Content $report
'## Git working tree after verification' | Add-Content $report
git status --short --branch 2>&1 | ForEach-Object { "$_" | Add-Content $report }
'' | Add-Content $report
'## Final status' | Add-Content $report
if ($ok) { 'Verification pipeline: PASS (restore/build/tests/publish)' | Add-Content $report; Write-Output 'PIPELINE_PASS' } else { 'Verification pipeline: FAIL — inspect the step output above; later steps may not have run.' | Add-Content $report; Write-Output 'PIPELINE_FAIL' }
Write-Output "REPORT=$report"
