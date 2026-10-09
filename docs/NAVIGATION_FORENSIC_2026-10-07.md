# Navigation Forensic / Tab-to-Tab Stability — 2026-10-07

## Root causes fixed
- Removed invalid `{StaticResource CurrencyState}` from `Views/ModuleTemplates.xaml`.
- Added `AccountsViewModel.Currency` loaded from `BusinessIdentity.Currency`.
- Reworked `MainViewModel.Navigate` into serialized/coalesced navigation.
- Page state is published only after `LoadAsync` succeeds.
- Older rapid-navigation requests cannot publish stale content.
- Navigation failures are logged to `%LOCALAPPDATA%\SmartLedger\navigation-error.log`.
- Navigation with an open input dialog is blocked to prevent silent data loss.
- Escape dialog handling now covers Accounts, Operations, Sales, Purchases, Inventory, Expenses and Users.
- Quick Actions now open dialogs only when navigation succeeds.
- Added runtime page-permission enforcement: View / Reports / Users / Settings.
- Removed the invalid resource reference; explicit StaticResource audit now reports zero missing keys.

## Runtime evidence
- WPF navigation harness: PASS.
- 13/13 DataTemplates mapped to the 13 AppPage values.
- Forward navigation: PASS.
- Backward navigation: PASS.
- Cross navigation: PASS.
- Repeated navigation: PASS.
- Rapid navigation converges to newest request: PASS.
- Quick Actions: PASS.
- Dialog open/close paths: PASS.
- F5/Ctrl+N/Ctrl+S/Ctrl+K underlying handlers: PASS.
- Sidebar collapse/expand: PASS.
- Page/VM/ContentControl/PageTitle invariants: PASS.
- Limited-user test: SKIPPED because the current database contains no active non-administrator user; no test user was fabricated or inserted.

## Build/Test
- Release build: PASS, 0 warnings, 0 errors.
- dotnet test: PASS, 48/48.
- Final self-contained win-x64 publish: PASS.
- Published runtime verification previously completed on the same self-contained artifact family: process alive, startup-error.log absent, navigation-error.log absent.
- Final v2 publish was rebuilt after permission hardening; direct launch of v2 was blocked by the execution safety boundary, so no false claim is made for that one step.

## SHA-256
- Release SmartLedger.App.dll: 9356BC2E3DD38077B6C38E8497FD8877C0FD507540A121234BD341E768C89F65
- Publish v2 SmartLedger.App.dll: 9356BC2E3DD38077B6C38E8497FD8877C0FD507540A121234BD341E768C89F65
- Publish v2 SmartLedger.App.exe: 222318762E83F301DE22FF11D42D5D1EFF6BBD6CF1AFDA040A4E4D9E39576FF4

## Diagnostic history
The pre-fix `startup-error.log` was archived as:
`%LOCALAPPDATA%\SmartLedger\startup-error-pre-navigation-20261007.log`
It contained the previously observed XamlParseException for missing `CurrencyState`. No new startup-error.log or navigation-error.log was produced by the final startup smoke check.

## Final artifact
`publish\SmartLedger_Final_20261007_v2\`
