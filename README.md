# SmartLedger — دفتر أعمال ذكي

تطبيق مكتبي عربي لإدارة الحسابات والمعاملات والمبيعات والمشتريات والمخزون والمصروفات. يعمل على Windows، ويحفظ بياناته محليًا في SQLite مع دعم النسخ الاحتياطي والتصدير.

## المزايا

- إعداد أولي للمنشأة، وتسجيل الدخول، واستعادة الوصول، وإدارة المستخدمين والصلاحيات.
- دفاتر وحسابات مع أرصدة افتتاحية، وكشوف حساب، وخصوصية لإخفاء المبالغ.
- تسجيل العمليات ومتابعة المبيعات والمشتريات والمصروفات، مع تحديث الأرصدة والمخزون ضمن معاملات قاعدة بيانات.
- إدارة الأصناف والوحدات والتحويل بينها، وحركات المخزون والتنبيه عند انخفاضه.
- لوحة رئيسية، وبحث وتنقل واختصارات، وإشعارات وسجل تدقيق وسلة محذوفات قابلة للاستعادة.
- تقارير حسب الفترة، مع تصدير PDF وExcel.
- إعدادات المنشأة والنسخ الاحتياطي والاستعادة محليًا.

## التقنية

- C# و.NET 10، وWPF، وCommunityToolkit.Mvvm.
- SQLite عبر Microsoft.Data.Sqlite.
- فصل التطبيق إلى Core وInfrastructure وApp.

## التشغيل على Windows

يتطلب Windows و.NET 10 SDK. من جذر المشروع:

```powershell
dotnet restore SmartLedger.sln
dotnet build SmartLedger.sln -c Release --no-restore
dotnet run --project src/SmartLedger.App/SmartLedger.App.csproj
```

لبناء إصدار Windows x64 مستقل:

```powershell
dotnet publish src/SmartLedger.App/SmartLedger.App.csproj -c Release -p:PublishProfile=WinX64
```

ملفات النشر الناتجة موجودة تحت `src/SmartLedger.App/bin/Release/net10.0-windows/win-x64/publish`.

## البيانات والنسخ الاحتياطي

قاعدة البيانات محلية على الجهاز. تستخدم SQLite وضع WAL، ويُنشأ النسخ الاحتياطي عبر واجهة SQLite للنسخ الحي لتجنب نسخ قاعدة نشطة بصورة غير متسقة. يتحقق التطبيق من وجهة النسخ قبل الكتابة، ويعرض حالة العمليات للمستخدم.

## البنية

```text
src/SmartLedger.Core           النماذج والعقود ومنطق المجال
src/SmartLedger.Infrastructure قاعدة البيانات والتقارير والنسخ الاحتياطي
src/SmartLedger.App             واجهة WPF ونماذج العرض
tests/SmartLedger.Tests         اختبارات المجال والتكامل
```
