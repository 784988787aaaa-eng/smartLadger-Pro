$ErrorActionPreference='Stop'; $root='C:\Users\pc\SmartLedger-Build-Clean-20261010'; $p=Join-Path $root 'src\SmartLedger.App\Views\ModuleTemplates.xaml'; $s=[IO.File]::ReadAllText($p); $utf8=New-Object System.Text.UTF8Encoding($false)
$mapJson=@'
[
{"label":"\u0627\u0644\u0628\u0627\u0631\u0643\u0648\u062f","key":"ColBarcode"},
{"label":"\u0627\u0633\u0645 \u0627\u0644\u0635\u0646\u0641","key":"ColProductName"},
{"label":"\u0627\u0644\u062a\u0635\u0646\u064a\u0641","key":"ColCategory"},
{"label":"\u0627\u0644\u0631\u0635\u064a\u062f \u0627\u0644\u0645\u062a\u0648\u0641\u0631","key":"ColStock"},
{"label":"\u0633\u0639\u0631 \u0627\u0644\u062a\u0643\u0644\u0641\u0629","key":"ColCost"},
{"label":"\u0633\u0639\u0631 \u0627\u0644\u062a\u062c\u0632\u0626\u0629","key":"ColRetail"},
{"label":"\u0633\u0639\u0631 \u0627\u0644\u062c\u0645\u0644\u0629","key":"ColWholesale"},
{"label":"\u0627\u0644\u062a\u0627\u0631\u064a\u062e \u0648\u0627\u0644\u0648\u0642\u062a","key":"ColDateTime"},
{"label":"\u0639\u0644\u064a\u0647 (\u0645\u062f\u064a\u0646)","key":"ColDebit"},
{"label":"\u0644\u0647 (\u062f\u0627\u0626\u0646)","key":"ColCredit"},
{"label":"\u0627\u0644\u0631\u0635\u064a\u062f \u0627\u0644\u062a\u0631\u0627\u0643\u0645\u064a","key":"ColRunningBalance"},
{"label":"\u0627\u0644\u0625\u062c\u0631\u0627\u0621\u0627\u062a","key":"ColActions"},
{"label":"\u0627\u0644\u0643\u0645\u064a\u0629","key":"ColQty"},
{"label":"\u0627\u0644\u0648\u062d\u062f\u0629","key":"ColUnit"},
{"label":"\u0633\u0639\u0631 \u0627\u0644\u0648\u062d\u062f\u0629","key":"ColUnitPrice"},
{"label":"\u0627\u0644\u062e\u0635\u0645","key":"LblDiscount"},
{"label":"\u0627\u0644\u0636\u0631\u064a\u0628\u0629","key":"LblTax"},
{"label":"\u0627\u0644\u0645\u062f\u0641\u0648\u0639","key":"LblPaid"},
{"label":"\u0627\u0644\u0645\u062a\u0628\u0642\u064a","key":"LblRemaining"},
{"label":"\u062a\u0639\u0644\u064a\u0642 \u0627\u0644\u0641\u0627\u062a\u0648\u0631\u0629","key":"BtnHoldInvoice"},
{"label":"\u0627\u0644\u0645\u0633\u062a\u062e\u062f\u0645","key":"ColActor"},
{"label":"\u0627\u0644\u062a\u0641\u0627\u0635\u064a\u0644","key":"ColDetails"}
]
'@ | ConvertFrom-Json
foreach($item in $mapJson){$e=[regex]::Escape($item.label);$key=$item.key;$s=[regex]::Replace($s,'Header="'+$e+'"','Header="{DynamicResource '+$key+'}"');$s=[regex]::Replace($s,'Text="'+$e+'"','Text="{DynamicResource '+$key+'}"')}; [IO.File]::WriteAllText($p,$s,$utf8); Write-Output 'DynamicResource bindings applied to mapped module labels.'
