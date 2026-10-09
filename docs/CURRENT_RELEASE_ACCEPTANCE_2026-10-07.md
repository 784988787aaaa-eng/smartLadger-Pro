# SmartLedger — Current Release Acceptance Evidence
## 2026-10-07 — Current authoritative status

> هذه الوثيقة هي المرجع الحالي الوحيد لحالة Release Acceptance.
> لا تُستخدم تقارير 2026-10-06 أو النتائج الأقدم كدليل على الحالة الحالية.

## 1. Release identity

| Item | Current evidence | Result |
|---|---|---|
| Release source EXE | src\\SmartLedger.App\\bin\\Release\\net10.0-windows\\SmartLedger.App.exe | PASS |
| Published EXE | publish\\SmartLedger\\SmartLedger.App.exe | PASS |
| SHA-256 — source EXE | 0B8837D0871EAB790E5AC92C8E6FCA1345FFC5B3214274198A1C59EDAC0E4A79 | PASS |
| SHA-256 — published EXE | 0B8837D0871EAB790E5AC92C8E6FCA1345FFC5B3214274198A1C59EDAC0E4A79 | PASS |
| SHA match | Identical | PASS |

تم استخراج SHA في 2026-10-07 بعد آخر Release Build، وليس من تقرير سابق.

## 2. Build / automated tests

Release Build: PASS — 0 warnings / 0 errors.

Automated tests: 45/45 PASS, 0 failed, 0 skipped.

| Category | Source test files | Count | Result |
|---|---|---:|---|
| Domain/Core | CoreInvariantsTests | 9 | PASS |
| Database / integration | DatabaseIntegrationTests | 15 | PASS |
| Backup / Restore | BackupServiceHardeningTests | 8 | PASS |
| Permissions | UserPermissionTests | 4 | PASS |
| Audit security | AuditSecurityTests | 2 | PASS |
| Reports / Export | ReportExportTests | 3 | PASS |
| Recovery | RecoveryTests | 4 | PASS |
| Total | 7 test files | 45 | PASS |

> هذه نتائج آلية فقط. لا تُعتبر بديلًا عن Manual UI Acceptance.

## 3. Schema source of truth

المصدر الفعلي هو src\\SmartLedger.Infrastructure\\Data\\Database.cs.

- PRAGMA user_version = 6.
- Migration V1 إلى V6 موجودة.
- V6 ينشئ UserPermissions ويمنح جميع الصلاحيات للمديرين.
- BackupService يدعم Schema 6.
- تم التحقق من قاعدة Release المعزولة بقراءة فقط: PRAGMA integrity_check = ok و user_version = 6.

## 4. Batch 1 — Login Recovery acceptance

Published EXE was re-tested after the A-001 Login UI fix.

### Evidence obtained

- Guest Login is absent from the current Login source and Published EXE.
- «نسيت كلمة المرور؟» is present in the current Login source and Published EXE.
- Published EXE successfully opened the Recovery Window from Login.
- Recovery username step completed and recovery hint/reset controls became visible.
- Recovery backend success, wrong phrase rejection, password confirmation mismatch and secret-free Audit are covered by the 4 new automated recovery tests.
- Published EXE Close was invoked and the application process exited cleanly.
- Original local DB SHA remained unchanged.

### A-001

Feature: تسجيل الدخول كضيف  
Result: CLOSED

The non-functional Guest button and GuestCommand reference were removed from the current product UI/source. Historical backups may still contain the old reference and are not product source.

### Forgot Password

Result: PASS for implementation and Published-EXE navigation.  
NOT VERIFIED for end-to-end successful reset using a controlled known recovery phrase from the Published EXE.

Reason for the evidence gap: the available remote UI automation cannot safely seed a controlled secret into the isolated PasswordBox test account without bypassing the password security boundary. Automated integration tests prove the complete recovery mutation and new-password verification path.

### Close

Result: PASS

Published EXE Login close terminated the application process cleanly.

### Batch gate

Manual product acceptance remains BLOCKED until the Published-EXE successful recovery evidence gap is closed.
## 5. Screen acceptance matrix

| Screen / Area | Result | Evidence |
|---|---|---|
| Startup | PASS | Published process launched/responded |
| Bootstrap | PASS (source) / NOT VERIFIED (full UI) | Existing RecoveryPhrase/RecoveryHint path remains intact |
| Login — visual/RTL | PASS | Published control tree and runtime inspection |
| Login — normal credential flow | NOT VERIFIED | No controlled known credential was available for this isolated runtime |
| Login — Guest | PASS | A-001 CLOSED; current Published EXE has no Guest action |
| Dashboard | NOT VERIFIED | توقف القبول |
| Accounts / Books | NOT VERIFIED | توقف القبول |
| Operations | NOT VERIFIED | توقف القبول |
| Sales | NOT VERIFIED | توقف القبول |
| Purchases | NOT VERIFIED | توقف القبول |
| Inventory | NOT VERIFIED | توقف القبول |
| Expenses | NOT VERIFIED | توقف القبول |
| Reports | NOT VERIFIED | توقف القبول |
| Users / Permissions | NOT VERIFIED (UI) | الاختبارات الآلية PASS، لكن UI لم تُقبل بعد |
| Settings | NOT VERIFIED | توقف القبول |
| Backup UI | NOT VERIFIED | الخدمة آليًا PASS؛ UI لم تُقبل بعد |
| Restore UI | NOT VERIFIED | الخدمة آليًا PASS؛ UI لم تُقبل بعد |
| Recycle Bin | NOT VERIFIED | توقف القبول |
| Audit UI | NOT VERIFIED | repository/security tests PASS؛ UI لم تُقبل بعد |

## 6. Critical workflows

| Workflow | Result | Reason |
|---|---|---|
| A: account → operations → partial settlement → statement → PDF → Excel | NOT VERIFIED | Manual acceptance stopped |
| B: product → purchase → stock-in → sale → stock-out → invoice → PDF | NOT VERIFIED | Manual acceptance stopped |
| C: mid-transaction failure / atomic rollback | PASS (automated) | Database/integration tests |
| D: delete → recycle bin → restore | PASS (automated) | DatabaseIntegrationTests |
| E: backup → modify → restore | PASS (service/integration) | BackupServiceHardeningTests |
| Backup UI | NOT VERIFIED | UI round stopped |
| Users/Permissions UI | NOT VERIFIED | UI round stopped |
| Audit UI | NOT VERIFIED | UI round stopped |

## 7. Backup / Restore

Automated service/integration: PASS.

Verified in isolated tests:
- WAL committed data preservation.
- corrupt archive.
- SHA mismatch.
- unsupported schema.
- replacement failure.
- cancellation.
- connection closure.
- interruption and emergency recovery.

Manual UI: NOT VERIFIED because the acceptance round was stopped at A-001.

## 8. Reports / Export

Automated:
- PDF/Excel export tests: PASS.
- Numeric Excel values/formatting: PASS.
- PDF profiles are implemented/tested as generated output.

Physical printing: NOT VERIFIED.

Classification:
- A4 PDF profile: automated PASS.
- A5 PDF profile: automated PASS.
- 80mm PDF profile: automated PASS.
- 58mm PDF profile: automated PASS.
- Physical printer: NOT VERIFIED.

## 9. DPI

baseline display scale / 125% / 150% and small-screen acceptance: NOT VERIFIED in this batch.

No commercial PASS is assigned to DPI acceptance.

## 10. Runtime / startup-error.log

Published executable launched and responded.

The historical startup-error.log under the original local data directory remained at its prior timestamp (2026-10-07 02:30:26) and was not used as evidence of a current failure.

Manual acceptance was performed against an isolated database clone, then the original local data directory was restored.

Original database clone/restore evidence:
- Original DB SHA before/after isolation: BEB84C83B2A2B906858F5096C856C3E16FEAFBB9EBB8CEC03B698815B288BE4C.
- Restored SHA matched exactly.
- PRAGMA integrity_check = ok.
- PRAGMA user_version = 6.

## 11. Commercial gate

NOT READY / GATE BLOCKED

Reason: A-001 is a real published-UI defect, and the Manual UI Acceptance matrix is therefore incomplete.

This document intentionally does not claim:
- baseline display scale completion.
- commercial readiness.
- complete screen acceptance.
- physical printing PASS.

## 12. Next independent batch

1. Fix A-001 only.
2. Rebuild Release.
3. Re-run all 41 existing tests; do not add tests merely to increase the count.
4. Republish.
5. Recompute both EXE SHA-256 values.
6. Re-run Manual UI Acceptance from Startup through Recycle Bin.
7. Execute the five commercial workflows.
8. Verify Backup/Restore UI.
9. Verify Users/Permissions UI.
10. Verify DPI 100/125/150 and small-screen behavior.
11. Re-run runtime/startup-error evidence.
12. Issue a new acceptance report only after evidence exists.

## Evidence files

- docs\\acceptance-bootstrap.png
- docs\\acceptance-current.png
- docs\\acceptance-build.log
- docs\\acceptance-tests.log
- current source: src\\SmartLedger.Infrastructure\\Data\\Database.cs
- current UI source: src\\SmartLedger.App\\Views\\LoginWindow.xaml
- current login VM: src\\SmartLedger.App\\ViewModels\\LoginViewModel.cs