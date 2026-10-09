# BATCH 1D — Publish/AppHost + Professional Icon Acceptance
Date: 2026-10-07
Project: C:\Users\pc\Desktop\SmartLedger_Run_2026-10-05

## Root Cause
The previous publish directory contained an apphost/runtimeconfig mismatch: SmartLedger.App.exe behaved as a self-contained apphost while runtimeconfig.json described framework-dependent execution. The apphost therefore searched only the publish directory and reported Microsoft.NETCore.App 10.0.0 missing, despite 10.0.12 being installed globally.

## Fix
- Added deterministic WinX64 publish profile:
  Properties\PublishProfiles\WinX64.pubxml
- RuntimeIdentifier: win-x64
- SelfContained: true
- UseAppHost: true
- PublishSingleFile: false
- PublishTrimmed: false
- PublishReadyToRun: false
- Cleaned publish output before final publish.
- Added real multi-size SmartLedger.ico and ApplicationIcon project property.
- No Recovery/Login architecture change.
- No database schema change.
- No new NuGet dependency.

## Verification
- Release build: PASS, 0 warnings, 0 errors.
- Tests: 45/45 PASS, 0 skipped.
- Final publish: PASS, exit code 0, 0 warnings.
- Final runtimeconfig uses includedFrameworks 10.0.12.
- Final deps target win-x64 and contains runtime packs.
- Published directory contains hostfxr.dll, hostpolicy.dll, coreclr.dll, WPF runtime assets and SmartLedger.App.exe.
- Published EXE launches directly and produces a live SmartLedger.App.exe process.
- Login Window found by UI Automation; 2 edit controls and 4 buttons detected.
- Published EXE closes cleanly; no SmartLedger.App.exe process remains.
- startup-error.log: NOT PRESENT — no startup exception evidence generated.
- Application icon: PASS. ApplicationIcon is embedded; associated icon extraction reports 32x32. Source ICO is 12,224 bytes and multi-size.
- In-app icon system: PASS by source audit: vector Geometry/Path icons, shared SideIcon style, consistent stroke treatment; no Emoji/icon-font/Unicode icon matches found.

## Database Safety
Original database was moved to an isolated hold directory during acceptance and restored unchanged.
Before SHA-256: BEB84C83B2A2B906858F5096C856C3E16FEAFBB9EBB8CEC03B698815B288BE4C
After SHA-256:  BEB84C83B2A2B906858F5096C856C3E16FEAFBB9EBB8CEC03B698815B288BE4C
integrity_check: ok
user_version: 6

## Remaining Acceptance Gap
Published-EXE credential success cannot be verified in this execution channel because direct automated entry of password/recovery secrets into PasswordBox is blocked by the execution security boundary. No bypass was used.
Therefore:
- Normal Login Success: NOT VERIFIED
- Recovery Published Success: NOT VERIFIED
- Login with New Password: NOT VERIFIED
- Old Password Rejection: NOT VERIFIED

Batch 1D remains BLOCKED until those two real credential workflows are manually exercised on the published EXE using the isolated acceptance credentials.
