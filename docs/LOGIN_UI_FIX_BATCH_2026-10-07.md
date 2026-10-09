# SmartLedger — LOGIN UI FIX BATCH
## 2026-10-07

### Scope
Login UI only. No account logic, database schema, password hashing, or recovery storage was changed.

### Changes
- Removed the non-functional Guest login button and all current XAML GuestCommand references.
- Removed the visible Forgot Password element because source inspection found recovery fields (RecoveryHash/RecoveryHint) but no actual recovery command/window/service/path. No fake recovery flow was added.
- Rebalanced the login form after removing dead controls.
- Moved the Login close action into a dedicated top bar area, kept Windows-style close placement at the top-right, added a larger click target and AutomationProperties.Name, and kept the action separate from the primary Login button.
- Marked Login as the default button for Enter behavior.
- XAML/CS files remain UTF-8 BOM.

### Verification
- Release Build: PASS — 0 warnings / 0 errors.
- Tests: PASS — 41/41, 0 failed, 0 skipped.
- Publish: PASS — win-x64 self-contained.
- Release EXE SHA-256: 0B8837D0871EAB790E5AC92C8E6FCA1345FFC5B3214274198A1C59EDAC0E4A79
- Published EXE SHA-256: 0B8837D0871EAB790E5AC92C8E6FCA1345FFC5B3214274198A1C59EDAC0E4A79
- SHA match: PASS.
- Published EXE runtime: PASS — Login rendered, RTL/Arabic controls present, Guest/Forgot controls absent, Login Invoke produced the expected invalid-credentials error state, and Close Invoke terminated the application cleanly.
- Original local DB isolation: restored with exact SHA match BEB84C83B2A2B906858F5096C856C3E16FEAFBB9EBB8CEC03B698815B288BE4C.
- startup-error.log: unchanged at 2026-10-07 02:30:26 +03:00.

### Acceptance status
- A-001 Guest: CLOSED.
- Forgot Password: FAIL / NOT IMPLEMENTABLE IN THIS UI-ONLY BATCH — no actual recovery path exists in the current project. The dead UI element was removed instead of inventing recovery behavior.
- Close Button: PASS.
- Normal credential success: NOT VERIFIED in this run; the remote UI automation security layer blocks programmatic entry into password fields. Invalid-login/error behavior was verified on the Published EXE.

### Gate
NOT READY / BLOCKED for the requested complete Login acceptance because a real Forgot Password flow does not currently exist and normal credential success was not directly verified in this batch.
