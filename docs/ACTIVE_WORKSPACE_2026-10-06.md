# SmartLedger — Active Workspace Lock

## Active project
`C:\Users\pc\Desktop\SmartLedger_Run_2026-10-05`

## Release executable used for runtime verification
`publish\SmartLedger\SmartLedger.App.exe`

## Database isolation
Legacy database variants were moved to:
`C:\Users\pc\Desktop\SmartLedger_Old_Versions_Archive_2026-10-06\Legacy_Databases_2026-10-06`

The active application created a fresh `smartledger.db` under `%LOCALAPPDATA%\SmartLedger` and bootstrapped SQLite `user_version = 3`.

## Verified remediation
- Removed the invalid WPF PasswordBox style inheritance that caused `Initialization of 'System.Windows.Controls.PasswordBox' threw an exception.`
- Rebuilt Release successfully: 0 warnings, 0 errors.
- Automated test suite: 12 passed, 0 failed.
- Published Release executable rebuilt successfully.
- Published executable launched successfully and displayed the premium RTL login screen.

## Legacy workspace handling
Old SmartLedger projects, builds, archives, dumps, inspection tools, and database variants were moved into `SmartLedger_Old_Versions_Archive_2026-10-06` where Windows permissions allowed. `Daftaaaar` remains on the Desktop because Windows denied the directory rename; it was not deleted or modified.

## QA evidence
Runtime screenshots are stored in this `docs` folder for traceability.
