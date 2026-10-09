# Backup / Restore

صيغة الملف: `SmartLedger_YYYY-MM-DD_HH-mm-ss.zip`.

المحتوى: `smartledger.db` + `metadata.json`، مع SHA-256 وschema version.

الاستعادة الآمنة:
1. إنشاء نسخة طوارئ من قاعدة البيانات الحالية.
2. فك الأرشيف إلى مجلد مؤقت.
3. التحقق من وجود قاعدة البيانات و`PRAGMA user_version`.
4. استبدال قاعدة البيانات فقط بعد نجاح التحقق.
5. عند الفشل يعاد ملف الطوارئ تلقائياً.


## Current release status — 2026-10-07
Schema source of truth: Database.cs, user_version 9. BackupService supports schema 9 and includes validated branding assets. Automated Backup/Restore hardening tests: 8/8 PASS. Manual Backup/Restore UI acceptance: NOT VERIFIED in the current batch because UI acceptance stopped at defect A-001. See CURRENT_RELEASE_ACCEPTANCE_2026-10-07.md.
