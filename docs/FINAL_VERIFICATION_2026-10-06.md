# HISTORICAL / SUPERSEDED

هذه الوثيقة سجل تاريخي من مرحلة سابقة وليست دليل Release Acceptance الحالي. المرجع الحالي الوحيد هو docs/CURRENT_RELEASE_ACCEPTANCE_2026-10-07.md. لا تستخدم أرقام الاختبارات أو حالات Final الواردة هنا للحكم على الإصدار الحالي.

# SmartLedger — Final Engineering Verification

Date: 2026-10-06
Target: .NET 10 / WPF / Windows x64

## Implemented in this hardening pass
- Rebuilt the dark luxury Design System with centralized Colors, Typography and Styles resources.
- Reworked LoginWindow into a split luxury RTL experience with vector icons, password visibility toggle, guest login and secure-state messaging.
- Reworked MainWindow shell with compact RTL navigation, search, privacy control, window chrome and draggable custom title area.
- Reworked Dashboard, Accounts, Operations, Inventory, Sales, Purchases, Expenses, Reports, Users and Settings templates to use the same visual language.
- Added atomic invoice transaction orchestration: invoice + lines + stock movement + optional ledger operation are committed in one SQLite transaction and rollback together on failure.
- Sales and Purchases ViewModels now use the atomic transaction path.
- Added an integration test proving invoice/line/stock/ledger rollback on insufficient stock.
- Hardened Excel export: RTL worksheet, frozen header panes, real numeric amount cells and `#,##0.00` number format, dark business header styling.
- Cleaned Arabic source text and removed detected mojibake markers from source/tests/docs.
- Completed Release solution configuration so the solution builds without configuration warnings.

## Verification gates
- `dotnet build SmartLedger.sln -c Release --no-restore`: PASS — 0 warnings, 0 errors.
- `dotnet test tests/SmartLedger.Tests/SmartLedger.Tests.csproj -c Release --no-build`: PASS — historical result superseded.
- `dotnet publish ... -r win-x64 --self-contained false`: PASS.
- Published executable launched on the connected Windows device and reported `Responding=True`, main window `SmartLedger`.
- Source/docs mojibake scan: PASS — no markers detected.

## Release artifact
The final cumulative ZIP is generated from the full solution source, tests, docs and x64 publish output while excluding transient `bin`/`obj` build folders from source trees.
