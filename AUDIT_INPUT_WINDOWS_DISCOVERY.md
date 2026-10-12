# SmartLedger — Audit Input Windows Discovery
> **الحالة:** استكشاف ساكن أولي — غير مكتمل  
> **المستودع:** `784988787aaaa-eng/smartLadger-Pro`  
> **الفرع المستهدف:** `feature/unified-clean-build`  
> **قيد السلامة:** لم تُجرَ أي تعديلات على كود التطبيق أو الواجهات أو قاعدة البيانات ضمن هذا الاستكشاف. هذا المستند توثيق فقط.  
> **حدود الإثبات:** المعلومات أدناه مقتبسة من الملفات التي أمكن قراءتها مباشرةً عبر GitHub. لا يُدّعى أن جرد XAML أو الخدمات شامل؛ يلزم استكمال تعداد ملفات الشجرة ومراجعة الملفات ذات الصلة.

## 1. بنية المشروع والحزم

### المشاريع الظاهرة في الحل
من `SmartLedger.sln`:
- `src/SmartLedger.Core/SmartLedger.Core.csproj`
- `src/SmartLedger.Infrastructure/SmartLedger.Infrastructure.csproj`
- `src/SmartLedger.App/SmartLedger.App.csproj`
- `tests/SmartLedger.Tests/SmartLedger.Tests.csproj`

### مشروع الواجهة
- **الاسم:** `SmartLedger.App`
- **المسار:** `src/SmartLedger.App/SmartLedger.App.csproj`
- **الإطار المستهدف:** `net10.0-windows`
- **WPF:** مفعّل عبر `<UseWPF>true</UseWPF>`
- **Nullable / ImplicitUsings:** مفعّلان.
- **TreatWarningsAsErrors:** مفعّل.
- **MVVM package:** `CommunityToolkit.Mvvm 8.4.2`
- **Project references:** `SmartLedger.Core` و`SmartLedger.Infrastructure`.

### Core
- **المسار:** `src/SmartLedger.Core/SmartLedger.Core.csproj`
- **الإطار المستهدف:** `net10.0`
- **TreatWarningsAsErrors:** مفعّل.

### Infrastructure والحزم
- **المسار:** `src/SmartLedger.Infrastructure/SmartLedger.Infrastructure.csproj`
- **الإطار المستهدف:** `net10.0`
- **Project reference:** `SmartLedger.Core`
- **الحزم المثبتة مباشرةً في الملف:**
  - `Microsoft.Data.Sqlite 10.0.12`
  - `QuestPDF 2026.9.1`
  - `QRCoder 1.6.0`
  - `DocumentFormat.OpenXml 3.1.1`

**ملاحظة:** لا تثبت الملفات التي قُرئت حتى الآن وجود MaterialDesignInXaml أو MahApps.Metro أو HandyControl؛ لا ينبغي استنتاج غيابها نهائيًا قبل استكمال فحص جميع ملفات المشروع ومراجع الحزم.

## 2. محتوى ملف App.xaml

المسار: `src/SmartLedger.App/App.xaml`

```xml
<Application x:Class="SmartLedger.App.App" xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:vm="clr-namespace:SmartLedger.App.ViewModels" xmlns:local="clr-namespace:SmartLedger.App.Converters" ShutdownMode="OnMainWindowClose">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source="Themes/Colors.xaml"/>
        <ResourceDictionary Source="Themes/Typography.xaml"/>
        <ResourceDictionary Source="Themes/Styles.xaml"/>
        <ResourceDictionary Source="Themes/Iconography.xaml"/>
        <ResourceDictionary Source="Views/ModuleTemplates.xaml"/>
        <ResourceDictionary Source="Views/Modules/InventoryView.xaml"/>
        <ResourceDictionary Source="Views/Modules/UsersView.xaml"/>
        <ResourceDictionary Source="Views/Modules/ExpensesView.xaml"/>
        <ResourceDictionary Source="Views/Modules/AccountsView.xaml"/>
        <ResourceDictionary Source="Views/Modules/PurchasesView.xaml"/>
        <ResourceDictionary Source="Views/Modules/DashboardView.xaml"/>
        <ResourceDictionary Source="Views/Modules/SettingsView.xaml"/>
        <ResourceDictionary Source="Views/Modules/SalesView.xaml"/>
        <ResourceDictionary Source="Views/Modules/AuditLogView.xaml"/>
        <ResourceDictionary Source="Views/Modules/RecycleBinView.xaml"/>
        <ResourceDictionary Source="Views/Modules/ReportsView.xaml"/>
        <ResourceDictionary Source="Views/Modules/OperationsView.xaml"/>
        <ResourceDictionary Source="Views/Modules/AccountDialogControl.xaml"/>
        <ResourceDictionary Source="Views/Modules/ProductDialogControl.xaml"/>
        <ResourceDictionary Source="Views/Modules/ExpenseVoucherDialogControl.xaml"/>
        <ResourceDictionary Source="Resources/Strings.ar.xaml"/>
      </ResourceDictionary.MergedDictionaries>
      <local:InverseBooleanToVisibilityConverter x:Key="InverseBoolToVisibility"/><local:ActivePageBackgroundConverter x:Key="ActivePageBackgroundConverter"/>
      <SolidColorBrush x:Key="Bg" Color="#070B19"/><SolidColorBrush x:Key="Surface" Color="#0F172A"/><SolidColorBrush x:Key="Surface2" Color="#131E3D"/><SolidColorBrush x:Key="Line" Color="#263A5A"/><SolidColorBrush x:Key="Text" Color="#F8FAFC"/><SolidColorBrush x:Key="Muted" Color="#93A4C3"/><SolidColorBrush x:Key="Cyan" Color="#00B4D8"/><SolidColorBrush x:Key="Green" Color="#06D6A0"/><SolidColorBrush x:Key="Red" Color="#EF476F"/><SolidColorBrush x:Key="Yellow" Color="#FFD166"/>
    </ResourceDictionary>
  </Application.Resources>
</Application>
```

### موارد مشتركة ظاهرة في App.xaml
- `Themes/Colors.xaml`
- `Themes/Typography.xaml`
- `Themes/Styles.xaml`
- `Themes/Iconography.xaml`
- `Views/ModuleTemplates.xaml`
- موارد Views/Modules المدرجة في الكود أعلاه.
- `Resources/Strings.ar.xaml`

### اتجاه RTL والخطوط
- يحتوي `App.xaml` الذي قُرئ على موارد Typography، لكنه لا يحدد `FlowDirection` على عنصر Application نفسه.
- لا يمكن تحديد ما إذا كان RTL مضبوطًا على مستوى النوافذ أو عناصرها قبل فحص XAML لكل نافذة وموارد الأنماط.

## 3. جرد أولي لملفات XAML

**هذه قائمة الملفات المشار إليها صراحةً في App.xaml، وليست جردًا كاملًا لجميع ملفات XAML في المستودع.** صُنّفت بحسب اسم الملف فقط؛ لم تُراجع بعد جميع محتوياتها لتحديد كل مسارات الإدخال بدقة.

### وحدات الواجهة
- `src/SmartLedger.App/Views/Modules/InventoryView.xaml`
- `src/SmartLedger.App/Views/Modules/UsersView.xaml`
- `src/SmartLedger.App/Views/Modules/ExpensesView.xaml`
- `src/SmartLedger.App/Views/Modules/AccountsView.xaml`
- `src/SmartLedger.App/Views/Modules/PurchasesView.xaml`
- `src/SmartLedger.App/Views/Modules/DashboardView.xaml`
- `src/SmartLedger.App/Views/Modules/SettingsView.xaml`
- `src/SmartLedger.App/Views/Modules/SalesView.xaml`
- `src/SmartLedger.App/Views/Modules/AuditLogView.xaml`
- `src/SmartLedger.App/Views/Modules/RecycleBinView.xaml`
- `src/SmartLedger.App/Views/Modules/ReportsView.xaml`
- `src/SmartLedger.App/Views/Modules/OperationsView.xaml`

### عناصر حوار/إدخال ظاهرة بالاسم
- `src/SmartLedger.App/Views/Modules/AccountDialogControl.xaml`
- `src/SmartLedger.App/Views/Modules/ProductDialogControl.xaml`
- `src/SmartLedger.App/Views/Modules/ExpenseVoucherDialogControl.xaml`
- `src/SmartLedger.App/Views/ModuleTemplates.xaml`

### ملفات موارد التصميم
- `src/SmartLedger.App/Themes/Colors.xaml`
- `src/SmartLedger.App/Themes/Typography.xaml`
- `src/SmartLedger.App/Themes/Styles.xaml`
- `src/SmartLedger.App/Themes/Iconography.xaml`
- `src/SmartLedger.App/Resources/Strings.ar.xaml`

## 4. ViewModels وCode-Behind والتحقق

- مرجع الحزمة المؤكد في مشروع الواجهة: `CommunityToolkit.Mvvm 8.4.2`.
- يثبت ذلك وجود اعتماد على الحزمة، لكنه **لا يثبت وحده** أن كل ViewModel يستخدم المصدر المولّد أو سمات Toolkit.
- لم تُحسم بعد قائمة ملفات `*.xaml.cs` وViewModels المقابلة، ولا نمط التحقق المستخدم (`IDataErrorInfo` أو `INotifyDataErrorInfo` أو تحقق يدوي). تُترك هذه البنود غير محسومة حتى قراءة الملفات ذات الصلة مباشرةً.

## 5. الخدمات المالية والمخزنية

- المشروع المرشح لاحتواء التنفيذات والبنية التحتية هو `src/SmartLedger.Infrastructure`، وفق تقسيم الحل ومرجع المشروع من واجهة المستخدم.
- الحزمة `Microsoft.Data.Sqlite` موجودة في ملف Infrastructure.
- **لم يتم بعد إثبات أسماء ومسارات الخدمات المسؤولة عن كل عملية**، ولم تُربط النوافذ بخدمات الحفظ والترحيل أو بحركات المخزون. يلزم فحص ملفات المصدر الفعلية قبل توثيق أي ربط.

## 6. ملاحظة CI/CD مسجلة

المسار: `.github/workflows/build.yml` على الفرع المستهدف.

في محتوى الملف الذي تمت قراءته، أحداث الدفع تشمل `main` و`fix/**`، وأحداث `pull_request` تستهدف `main`. لا يظهر `feature/unified-clean-build` ضمن هذه القوائم. لا يُجرى أي تعديل على Workflow في هذه المرحلة.

## 7. الأعمال المتبقية لاستكمال سجل التدقيق

1. الحصول على قائمة شجرة المستودع كاملة على الفرع المستهدف، ثم حصر كل `.xaml` و`.xaml.cs` وViewModels والخدمات والاختبارات.
2. قراءة ملفات الأنماط والخطوط وتحديد نطاق تطبيق RTL فعليًا.
3. مطابقة كل نافذة إدخال مع ViewModel والأوامر والتحقق وخدمة الحفظ.
4. تتبع مسارات الفواتير والقيود والمخزون والتسعير في طبقات Core وInfrastructure.
5. إعداد مصفوفة التدقيق البصري والوظيفي والمحاسبي، وتصنيف المخاطر P0–P3.
6. إبقاء كل التعديلات على كود التطبيق وقاعدة البيانات مجمّدة حتى موافقة صريحة على خطة التنفيذ.

---
**مبدأ التوثيق:** البنود غير المقروءة مباشرةً تبقى «غير محسومة» ولا تُستبدل باستنتاجات افتراضية.
