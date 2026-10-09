# BATCH 1 — Final Engineering / Product Acceptance Audit
## Forgot Password + Login Recovery Path
### 2026-10-07

## 1. Findings
- PASS — RecoveryHash and RecoveryHint were preserved and are now connected to a real recovery flow.
- PASS — Existing PasswordHasher is reused; no second hashing/encryption mechanism was introduced.
- PASS — No database schema change; schema remains 6.
- PASS — Guest Login is absent from current Login source; remaining GuestCommand matches are historical backup files only.
- PASS — Login now opens a dedicated Recovery Window.
- PASS — Username is required before the recovery hint and reset fields are revealed.
- PASS — Successful recovery updates PasswordHash and writes only non-secret audit details.
- PASS — Wrong recovery phrase leaves the existing password unchanged.
- PASS — Password confirmation mismatch is rejected before hashing.
- PASS — Unknown users do not receive a recovery hint.
- PASS — Published EXE opened Recovery from the visible Forgot Password action.
- PASS — Published Recovery UI displayed the recovery hint and reset controls after the username step.
- PASS — Published EXE Login close terminated the application cleanly.
- NOT VERIFIED — Successful password recovery was not executed end-to-end through the Published EXE with a known recovery phrase. The available remote automation could not safely seed a controlled secret into the isolated PasswordBox test account without bypassing the security boundary. The same success path is covered by automated integration tests.

## 2. Decisions
- PASS — Recovery semantics were implemented rather than removed.
- PASS — No alternate authentication or hashing scheme was added.
- PASS — No schema migration was added.
- PASS — No Guest user or Guest permission was added.
- PASS — Existing UserRepository, AuditRepository, PasswordHasher and Login lifecycle were reused.
- PASS — Recovery remains a focused secondary flow and does not redesign Login.

## 3. Files Affected
- PASS — src/SmartLedger.Core/Abstractions/ISecurityContracts.cs
- PASS — src/SmartLedger.Core/Services/RecoveryPasswordPolicy.cs
- PASS — src/SmartLedger.Infrastructure/Data/UserRepository.cs
- PASS — src/SmartLedger.App/ViewModels/RecoveryViewModel.cs
- PASS — src/SmartLedger.App/Views/RecoveryWindow.xaml
- PASS — src/SmartLedger.App/Views/RecoveryWindow.xaml.cs
- PASS — src/SmartLedger.App/Views/LoginWindow.xaml
- PASS — src/SmartLedger.App/Views/LoginWindow.xaml.cs
- PASS — tests/SmartLedger.Tests/RecoveryTests.cs
- PASS — tests/SmartLedger.Tests/SmartLedger.Tests.csproj

## 4. Files Added
- PASS — RecoveryPasswordPolicy.cs
- PASS — RecoveryViewModel.cs
- PASS — RecoveryWindow.xaml
- PASS — RecoveryWindow.xaml.cs
- PASS — RecoveryTests.cs
- PASS — this batch report

## 5. Files Removed
- PASS — No product source file was removed in Batch 1.
- PASS — Guest UI remains absent.
- PASS — Temporary final-audit harness was removed after testing.

## 6. Files Refactored
- PASS — UserRepository gained recovery hint retrieval and password recovery mutation with mandatory audit.
- PASS — IUserRepository was extended consistently.
- PASS — Login gained the real Forgot Password action.
- PASS — Recovery validation is isolated in a small Core policy.
- PASS — Login/Recovery retain the existing dark SmartLedger RTL visual language.

## 7. Dependencies
- PASS — Existing PasswordHasher.
- PASS — Existing UserRepository.
- PASS — Existing AuditRepository.
- PASS — Existing App/Login lifecycle.
- PASS — No new NuGet package.
- PASS — No schema migration.
- PASS — No external service.

## 8. Risks
- NOT VERIFIED — Published-EXE successful recovery with a known secret.
- PASS — Recovery phrase and new password are never placed in successful recovery Audit details.
- PASS — Recovery requires an active user with a stored recovery hash.
- PASS — Recovery does not start a session automatically.
- PASS — Old password becomes invalid after successful recovery in automated verification.
- NOT VERIFIED — Rate limiting; no existing rate-limiting infrastructure was found or added in this batch.

## 9. Tests Added
1. PASS — Recovery_Success_ChangesPasswordAndAuditsWithoutSecrets
2. PASS — Recovery_WrongPhrase_DoesNotChangePassword
3. PASS — Recovery_UnknownUser_DoesNotRevealRecoveryHint
4. PASS — Recovery_PasswordConfirmationMismatch_IsRejectedBeforeHashing

## 10. Tests Passed
- PASS — 45/45.
- PASS — 0 failed.
- PASS — 0 skipped.
- PASS — Baseline was 41/41; four recovery/security tests were added.

## 11. Build Result
- PASS — Release Build.
- PASS — 0 warnings.
- PASS — 0 errors.
- PASS — net10.0-windows.

## 12. Regression Result
- PASS — All previous 41 tests remained passing.
- PASS — Four new recovery tests passed.
- PASS — Schema remains 6.
- PASS — Original DB SHA preserved: BEB84C83B2A2B906858F5096C856C3E16FEAFBB9EBB8CEC03B698815B288BE4C.

## 13. Runtime Evidence
- PASS — Published EXE only was used for runtime acceptance.
- PASS — Login Window rendered and responded.
- PASS — Forgot Password was invoked from the Published EXE.
- PASS — Recovery Window appeared.
- PASS — Recovery username step completed and recovery hint/reset controls appeared.
- PASS — Recovery Window closed cleanly.
- PASS — Login close terminated SmartLedger.App with no remaining process.
- PASS — Published runtime was responsive.
- NOT VERIFIED — Successful password reset through Published EXE with a known recovery phrase.
- NOT VERIFIED — startup-error.log evidence for this isolated run; the file was absent after restoring the isolated data directory, although no runtime exception was observed.

## 14. Visual Evidence
- PASS — Published control tree confirms Forgot Password is a compact secondary action below the password field and separate from the primary Login button.
- PASS — Recovery Window exposes title, username, recovery hint, recovery phrase, new password, confirmation, save, return and close actions.
- PASS — UI Automation bounds confirm primary Login and Forgot Password are separate targets.
- PASS — Arabic/RTL controls are present in the published UI.
- NOT VERIFIED — Screenshot-based forensic DPI review of Recovery.

## 15. Screen Acceptance Matrix
| Screen | Result | Evidence |
|---|---|---|
| Bootstrap | PASS | Existing recovery phrase/hint setup remains supported |
| Login | PASS | Published EXE opened; Forgot and Close executed |
| Recovery | PASS | Published EXE opened it; username step and hint/reset UI appeared |
| Main/Shell | NOT VERIFIED | Batch 1 scope |
| Dashboard | NOT VERIFIED | Batch 1 scope |
| Accounts/Books/Operations | NOT VERIFIED | Batch 1 scope |
| Sales/Purchases/Inventory | NOT VERIFIED | Batch 1 scope |
| Expenses/Employees | NOT VERIFIED | Batch 1 scope |
| Reports | NOT VERIFIED | Batch 1 scope |
| Users/Permissions | NOT VERIFIED | Batch 1 scope |
| Settings | NOT VERIFIED | Batch 1 scope |
| Messages/WhatsApp | NOT VERIFIED | Batch 1 scope |
| Backup/Restore | NOT VERIFIED | Batch 1 scope |
| Recycle Bin | NOT VERIFIED | Batch 1 scope |
| Audit Log | NOT VERIFIED | Batch 1 scope |

## 16. Button Audit
| Button | Screen | Class | Result |
|---|---|---|---|
| دخول | Login | A — أساسي | PASS — LoginCommand; IsDefault |
| نسيت كلمة المرور؟ | Login | B — ثانوي | PASS — opens RecoveryWindow in Published EXE |
| Eye | Login | B — ثانوي | PASS — existing TogglePassword_Click |
| × | Login | A — system | PASS — process exited cleanly |
| متابعة الاسترداد | Recovery | A — contextual | PASS — BeginRecoveryCommand executed |
| حفظ كلمة المرور الجديدة | Recovery | A — primary | PASS — ResetPasswordCommand; backend covered by tests |
| العودة إلى تسجيل الدخول | Recovery | B — secondary | PASS — BackToLoginCommand |
| × | Recovery | A — system | PASS — recovery dialog closed |
- PASS — No current-source Guest button remains.

## 17. Shortcut Audit
| Shortcut | Action | Screen | Execution | Conflict | Needed? |
|---|---|---|---|---|---|
| Enter | Default Login action | Login | PASS | None observed | PASS |
| F11 | Fullscreen toggle | Login | PASS | None observed | PASS |
| Escape | Exit fullscreen | Login | PASS | None observed | PASS |
| Ctrl+K | Global Search | Application | NOT VERIFIED | NOT VERIFIED | Deferred to Batch 11 |

## 18. Critical Workflow Results
### Recovery
- PASS — Login → Forgot Password.
- PASS — Username step.
- PASS — Recovery hint display.
- PASS — Recovery phrase verification in automated integration tests.
- PASS — Wrong phrase rejection in automated integration tests.
- PASS — Confirmation mismatch.
- PASS — New password hashing/persistence.
- PASS — New password immediately usable in automated verification.
- PASS — Secret-free audit.
- NOT VERIFIED — Successful recovery action in Published EXE.

### Login close
- PASS — Login → Close.
- PASS — Application process termination.

### Database safety
- PASS — Original DB restored.
- PASS — Original DB SHA preserved.
- PASS — Schema remains 6.

## 19. Remaining Work
- BLOCKED for Batch 2 acceptance — Obtain direct Published-EXE evidence of successful recovery using a controlled test account/recovery phrase without bypassing the PasswordBox security boundary.
- NOT VERIFIED — Normal credential login success on Published EXE in this batch.
- NOT VERIFIED — Login/Recovery DPI forensic review at 100/125/150/200%.
- NOT VERIFIED — Full application screen/button audit.
- NOT VERIFIED — Global Search and shortcut matrix.
- NOT VERIFIED — Critical commercial workflows A–E.
- NOT VERIFIED — Reports/PDF/Excel UI acceptance.
- NOT VERIFIED — Users/Permissions/Audit UI acceptance.
- NOT VERIFIED — Settings/Privacy/Messages/WhatsApp.
- NOT VERIFIED — Backup/Restore/Recycling UI acceptance.
- NOT VERIFIED — Performance measurements.
- NOT VERIFIED — Accessibility/DPI/RTL/encoding forensic pass across the full product.

### Batch gate
FAIL / BLOCKED for declaring the product commercially ready.
The recovery implementation is PASS by source + automated integration tests + Published-EXE navigation evidence, but end-to-end successful recovery from the Published EXE remains NOT VERIFIED. Do not treat Batch 2 or later product acceptance as complete until that evidence gap is closed.