# HISTORICAL / SUPERSEDED

هذه الوثيقة سجل تاريخي من مرحلة سابقة وليست دليل Release Acceptance الحالي. المرجع الحالي الوحيد هو docs/CURRENT_RELEASE_ACCEPTANCE_2026-10-07.md. لا تستخدم أرقام الاختبارات أو حالات Final الواردة هنا للحكم على الإصدار الحالي.

# SmartLedger — Batch Finalization 2026-10-06

## تنفيذ مباشر
تم تنفيذ التعديلات على SmartLedger_Run_2026-10-05 ثم Release Publish وتشغيل النسخة المنشورة.

## UX / Shell
- إزالة عنوان "لوحة القيادة" من محتوى Dashboard.
- إزالة عناوين الصفحات المكررة داخل المحتوى، وأصبح عنوان الصفحة في TopBar فقط.
- نقل "عملية جديدة" إلى TopBar كإجراء رئيسي ثابت.
- Sidebar RTL: الأيقونة في موضع الـleading العربي قبل النص بصرياً، مع محاذاة RTL.
- إضافة رسائل وقوالب وسلة المحذوفات إلى التنقل.
- الإبقاء على Sidebar collapsed 72 / expanded 230.
- الحفاظ على Ctrl+K / Ctrl+N / Ctrl+S / F5 / Esc.

## Features / Services
- Schema V5.
- MessageTemplates table + seed templates + persistence/update.
- RecycleBin ViewModel/UI + list/restore/hard-delete repository operations.
- Migration V5 مستقلة حتى في قواعد جزئية لا تحتوي DeletedItems.
- Backup metadata/schema updated to 5، وRestore يقبل حتى V5.
- OccrredAt legacy migration remains covered.

## Verification
- Release Build: 0 warnings / 0 errors.
- Tests: historical result superseded.
- Atomic invoice success + rollback covered.
- Legacy OccrredAt migration covered.
- Message template persistence covered.
- RecycleBin restore payload covered.
- Release publish: successful.
- Runtime: SmartLedger.App.exe Responding=True.
- Clean startup: no startup-error.log.

## ملاحظة هندسية
تظل بعض العمليات الإدارية المتقدمة (صلاحيات granular، automatic backup scheduler، full audit UI، document center profiles) بحاجة إلى Batch مستقل إذا كان معيار القبول هو إغلاق كل بند في الـMaster Blueprint حرفياً، ولا ينبغي اعتبارها مكتملة لمجرد وجود طبقة أولية لها.
