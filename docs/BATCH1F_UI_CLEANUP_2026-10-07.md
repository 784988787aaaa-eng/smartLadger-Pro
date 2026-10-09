# BATCH 1F — UI Cleanup & Visual Leadership
Date: 2026-10-07
Scope: C:\Users\pc\Desktop\SmartLedger_Run_2026-10-05 only.

## Implementation
- Recovery now has a real PasswordBox for "عبارة الاسترداد", separate from the displayed recovery hint. New password and confirmation remain separate PasswordBoxes.
- Login/Recovery/MainWindow brand duplication was reduced. MainWindow keeps one visible SmartLedger brand presence in the sidebar; Login keeps one visible brand name; Recovery no longer repeats SmartLedger as visible header text.
- Dashboard title "الرئيسية" no longer has a duplicated/legacy arrow beside it. The title uses the same 18px inner alignment as the quick-actions/latest-operations card headings.
- Dashboard summary language remains "لنا" / "علينا".
- Add Account dialog was cleaned: proper header close icon, no broken center glyph, clear fields, opening-balance section, neutral segmented direction choice.
- Opening balance is persisted through the existing Operations ledger as a clearly labeled "رصيد افتتاحي"; no second balance store was introduced. Creation is transactional with account creation.
- Operations UI now exposes only "استلام" / "سداد" as the movement choice. Direction is not redundantly selected for the same movement.
- Recent-operation labels were normalized to "استلام", "سداد", "له", "عليه"; recent-operation coloring is neutral rather than semantic red/green.
- Account table headers use "لنا" / "علينا" without the heavier "مدين/دائن" parentheticals.
- Other malformed dialog close glyphs in ModuleTemplates were replaced with the shared vector CloseIcon.

## Requested Gate
| Item | Result | Evidence |
|---|---|---|
| Recovery Input | PASS | Published runtime Recovery exposes 3 edit controls; source has a dedicated PasswordBox named/labelled "عبارة الاسترداد". |
| Forgot Password | PASS* | Published Login exposed 4 buttons and Recovery navigation was invoked successfully. *Full password-reset success was not exercised because no controlled recovery phrase was used. |
| Duplicate SmartLedger Text | CLEAN | XAML audit found only intentional brand/title occurrences; Login runtime exposed one visible SmartLedger text. |
| Old Arrow Icon near الرئيسية | REMOVED | Dashboard template has a clean "الرئيسية" heading with no adjacent legacy arrow path. |
| Dashboard Heading Alignment | PASS | Dashboard headings share the same parent Grid and the same 18px inner alignment for the title/section cards; no per-TextBlock corrective offsets were added. |
| Quick Actions Alignment | PASS | Same Dashboard Grid/Card alignment system as Latest Operations. |
| Latest Operations Alignment | PASS | Same Dashboard Grid/Card alignment system as Quick Actions. |
| Add Account Dialog | PASS | Published runtime dialog opened; form exposed name/phone/note/opening-amount fields and a dedicated direction choice. |
| Broken Center Icon/Button | REMOVED | Broken "أ—" glyphs removed; dialog close controls use vector CloseIcon. |
| Opening Balance | PASS | UI creation path was exercised with a temporary QA account and 123.45 opening amount; database/domain test verifies ledger entry and balance. QA records were cleaned afterward. |
| Opening Balance Direction | PASS | Runtime exposed 2 equal segmented choices; integration test verifies DebtToUs and DebtOnUs balances. |
| استلام / سداد | PASS | Published Operations screen exposed exactly 2 segmented choices; integration test applies Receive then Payment and verifies resulting balance. |
| لنا / علينا | PASS | Dashboard/account summary terminology preserved as "لنا"/"علينا"; no global replacement with heavy accounting terminology. |
| له / عليه | PASS | Opening-balance direction and ledger labels use "له"/"عليه" in account-specific context. |
| Balance Calculation | PASS | Opening-balance integration test verifies 250 -> receive 50 -> payment 25 => 225; opposite direction verifies -100. |
| Regression | PASS | Full test suite: 46 passed, 0 failed, 0 skipped. App build: 0 warnings, 0 errors. |
| Build | PASS | dotnet build SmartLedger.App Release => succeeded, 0 warnings, 0 errors. |
| Tests | 46 passed | Passed: 46, Failed: 0, Skipped: 0, Total: 46. |
| Publish | PASS | Clean Release win-x64 self-contained publish exited 0. |
| Published EXE | PASS | publish\SmartLedger\SmartLedger.App.exe launched successfully; runtime Login UIA exposed 2 edits/4 buttons; Recovery navigation exposed 3 edits/7 buttons. |

## Financial Logic Evidence
OpeningBalanceTests.OpeningBalance_UsesLedgerAsSingleSourceOfTruth covers:
1. account with no opening balance creates no ledger operation;
2. opening "له" creates a DebtToUs ledger entry labelled "رصيد افتتاحي";
3. opening "عليه" creates a DebtOnUs ledger entry;
4. balances split correctly into OwedToUs/WeOwe;
5. Receive reduces the receivable balance;
6. Payment increases it according to the existing domain balance rules.

No second balance calculation/store was introduced.

## Runtime Cleanup
A temporary QA account was created only to exercise the published UI opening-balance path. The temporary QA records were removed after the test. The project-side temporary cleanup files were removed; docs\qa_tmp does not remain.

Final process state: SMARTLEDGER_PROCESS=NONE.

## Visual-QA Limitation
Runtime UI Automation and control geometry were used for actual published-EXE verification. A pixel-level screenshot review was not used as PASS evidence because the remote desktop tool available in this session did not expose a render/screenshot inspection surface. Therefore this report does not claim pixel-perfect visual PASS beyond the verified runtime control/layout evidence and source alignment checks.
