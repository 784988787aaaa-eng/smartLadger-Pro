# HISTORICAL / SUPERSEDED

هذا سجل زمني تاريخي للتطوير، وليس دليل Release Acceptance الحالي. المرجع الحالي الوحيد هو docs/CURRENT_RELEASE_ACCEPTANCE_2026-10-07.md.

# Progress Report — Premium Hardening

## Implemented
- Clean .NET 10 Core/Infrastructure/App/Test targets.
- Centralized domain entities and financial/inventory engines.
- SQLite schema with migrations to v3.
- Atomic operation and stock mutation paths.
- Login / first-run administrator bootstrap.
- Premium dark RTL Shell, sidebar, top bar, dashboard, accounts, operations, inventory.
- PDF / Excel / Arabic amount words / backup / audit / WhatsApp / HWID services.
- Unit and SQLite integration tests.

## Verification gate
The project is considered release-candidate only after the device build and test commands finish with zero errors and the WPF process remains responsive.

## 2026-10-06 � Final Professional Hardening
- Luxury RTL UI refresh, centralized theme resources, login/shell/dashboard polish.
- Atomic invoice + stock + ledger transaction path and rollback integration test.
- Excel RTL/numeric/frozen-pane hardening.
- Release solution configuration fixed; final build 0 warnings/0 errors; tests historical result superseded.
- x64 publish launched successfully on the connected Windows device.

## Pre-Modification Ledger � Dashboard Artisan Pass (20261006_234244)
- MainWindow.xaml � top bar, search, notifications, chrome � callers: MainViewModel + shell bindings � financial impact: none � XAML risk: medium � protection: backup + build/test.
- ModuleTemplates.xaml � Dashboard KPI/actions/recent table � caller: DashboardViewModel � financial impact: presentation only � XAML risk: medium � protection: backup + build/test.
- MainViewModel.cs � dashboard navigation/search/quick actions � callers: MainWindow.xaml � financial impact: navigation only � risk: medium � protection: backup + 18-test suite.
- No database schema or financial mutation path is being changed in this batch.

## Pre-Modification Ledger — True Spatial RTL & Dashboard Visual Forensics (20261007)
- MainWindow.xaml — Arabic shell geometry, centered search, page-header placement, notification RTL alignment, sidebar density — callers: MainViewModel + shell bindings — financial impact: none — XAML risk: medium — protection: backup + build/test.
- MainWindow.xaml.cs — shell-only interaction support for drag/reorder and search/keyboard behavior if required — financial impact: none — risk: low/medium — protection: backup + build/test.
- ModuleTemplates.xaml — dashboard quick actions, true RTL recent-movements column order, readable date/day formatting — caller: DashboardViewModel + MainWindow host — financial impact: presentation only — risk: medium — protection: backup + build/test.
- Styles.xaml — typography, DataGrid density, Arabic alignment, sidebar/button readability — callers: all WPF views — financial impact: none — blast radius: high — protection: backup + full build/test.
- MainViewModel.cs — quick-action collection/reordering command bindings only — callers: dashboard DataTemplate — financial impact: navigation only — risk: medium — protection: backup + 18-test suite.
- New UI helpers — serial-number column behavior, Arabic date formatting, quick-action drag/reorder behavior — no DB/repository mutation.
- Global RTL invariant: Arabic interfaces start visually from the right; DataGrid first column is the rightmost column.

## Verification — True Spatial RTL & Dashboard Visual Forensics (20261007)
- Dashboard rebuilt around a right-origin Arabic composition: page title moved into content, search centered geometrically, utility chrome isolated to the left.
- Quick actions are command-bound, keyboard/mouse executable, and reorderable by drag-and-drop.
- Recent movements now use the Arabic column sequence: serial → account → amount → type → date/time/day.
- Serial numbering is applied consistently to DataGrid tables through a reusable behavior.
- Arabic date formatting preserves Arabic day names with Latin digits.
- Typography switched to Display formatting for sharper baseline display scale rendering; table text is larger and no longer uses ellipsis.
- Notification center is compact, explicitly RTL, right-aligned, and uses Arabic-first hierarchy.
- Build: 0 warnings / 0 errors. Tests: historical result superseded.
- Arabic spatial convention corrected per project rule: the Arabic reading start/right is represented by the physical left side of the application; sidebar and primary page heading now originate from screen-left.
- All WPF DataGrids now use a physical left-to-right column rail for the requested Arabic spatial order while keeping cell text Arabic/right-aligned.
- Serial column is globally injected as the first physical column: م → domain columns.
- Dashboard heading is now only الرئيسية, positioned on the Arabic-origin side; the redundant لوحة الأعمال subtitle treatment was removed.
- Taskbar protection: application maximize no longer uses Topmost/virtual-screen fullscreen geometry; Windows taskbar remains visible.
- Build: 0 warnings / 0 errors. Tests: historical result superseded.
- Running SmartLedger.App process remains verified responsive after rebuild.



## Pre-Modification Ledger — Runtime Main-Window Forensics (20261007)
- App.xaml.cs — temporary audit-only guest bypass to open the current MainWindow for visual forensics; caller: WPF startup; financial impact: none; blast radius: startup only; protection: copied to `_ForensicsBackups/20261007_audit_App.xaml.cs`; mandatory restore after capture.
- No financial/domain/database mutation is part of this audit step.


## Pre-Modification Ledger — Dashboard Screenshot Forensics Fix (20261007)
- ModuleTemplates.xaml — dashboard title/action/table spatial order and explicit serial behavior; presentation-only; protection: backup `20261007_ModuleTemplates_before_dashboard_fix.xaml`.
- Styles.xaml — DataGrid row rendering and table digit typography; presentation-only, high blast radius; protection: backup `20261007_Styles_before_dashboard_fix.xaml`.
- DataGridSerialBehavior.cs — serial injection reliability review; presentation-only; protection: backup `20261007_DataGridSerialBehavior_before_dashboard_fix.cs`.
- No financial/database mutation path is touched.


## Screen Completion — Main Dashboard + Login Forensics (20261007)
- Confirmed spatial convention: sidebar physically RIGHT; dashboard title `الرئيسية` physically RIGHT; dashboard/table reading rail is RIGHT→LEFT.
- Dashboard table finalized physically RIGHT→LEFT: `م` → `الحساب` → `المبلغ` → `الحركة` → `التفاصيل` → `اليوم والتاريخ`.
- Serial numbering is now explicit on Dashboard and global serial behavior duplicate-detects both string and TextBlock headers.
- Fixed TextBox template content host so field text is vertically/horizontally presented inside the field instead of disappearing/clipping.
- Login close button moved to the professional top-right position of the login pane; username/password padding corrected around icons.
- Removed invalid custom numeric font usage that caused runtime `FontFamily` exceptions; serial behavior uses system message font and numeric cells use the validated Arabic font resource.
- Runtime final capture verified: no FontFamily dialog; serial numbers 1–5 visible; required column order verified; sidebar RIGHT; title RIGHT; quick actions RTL order.
- Audit-only guest bypass was restored to the original `App.xaml.cs` after runtime capture; no audit bypass remains in source.
- Build verified: 0 warnings, 0 errors.
