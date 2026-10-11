 # Dialog Components Extraction & Form Ergonomics Report

Generated: 2026-10-11 03:28:49 +03:00
Workspace: C:\Users\pc\SmartLedger-Build-Clean-20261010

## Extracted components
- AccountDialogControl.xaml: ResourceDictionary DataTemplate extracted from existing parent overlay; component template keys: AccountDialogControlTemplate.
  - TextBox count: 8; ScrollViewer count: 0.
  - XML parse: PASS.
- ExpenseVoucherDialogControl.xaml: ResourceDictionary DataTemplate extracted from existing parent overlay; component template keys: ExpenseVoucherDialogTemplate.
  - TextBox count: 2; ScrollViewer count: 0.
  - XML parse: PASS.
- ProductDialogControl.xaml: ResourceDictionary DataTemplate extracted from existing parent overlay; component template keys: ProductDialogControlTemplate.
  - TextBox count: 16; ScrollViewer count: 1.
  - XML parse: PASS.

## Parent integration and bindings
- AccountsView.xaml now hosts ContentControl with Content={Binding}, ContentTemplate={StaticResource AccountDialogControlTemplate}; IsDialogOpen remains inside the extracted overlay and commands bind to the original AccountsViewModel.
- InventoryView.xaml now hosts ProductDialogControlTemplate with original InventoryViewModel bindings and commands preserved.
- ExpensesView.xaml now hosts ExpenseVoucherDialogTemplate with original ExpensesViewModel bindings and commands preserved.
- App.xaml merges all three dictionaries after the shared theme and module dictionaries.

## Layout and BIDI validation
- Account and expense extracted dialog XML parsed successfully; neither contains a ScrollViewer.
- Product dialog retains its existing ScrollViewer with VerticalScrollBarVisibility=Auto and dialog MaxHeight=940. Therefore zero vertical scrolling on standard-height displays is NOT certified by this pass; forcing scrollbar off without reducing measured content risks clipping. Product layout still needs runtime visual validation and compact row sizing.
- Existing binding expressions, keyboard shortcuts, close/save commands and overlay visibility remain in the extracted templates.
- 40px control height and CornerRadius=6 are not globally asserted on every input in this extraction pass; inherited styles and specialized controls require follow-up audit.

## Preservation
- BackupService.cs SHA-256: 1AE2609107242E297D854284B7B0938C3C95F2B725F66FA229922600D27114A6
- Preservation check: PASS (same hash captured before extraction and after build/test).

## Build output (full)
  Determining projects to restore...
  All projects are up-to-date for restore.
  SmartLedger.Core -> C:\Users\pc\SmartLedger-Build-Clean-20261010\src\SmartLedger.Core\bin\Debug\net10.0\SmartLedger.Core.dll
  SmartLedger.Infrastructure -> C:\Users\pc\SmartLedger-Build-Clean-20261010\src\SmartLedger.Infrastructure\bin\Debug\net10.0\SmartLedger.Infrastructure.dll
  SmartLedger.App -> C:\Users\pc\SmartLedger-Build-Clean-20261010\src\SmartLedger.App\bin\Debug\net10.0-windows\SmartLedger.App.dll
  SmartLedger.Tests -> C:\Users\pc\SmartLedger-Build-Clean-20261010\tests\SmartLedger.Tests\bin\Debug\net10.0-windows\SmartLedger.Tests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:06.61

## Test output (full)
Test run for C:\Users\pc\SmartLedger-Build-Clean-20261010\tests\SmartLedger.Tests\bin\Debug\net10.0-windows\SmartLedger.Tests.dll (.NETCoreApp,Version=v10.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:   105, Skipped:     0, Total:   105, Duration: 6 s - SmartLedger.Tests.dll (net10.0)

## Result
- Extraction and resource registration compiled successfully.
- Automated test suite: 105 passed, 0 failed, 0 skipped.
- This report explicitly records the remaining no-scroll visual certification gap rather than claiming an unverified zero-scroll result.
