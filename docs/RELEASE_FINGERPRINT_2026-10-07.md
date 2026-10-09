# SmartLedger Release Fingerprint — 2026-10-07

Project: `C:\Users\pc\Desktop\SmartLedger_Run_2026-10-05`

## Verified release artifacts

- Published EXE SHA256: `222318762E83F301DE22FF11D42D5D1EFF6BBD6CF1AFDA040A4E4D9E39576FF4`
- Published App assembly SHA256: `F27298E5373853C380DDDA98FC53CF7FD8EE130CC653064EEAD3CC70FA2CC3A1`
- Published Infrastructure SHA256: `29E81F2CA25E9782DE663D66CC6D1BCC320D11A5D3E28CBBBB1394440B1041A4`
- Published Core SHA256: `5D19A8EFD9A6C143CF01F671B02931B26686BD08BB26DC35CB60073A71C51F3A`

## Verification

- Release build: PASS — 0 warnings, 0 errors.
- Automated tests: PASS — 48/48.
- Published EXE startup/responding: PASS.
- Published login UI automation: PASS — 2 Edit controls and 4 Buttons observed.
- Authenticated Accounts navigation: PASS after hardening — published process remained responsive after opening Accounts.
- Accounts/Add Account UI inspection: PASS — compact account-entry controls observed at runtime; account dialog was opened for inspection.
- Full authenticated journey (sales, purchases, inventory, reports, users, settings, backup/restore, recycle, audit): NOT VERIFIED because credential injection into the real Login UI was blocked by the test-tool security boundary. No authentication bypass was added.

## Critical hardening included

- Prevented Accounts book-selection refresh re-entrancy during group/type synchronization; this removed the reproduced StackOverflow crash when navigating to Accounts.
- Recycle Bin stock-movement restore now reapplies the movement's inventory effect; covered by automated regression test.
- Added account detail/statement surface with current balance, لنا/علينا summary, movement history, and PDF/Excel export paths.
- Account PDF includes identity, account/book context, running balance and organized statement table.
- Account Excel export writes numeric amounts and real Excel date serials with date formatting.
- Removed seeded placeholder business phone data from new databases.
- Reduced oversized Inventory/Sales/Purchases/User dialogs.

## Release rule

The published folder must be treated as the exact artifact set for the hashes above. Do not publish over it while the application is running.
