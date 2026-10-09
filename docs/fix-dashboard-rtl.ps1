$p='C:\Users\pc\Desktop\SmartLedger_Run_2026-10-05\src\SmartLedger.App\Views\ModuleTemplates.xaml'
$s=[System.IO.File]::ReadAllText($p)
$labels=@('قيمة بضائع المخزون','نبض نشاط اليوم','صافي المركز المالي','الإجراءات السريعة','أحدث العمليات','آخر القيود المسجلة في الدفاتر','النشاط اليومي','ملخص الحركة المالية اليوم','مبيعات اليوم','مشتريات اليوم','مصروفات اليوم','صافي الحركة التقديرية','بعد المشتريات والمصروفات','مؤشر السيولة السريعة','حركة نقدية متوقعة','مبني على نشاط اليوم')
foreach($label in $labels){
  $escaped=[regex]::Escape($label)
  $pattern='<TextBlock(?=[^>]*\bText="'+$escaped+'")'
  $s=[regex]::Replace($s,$pattern,{param($m) $tag=$m.Value; if($tag -match 'HorizontalAlignment='){$tag}else{$tag+' HorizontalAlignment="Stretch" TextAlignment="Right" FlowDirection="RightToLeft"'}},[System.Text.RegularExpressions.RegexOptions]::Singleline)
}
[System.IO.File]::WriteAllText($p,$s,[System.Text.UTF8Encoding]::new($false))
