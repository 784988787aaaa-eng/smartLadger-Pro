# Architecture

## Boundaries
- **Core**: models, contracts, security primitives, session. لا يعتمد على WPF أو SQLite.
- **Infrastructure**: SQLite repositories and local paths.
- **App**: WPF views, view-models, navigation and presentation resources.

## Rules
1. الواجهة لا تنفذ SQL.
2. قواعد البيانات خلف interfaces.
3. ViewModel لا يحتوي على عناصر WPF.
4. العمليات تحفظ كأحداث غير قابلة للتعديل تاريخيًا؛ التصحيح المستقبلي يضاف كتسوية/عملية عكسية.
5. المبالغ Decimal في النماذج وتخزين نصي مضبوط الثقافة في SQLite لتجنب أخطاء التقريب الثنائية.
6. كل شخص ينتمي إلى دفتر واحد.
7. «الكل» دفتر تجميعي غير قابل للحذف.
8. الحذف النهائي ليس المسار الافتراضي؛ سلة المحذوفات مخصصة للحذف القابل للاستعادة.

## Operation semantics
- ReceiveFromPerson: استلام من الشخص → يقلل لنا.
- DebtOnUs: دين علينا → يزيد علينا.
- Sale: بضاعة خرجت للشخص → تزيد لنا.
- Purchase: بضاعة دخلت من المورد → تزيد علينا.
- ReturnSale: مرتجع من الشخص → يقلل لنا.
- ReturnPurchase: مرتجع للمورد → يقلل علينا.
- Adjustment: مخصص للتسويات وسيأخذ قواعد صريحة قبل تفعيله في الواجهة.
