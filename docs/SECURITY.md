# Security

- كلمات المرور لا تحفظ كنص صريح؛ PBKDF2-SHA256 مع salt و250000 iteration.
- AuditRepository يحذف الحقول التي تحمل أسماء password/recovery/secret/token/privatekey قبل الإدراج.
- HWID مشتق من MachineName + ProcessorCount + OSVersion عبر SHA-256.
- قاعدة البيانات محلية، Foreign Keys مفعلة، والمعاملات تستخدم SQLite transactions.
- وضع الخصوصية يخفي القيم المالية بصرياً دون تغيير البيانات الأصلية.
- النسخ الاحتياطية تحتوي SHA-256 للملف وقبل الاستعادة يتم إنشاء نسخة طوارئ.
