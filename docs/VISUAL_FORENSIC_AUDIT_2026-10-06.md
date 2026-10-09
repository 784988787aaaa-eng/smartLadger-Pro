# HISTORICAL / SUPERSEDED

هذه الوثيقة سجل تاريخي من مرحلة سابقة وليست دليل Release Acceptance الحالي. المرجع الحالي الوحيد هو docs/CURRENT_RELEASE_ACCEPTANCE_2026-10-07.md. لا تستخدم أرقام الاختبارات أو حالات Final الواردة هنا للحكم على الإصدار الحالي.

# SmartLedger — Direct Visual Forensic Audit
## 2026-10-06

## نطاق الفحص
تم فحص النسخة الوحيدة المعتمدة:
C:\Users\pc\Desktop\SmartLedger_Run_2026-10-05

تم تشغيل Release runtime والتقاط الشاشة الفعلية بدقة 1920x1080 عبر الجهاز المتصل، مع فحص UI Automation لمواضع عناصر التنقل والنوافذ.

## نتائج الفحص المرئي
### 1. Full Screen / DPI
- اكتشف الفحص أن التطبيق كان System-DPI-aware فقط (Awareness=1).
- هذا تسبب في اختلافات بين القياس المنطقي والفعلي عند ملء الشاشة، وظهر كقصّ في لقطات العرض ذات الـ125% DPI.
- تم إضافة app.manifest مع PerMonitorV2.
- تم تحديث FullScreen في MainWindow وLoginWindow ليستخدم Win32 SetWindowPos مع قياس DPI.
- UI Automation أكد وجود الشريط الجانبي فعلياً في الجهة اليمنى، مع عناصر التنقل عند X≈1646..1903 على الشاشة الفعلية؛ اللقطات السابقة ذات 1536px كانت تقص الجزء الأيمن فقط.

### 2. اتجاه التخطيط
- تم فصل اتجاه التخطيط عن RTL النصي:
  - Root layout: LeftToRight.
  - المحتوى العربي: RightToLeft.
  - Sidebar: عمود مستقل في اليمين.
- هذا يمنع انعكاس الأعمدة أو خروج القائمة الجانبية عن مساحة المحتوى.

### 3. Dialogs
- DialogOverlay موجود كطبقة فوق المحتوى ولا يضيف Row/Column جديدة، وبالتالي لا يدفع الجداول أو البطاقات عند فتح نافذة الإدخال.
- لقطة إضافة الحساب 28-add-account-dialog.png تؤكد أن النافذة تظهر فوق الصفحة مع تعتيم الخلفية وبحقول ثابتة.
- شكل نافذة إضافة الحساب أصبح: عنوان + وصف + حقول + فاصل + إجراءات ثابتة.

### 4. ComboBox
- الفحص كشف أن ComboBox الافتراضي كان يظهر أبيض في صفحات الحسابات والمبيعات والمشتريات.
- تم استبداله بقالب Dark ComboBox موحد مع Popup داكن وHover/Selected states.

### 5. Settings
- تمت إعادة ترتيب إعدادات الهوية في بطاقة احترافية متعددة الصفوف.
- تمت إضافة حقل العملة إلى الواجهة.
- تمت إضافة منطقة هوية المطور:
  م/ منصور قطينه للبرمجيات • 774004399

### 6. Users
- تم استبدال CheckBox العرضي في عمود الحالة بــ Status Pills:
  نشط / معطل.
- تمت إضافة عرض الدور كـ Pill.

## لقطات التدقيق
docs/visual-audit/2026-10-06/
- 25-dashboard-runtime.png
- 27-accounts-page-dialog.png
- 28-add-account-dialog.png
- 26-full-physical-screen.png
- 29-final-runtime-audit.png
- screens/operations.png
- screens/sales.png
- screens/purchases.png
- screens/inventory.png
- screens/expenses.png
- screens/reports.png
- screens/users.png
- screens/settings.png

## الاختبارات
Release Build:
0 Warning
0 Error

Automated tests:
12 Passed
0 Failed
0 Skipped

## ملاحظات العمل المتبقية
الفحص البصري أثبت أن النواة الحالية أصبحت متماسكة بصرياً، لكن ما زالت هناك أعمال UX عالية المستوى قبل إعلان المنتج النهائي:
- تطوير Reports إلى مركز تقارير كامل مع فلاتر الفترة وكشوف الحساب والتصدير.
- تطوير Users إلى إدارة مستخدمين/صلاحيات كاملة بدلاً من جدول العرض فقط.
- رفع كثافة وثراء صفحات Sales/Purchases/Operations.
- تحسين Settings لتشمل الشعار والهوية التجارية بصورة كاملة.
- المرور النهائي على حالات Empty/Loading/Error/Saving/Disabled.
- اختبار كل نافذة إدخال بالماوس ولوحة المفاتيح وبمقاسات DPI متعددة.

## معيار القبول
لا يعتمد القبول النهائي على نجاح البناء فقط؛ يجب أن تمر كل شاشة باختبار بصري فعلي وUI Automation واختبار إدخال وحفظ.
