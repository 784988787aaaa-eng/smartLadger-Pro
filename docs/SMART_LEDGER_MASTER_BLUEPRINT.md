# SmartLedger — Master Engineering Blueprint

## الحالة
نسخة تنفيذية تراكمية على الجهاز المتصل، مع SQLite Local-First وWPF RTL ونظام تصميم Midnight Navy.

## الحدود
- `SmartLedger.Core`: المجال والقواعد والعقود والمحركات المالية والمخزنية.
- `SmartLedger.Infrastructure`: SQLite، المعاملات، النسخ الاحتياطي، PDF، Excel، التدقيق، التكاملات.
- `SmartLedger.App`: WPF/MVVM، Shell، Login، Dashboard، الحسابات، العمليات، المخزون.
- `SmartLedger.Tests`: اختبارات النواة وقاعدة البيانات والمعاملات.

## الثوابت
- decimal لكل القيم المالية والكميات.
- SQLite WAL + Foreign Keys + busy timeout.
- Schema Version = 6 عبر `PRAGMA user_version`، مع Migration V4 لإصلاح `OccrredAt` إلى `OccurredAt` وMigration V5 لقوالب الرسائل وسلة المحذوفات.
- Password hashing = PBKDF2-SHA256 مع salt عشوائي.
- الخصوصية مركزية عبر `IPrivacyStateService`.
- الوحدات المركبة تحول إلى الوحدة الصغرى قبل التخزين.
- تحديث المخزون يتم داخل transaction مع رفض الرصيد السالب.

## التصميم
الخلفية `#070B19`، الأسطح `#0F172A` و`#131E3D`، التمييز `#00B4D8` و`#00D2D3`، لنا `#06D6A0`، علينا `#EF476F`، التنبيه `#FFD166`.

## معيار الجودة
لا اعتماد نهائي قبل نجاح restore + build + test، وتشغيل التطبيق فعلياً والتحقق من الاستجابة.
