# HISTORICAL / SUPERSEDED

هذه الوثيقة سجل تاريخي من مرحلة سابقة وليست دليل Release Acceptance الحالي. المرجع الحالي الوحيد هو docs/CURRENT_RELEASE_ACCEPTANCE_2026-10-07.md. لا تستخدم أرقام الاختبارات أو حالات Final الواردة هنا للحكم على الإصدار الحالي.

# SmartLedger — Engineering Hardening & Runtime Verification
## 2026-10-06

### نطاق التنفيذ
تم التدقيق والتنفيذ مباشرة على:
C:\Users\pc\Desktop\SmartLedger_Run_2026-10-05

والنسخة المنشورة:
publish\SmartLedger\SmartLedger.App.exe

### إصلاحات قاعدة البيانات والخدمات
- ترقية مخطط SQLite من schema 3 إلى schema 4.
- إضافة Migration V4 متوافقة مع قواعد البيانات القديمة.
- إصلاح الاسم القديم `OccrredAt` إلى `OccurredAt` في Operations وStockMovements وExpenses وAuditLogs.
- تفعيل WAL وforeign_keys وbusy_timeout كما هو قائم في طبقة Database.
- إضافة حواجز مرجعية قبل المعاملة الذرية للفواتير للحساب والمستخدم والصنف.
- الحفاظ على Atomic Invoice Transaction بحيث تتراجع الفاتورة والبنود وحركة المخزون والقيد معاً عند الفشل.

### إصلاحات الواجهة
- ضبط LoginWindow ليكون RTL فعلياً مع الحفاظ على ترتيب منطقة الهوية وبطاقة الدخول.
- تحسين حجم LoginWindow إلى 1120×700 مع حد أدنى 980×620 بدلاً من النافذة الضخمة السابقة.
- معالجة XamlParseException في Users: تم نقل InverseBoolToVisibility إلى ResourceDictionary الخاص بالقوالب حتى يكون StaticResource مرئياً أثناء تحميل القالب.
- MainWindow بقي على 1400×850 كمرجع، Min 1100×700، مع Sidebar 230/72.
- الحفاظ على DialogOverlay والـKeyboard-first وRTL والأرقام الرقمية كما هو مثبت في التدقيق السابق.

### الاختبارات
- Debug solution tests: historical result superseded.
- Release solution build: PASS — 0 warnings, 0 errors.
- Release tests: historical result superseded.
- Release publish win-x64: PASS.
- اختبار AtomicInvoiceTransaction للنجاح: PASS.
- اختبار rollback عند نقص المخزون: PASS.
- اختبار ترحيل LegacyOccrredAt: PASS.
- اختبار foreign_key_check: PASS بلا مخالفات.

### Runtime على الجهاز المتصل
- تشغيل النسخة المنشورة الجديدة: PASS.
- SmartLedger.App.exe: Responding=True.
- بعد تشغيل نظيف، لم يتم إنشاء startup-error.log.
- قاعدة المستخدم الفعلية:
  - user_version = 4.
  - Operations.OccurredAt موجود.
  - StockMovements.OccurredAt موجود.
  - AuditLogs.OccurredAt موجود.
  - foreign_key_check بلا نتائج.
  - Users = 1.
  - People = 7.
  - Products = 1.

### الحماية
تم حفظ نسخ رجوع قبل تعديل الملفات الحساسة:
- Database.cs.bak-20261006
- AppRepository.cs.bak-20261006
- LoginWindow.xaml.bak-20261006
- DatabaseIntegrationTests.cs.bak-20261006

### نتيجة القبول الحالية
البنية الحالية قابلة للبناء والنشر والتشغيل، ومشكلة OccrredAt تمت معالجتها بترحيل حقيقي لا بتعديل استعلام فقط، كما تم إغلاق عيب ResourceDictionary الذي كان يمكن أن يكسر نافذة المستخدمين وقت التشغيل.

### ملاحظة نطاق
التدقيق أثبت أن صفحات Dashboard/Accounts/Operations/Sales/Purchases/Inventory/Expenses/Reports/Users/Settings موجودة وتبني بنجاح. أما Messages وRecycle Bin كصفحات مستقلة فليسا جزءاً من AppPage الحالية، رغم وجود DeletedItems في المخطط؛ لذلك لا أعتبرهما «منفذين بالكامل» حتى يتم تأسيسهما فعلياً في طبقة Core/Infrastructure/App واختبارهما.
