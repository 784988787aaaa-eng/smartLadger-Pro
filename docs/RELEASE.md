# Release Checklist

- [x] .NET 10 targets.
- [x] Nullable + implicit usings + warnings as errors.
- [x] SQLite schema migrations through version 9.
- [x] WAL / FK / busy timeout.
- [x] PBKDF2 password hashing.
- [x] Backup ZIP + SHA-256 metadata + pre-restore emergency copy.
- [x] PDF service with A4/A5/Thermal 80/58 definitions.
- [x] Excel export with numeric amount cells and Arabic RTL worksheet setting.
- [x] Arabic amount-to-words engine.
- [x] WhatsApp URL encoding and post-commit gate.
- [x] Hardware identifier service.
- [x] Core unit tests and SQLite integration tests.

Current acceptance gate: see CURRENT_RELEASE_ACCEPTANCE_2026-10-07.md. The current gate is BLOCKED by Manual UI defect A-001; do not treat this checklist as commercial readiness.
