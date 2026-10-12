# SmartLedger — Audit Input Windows Discovery

> **الحالة:** استكشاف ساكن موجّه، غير نهائي. **الفرع:** `feature/unified-clean-build`.
> **قيد:** لم يُعدّل كود التطبيق أو قاعدة البيانات أو Workflow. لم يُنفذ build/tests/runtime QA؛ هذه النتائج من قراءة الملفات على الفرع مباشرة.

## 1. الملفات التي تمت قراءتها

| الملف | Blob SHA |
|---|---|
| `src/SmartLedger.App/Views/Modules/AccountDialogControl.xaml` | `0f82c65ff62147196c84e733cf8b41cfedff25f0` |
| `src/SmartLedger.App/Views/Modules/ProductDialogControl.xaml` | `d4aee194accaac2390f8b4862b3d1ae3d81698a2` |
| `src/SmartLedger.App/Views/Modules/ExpenseVoucherDialogControl.xaml` | `afe66bb24c0914eb2bb495922f024f335d579bd8` |
| `src/SmartLedger.App/Views/Modules/SalesView.xaml` | `b33e538868cc2ab9e2f79cb5c54ff10560bf0309` |
| `src/SmartLedger.App/Themes/Styles.xaml` | `c098fd82aa988248a1d87eb40c7d7fde6e1751c7` |
| `src/SmartLedger.App/Themes/Typography.xaml` | `1462ed005a141756c323dfaea77943cd05121409` |
| `src/SmartLedger.App/Views/MainWindow.xaml` | `e5f48679ce4952a57a4da1134d45adfc963ba77e` |
| `src/SmartLedger.App/App.xaml` | `7b5dfd9761f49cd15788e195e2ec361ec30d14d7` |

تمت قراءة `PurchasesView.xaml` و`OperationsView.xaml` وViewModels ذات الصلة أيضًا؛ لم تُسجّل بصماتها في ملخص هذه الجولة.

## 2. البنية المعمارية: Dialog DataTemplates

الملفات الثلاثة التي تنتهي بـ `DialogControl.xaml` ليست `Window` أو `UserControl`. كل ملف هو `ResourceDictionary` ويعرّف `DataTemplate`:
- `AccountDialogControlTemplate`
- `ProductDialogControlTemplate`
- `ExpenseVoucherDialogTemplate`

وتستخدم القوالب `DialogOverlay` وخصائص مثل `IsDialogOpen` مع `BoolToVisibility`. هذا يثبت أنها قوالب تُعرض داخل واجهة أخرى، لا نوافذ مستقلة بذاتها. يلزم فحص `ModuleTemplates.xaml` و`MainWindow.xaml` وViewModels لتوثيق الاستضافة الكاملة وإدارة التركيز/الخلفية.

## 3. المجموعة (أ): قوالب الإدخال

### 3.1 الحساب — `AccountDialogControl.xaml`

- RTL صريح على Grid، و`InputDialogBehavior.IsEnabled`; TabNavigation دائري؛ F5 للحفظ وEscape للإغلاق.
- الحقول: اسم الحساب، الدفتر، الرصيد الافتتاحي، اتجاه الرصيد «له/عليه»، البيان، الهاتف، حد الائتمان، واتساب.
- الرصيد وحد الائتمان LTR مع محاذاة يمين؛ الهاتف LTR ومحاذاة يسار.
- زر حفظ وإغلاق مع حالة `IsSaving` تعطل الزر أثناء الحفظ، وزر إلغاء. لا يظهر زر «حفظ وجديد».
- ViewModel المرشح الذي عُثر عليه: `src/SmartLedger.App/ViewModels/AccountsViewModel.cs`. لم يُعثر على `AccountDialogControl.xaml.cs` بالمسار المتوقع؛ وهذا متسق مع ResourceDictionary/DataTemplate.

**ملاحظات:** الاسم معلّم بنجمة لكن يجب إثبات تحقق الاسم والتكرار والرصيد من مسار الحفظ. نصوص عديدة مكتوبة مباشرة في XAML، لذلك التعريب ليس مركزيًا بالكامل. إعدادات XAML لا تثبت نجاح Enter/Tab/IME وقت التشغيل.

### 3.2 الصنف — `ProductDialogControl.xaml`

- DataTemplate بعرض 880 وحد أقصى 920 وارتفاع 580 وحد أقصى 600، بثلاث بطاقات/أعمدة و`ClipToBounds="True"`.
- حقول الاسم والباركود والتصنيف والمخزن وموقعه والمورد والوحدات ومعامل التحويل والكميات الافتتاحية وحد التنبيه والملاحظة وأسعار الشراء/البيع وهوامش الربح.
- RTL على الحاوية؛ بعض حقول الباركود والأرقام والكميات والأسعار LTR. الأزرار: إلغاء، «حفظ ومتابعة» (`SaveAndNewProductCommand`) و«حفظ وإغلاق» (`SaveAndCloseProductCommand`).
- ViewModel: `src/SmartLedger.App/ViewModels/InventoryViewModel.cs`. توجد حسابات `CalculatedUnitCostMinor`, `ProfitPerUnit`, `ProfitMargin`, `NetOpeningSmallUnits`. دالة `ParseDecimal` تستخدم `decimal.TryParse` مع `CurrentCulture` وتعيد صفرًا عند فشل التحليل، وبعض الحسابات تطبق `Math.Max(1m, conversion)`.

**مخاطر للتدقيق:** إرجاع صفر عند فشل التحليل قد يخفي الفرق بين القيمة غير الصالحة والصفر؛ وقيد معامل التحويل إلى 1 أو أكثر يحتاج إثباته في التحقق ومسار الحفظ. الحاوية الثابتة مع `ClipToBounds` تستلزم اختبار دقات مختلفة. لم يُعثر على code-behind مستقل بالمسار المتوقع.

### 3.3 سند النفقة — `ExpenseVoucherDialogControl.xaml`

- DataTemplate بعرض 560: الفئة، المبلغ، التاريخ، مصدر الدفع، البيان؛ المبلغ بارز بحجم 40 وLRT/LTR رقمي، وخيارات الدفع Cash/Bank/Credit.
- حقل البيان عربي لكنه مضبوط على `FlowDirection="LeftToRight"`، وهو تفاوت يستحق التحقق البصري والسلوكي.
- أزرار حفظ وإلغاء؛ لا يظهر في القالب تعطيل صريح للحفظ أثناء الحفظ.
- ViewModel: `src/SmartLedger.App/ViewModels/ExpensesViewModel.cs`. `SaveAsync` يمنع التكرار عبر `IsSaving`، ويتحقق من مبلغ موجب عبر `NumericNormalizer.TryParseDecimal`، ثم يحفظ ويعيد تحميل البيانات. يستخدم `session.CurrentUser!.Id`؛ يجب إثبات أن الجلسة مهيأة قبل السماح بالحفظ. يعرض `ex.Message` في StatusText، ويلزم تقييم ملاءمة رسائل الاستثناء للمستخدم النهائي.

## 4. المجموعة (ب): الفواتير والعمليات

### 4.1 المبيعات — `SalesView.xaml` / `SalesViewModel.cs`

- القالب DataTemplate لـ `SalesViewModel` ويضم سجل الفواتير وحوار إدخال overlay؛ ليست عملية الإدخال محصورة في صفحة ثابتة.
- اختصارات F1–F6 وEscape وCtrl+S وCtrl+P.
- الحقول: باركود، عميل، صنف، كمية ووحدة، سعر، مدفوع، خصم، ضريبة %، بيان؛ جدول سطور مع حذف.
- طرق الدفع: نقدي، آجل، جزئي. مؤشرات: إجمالي السطور، الخصم، الضريبة، الصافي، المدفوع، المتبقي، رصيد العميل الحالي والمتوقع.
- أوامر مرئية للحفظ/الترحيل والطباعة، حفظ فقط، طباعة، واتساب، تعليق واستئناف فاتورة.
- ViewModel: `src/SmartLedger.App/ViewModels/SalesViewModel.cs`. يحسب `LinesTotal` و`DiscountAmountDisplay` و`TaxAmountDisplay` و`Remaining` و`ProjectedCustomerBalance`. يستدعي `CurrentDiscount` و`CurrentTax`؛ يجب فحص تعريفهما ومسار الترحيل قبل الجزم بحدود الخصم أو طريقة الضريبة.

### 4.2 المشتريات — `PurchasesView.xaml` / `PurchasesViewModel.cs`

- ViewModel يحوي `IsDialogOpen` وسطور الفاتورة والإجماليات والمتبقي، ويستخدم آلية تغيير الوحدة ومعامل التحويل.
- تغيير الوحدة قد يعيد حساب السعر من نسبة معامل الوحدة الحالي إلى السابق، مع تنسيق InvariantCulture.
- `ReverseInvoiceAsync` يطلب تأكيدًا، يستدعي `repo.ReverseInvoiceAsync`، ثم يعيد تحميل سجل الفواتير.

**مطلوب:** تدقيق قواعد الخصم/الضريبة/التقريب وتكلفة المخزون والسداد الجزئي، واختبارات تبديل الوحدة والقيم غير الصالحة. وجود أمر العكس لا يثبت وحده سلامة عكس المخزون والدفتر العام؛ يجب تتبع repository/services واختباراتها. تفاصيل XAML ومسار الحفظ لم تُستخرج كاملة في هذه الجولة.

### 4.3 العمليات — `OperationsView.xaml` / `OperationsViewModel.cs`

- قالب حركة داخل overlay؛ بحث حساب مع Popup اقتراحات؛ أنواع ظاهرة: استلام/قبض، سداد/صرف، دين جديد.
- RTL للحاوية، LTR للمبلغ، F5 وCtrl+S للحفظ وEscape للإغلاق.
- `SaveAsync` و`CanSave` يتحققان من مبلغ موجب صالح وحساب مختار، ويمنعان الإتاحة أثناء `IsBusy`.

**مطلوب:** إثبات اتجاه المدين/الدائن لكل نوع، ذرية الحفظ وسجل التدقيق، ومنع الترحيل المكرر. حقل البحث مضبوط LTR مع محاذاة يمين ويحتاج اختبارًا عمليًا.

## 5. المجموعة (ج): الأنماط والنافذة الجذرية

### Styles.xaml
توجد أنماط مشتركة مثل `Field`, `NumericField`, `DialogNumericField`, `DialogComboBox`, `PrimaryButton`, `GhostButton`, `DialogShell`, `DialogOverlay` وأنماط DataGrid. `NumericField` يضبط LTR ومحاذاة يمين، لكن بعض العناصر تتجاوز الأنماط بخصائص محلية. توجد حالة تركيز تغيّر لون الحد إلى لون التمييز؛ لم يُجر اختبار تباين أو فحص runtime، لذا لا تصنيف نهائي للتباين بعد.

### Typography.xaml
- `ArabicFont` يحدد خطًا مفضلًا مع بدائل.
- النمط العام لـ `TextBlock` يضبط `Language="en-US"`، بينما النافذة الرئيسية `ar-SA`; يستحق ذلك اختبار الأرقام والنصوص المختلطة.
- `MetricText` يضبط `FlowDirection="LeftToRight"` للأرقام؛ يجب اختبار العملة والفواصل العشرية مع النص العربي.

### MainWindow.xaml
المسار المكتشف هو `src/SmartLedger.App/Views/MainWindow.xaml`، وليس `src/SmartLedger.App/MainWindow.xaml`. النافذة تضبط `FlowDirection="RightToLeft"`, `Language="ar-SA"`, و`UseLayoutRounding` و`SnapsToDevicePixels`. أبعادها الابتدائية 1400×850 وحدها الأدنى 1100×700. هذا يثبت RTL على النافذة الجذرية لكنه لا يلغي إعادة ضبط الاتجاه على عناصر فرعية.

## 6. استنتاجات مشتركة ومخاطر أولية

1. **التعريب:** وجود `Strings.ar.xaml` لا يعني أن كل النصوص مترجمة من الموارد؛ القوالب أعلاه تحتوي نصوصًا ثابتة كثيرة.
2. **الأرقام:** `NumericNormalizer.TryParseDecimal` في بعض المسارات، مقابل `InventoryViewModel.ParseDecimal` الذي يعيد صفرًا عند الفشل. يجب اختبار النقطة/الفاصلة والثقافة الحالية.
3. **RTL/LTR:** التباين قد يكون مقصودًا للباركود والأرقام، لكن بعض الحقول النصية العربية مضبوطة LTR. يلزم حصر لكل حقل.
4. **القص والتخطيط:** حوار الصنف ذو حجم ثابت و`ClipToBounds`; يلزم runtime QA بدقات مختلفة.
5. **الحفظ المكرر:** توجد حماية صريحة في بعض ViewModels، لكن لم يثبت بعد أن كل مسارات الترحيل محمية على مستوى الخدمات/قاعدة البيانات.
6. **الجودة:** `TreatWarningsAsErrors` مفعّل وفق ملفات المشاريع التي فُحصت سابقًا، لكن هذه الجولة لم تنفذ بناءً أو اختبارًا، ولا تثبت خلو Binding warnings وقت التشغيل.

## 7. ViewModels المرتبطة التي عُثر عليها

- `src/SmartLedger.App/ViewModels/AccountsViewModel.cs`
- `src/SmartLedger.App/ViewModels/InventoryViewModel.cs`
- `src/SmartLedger.App/ViewModels/ExpensesViewModel.cs`
- `src/SmartLedger.App/ViewModels/SalesViewModel.cs`
- `src/SmartLedger.App/ViewModels/PurchasesViewModel.cs`
- `src/SmartLedger.App/ViewModels/OperationsViewModel.cs`

لم تُوجد ملفات code-behind مستقلة للقوالب الثلاثة بالمسارات المتوقعة؛ الملفات موارد XAML وليست UserControl.

## 8. ما يلزم قبل المخرجات النهائية

- قراءة `ModuleTemplates.xaml` و`InputDialogBehavior` لتوثيق الاستضافة وEnter/Tab والاقتراحات.
- تتبع أوامر الحفظ حتى repository/services وقاعدة البيانات، مع المعاملات ومنع التكرار وسجل التدقيق.
- فحص تعريفات `CurrentDiscount` و`CurrentTax` وحسابات التقريب والتكلفة والتحويل واختباراتها.
- حصر النصوص الثابتة وخصائص RTL/LTR ومصادر الموارد.
- إجراء build/tests وruntime screenshots في مرحلة لاحقة مصرح بها.
- بعدها إعداد Input Windows Audit Matrix وتصنيف P0–P3، وتقييم Design System، وخطة تنفيذ للموافقة.

## 9. القرار

**لا تعديل على التطبيق أو قاعدة البيانات.** هذا تحديث للاستكشاف الساكن فقط، ولا يعتمد أي إصلاح قبل اكتمال الأدلة وموافقة صريحة على خطة التنفيذ.
