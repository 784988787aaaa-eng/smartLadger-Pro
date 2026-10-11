$ErrorActionPreference='Stop'
$root='C:\Users\pc\SmartLedger-Build-Clean-20261010'
Set-Location -LiteralPath $root
$xamlPath=Join-Path $root 'src\SmartLedger.App\Views\ModuleTemplates.xaml'
$before=[IO.File]::ReadAllText($xamlPath)
$s=$before
$s=$s.Replace('<Border Width="820" MaxWidth="960" MaxHeight="900" HorizontalAlignment="Center" VerticalAlignment="Center" Style="{StaticResource DialogShell}" Padding="22">','<Border Width="1120" MaxWidth="1180" MaxHeight="940" HorizontalAlignment="Center" VerticalAlignment="Center" Style="{StaticResource DialogShell}" Padding="16">')
$prodAnchor=$s.IndexOf('AutomationProperties.AutomationId="Btn_CloseProductDialog"')
if($prodAnchor -lt 0){throw 'Product dialog anchor not found'}
$prodStart=$s.LastIndexOf('<Grid Style="{StaticResource DialogOverlay}"',$prodAnchor)
$prodEnd=$s.IndexOf('</Grid></Grid>', $prodAnchor)
if($prodStart -lt 0 -or $prodEnd -lt 0){throw 'Could not locate product dialog scope'}
$prodEnd += '</Grid></Grid>'.Length
$prod=$s.Substring($prodStart,$prodEnd-$prodStart)
$prod=$prod.Replace('Padding="16" Margin="0,0,0,12"','Padding="10" Margin="0,0,0,7"')
$prod=$prod.Replace('Margin="0,0,0,16"','Margin="0,0,0,8"')
$prod=$prod.Replace('Margin="0,4,0,0"','Margin="0,2,0,0"')
$prod=$prod.Replace('Height="44"','Height="36"')
$prod=$prod.Replace('Height="42"','Height="36"')
$prod=$prod.Replace('FontSize="22" FontWeight="SemiBold"','FontSize="20" FontWeight="SemiBold"')
$s=$s.Substring(0,$prodStart)+$prod+$s.Substring($prodEnd)
$pattern='<(TextBox|ComboBox)(\b[^>]*?)(/?>)'
$s=[regex]::Replace($s,$pattern,{
 param($m)
 $tag=$m.Groups[1].Value; $attrs=$m.Groups[2].Value; $end=$m.Groups[3].Value
 $isNumeric=$attrs -match '(?i)(Barcode|Sku|SKU|Phone|Amount|Balance|Quantity|Price|Cost|CreditLimit|OpeningBalance|Reorder|Stock|Factor|Code|Reference|Number|Tax|Discount|Profit|Margin|Payment)'
 $isArabicText=$attrs -match '(?i)(NewName|NewNote|Note|Address|Beneficiary|Recipient|Description|Category|PersonName|AccountName)'
 $flow=$null; $align=$null
 if($isNumeric){$flow='LeftToRight'; if($attrs -match '(?i)(Amount|Balance|Price|Cost|CreditLimit|OpeningBalance|Quantity|Stock|Tax|Discount|Profit|Margin)'){$align='Right'}else{$align='Left'}}
 elseif($isArabicText){$flow='RightToLeft';$align='Right'}
 if($flow){
   if($attrs -match '\bFlowDirection='){ $attrs=[regex]::Replace($attrs,'\bFlowDirection="[^"]*"','FlowDirection="'+$flow+'"',1) } else {$attrs+=' FlowDirection="'+$flow+'"'}
   if($attrs -match '\bTextAlignment='){ $attrs=[regex]::Replace($attrs,'\bTextAlignment="[^"]*"','TextAlignment="'+$align+'"',1) } elseif($tag -eq 'TextBox') {$attrs+=' TextAlignment="'+$align+'"'}
 }
 return '<'+$tag+$attrs+$end
})
$s=$s.Replace('Text="{Binding NewBarcode,UpdateSourceTrigger=PropertyChanged}" FlowDirection="LeftToRight"','Text="{Binding NewBarcode,UpdateSourceTrigger=PropertyChanged}" AutomationProperties.AutomationId="ProductBarcodeInput" FlowDirection="LeftToRight" TextAlignment="Left"')
$s=$s.Replace('Text="{Binding NewName,UpdateSourceTrigger=PropertyChanged}"','Text="{Binding NewName,UpdateSourceTrigger=PropertyChanged}" AutomationProperties.AutomationId="ProductNameInput" FlowDirection="RightToLeft" TextAlignment="Right"')
$s=$s.Replace('Text="{Binding AmountText,UpdateSourceTrigger=PropertyChanged}" Height="38" TextAlignment="Right" FlowDirection="LeftToRight"','Text="{Binding AmountText,UpdateSourceTrigger=PropertyChanged}" Height="42" FontSize="20" FontWeight="Bold" TextAlignment="Right" FlowDirection="LeftToRight"')
$s=$s.Replace('Content="��� �����" Command="{Binding SaveCommand}" Style="{StaticResource PrimaryButton}" Width="125"','Content="تسجيل سند الصرف" Command="{Binding SaveCommand}" Style="{StaticResource PrimaryButton}" Width="170"')
if($s -eq $before){throw 'No XAML changes were produced'}
[IO.File]::WriteAllText($xamlPath,$s,[Text.UTF8Encoding]::new($false))
$backupPath=Join-Path $root 'src\SmartLedger.Infrastructure\Backup\BackupService.cs'
$backupHash=(Get-FileHash -LiteralPath $backupPath -Algorithm SHA256).Hash
$reportPath=Join-Path $root 'DEEP_DECLUTTER_AND_REMEDIATION_REPORT.md'
$localOverrides=[regex]::Matches($s,'<(?:TextBox|ComboBox|DatePicker)\b[^>]*\sHeight="(?:42|44|46)"').Count
$report=@"
# SmartLedger — Deep De-cluttering and Input Ergonomics Remediation
Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
Workspace: $root

## In-place code changes
- Updated src/SmartLedger.App/Views/ModuleTemplates.xaml in place.
- Applied binding-semantic input direction rules: identifier/code/barcode/phone/numeric fields are LTR; monetary and quantity values are LTR with right alignment; Arabic name/note/category/address/description-bound fields are RTL and right-aligned.
- Added/retained stable AutomationIds for product barcode and name entry fields.
- Expanded the product dialog width from 820/960 to 1120/1180 and tightened card padding, vertical gaps, title size, and barcode/button heights to reduce vertical pressure while preserving existing bindings and commands.
- Increased expense amount prominence (42px, 20px bold, LTR/right-aligned) and clarified the submit button label to “تسجيل سند الصرف”.
- Preserved the existing scroll fallback for smaller display sizes instead of risking clipped fields/actions. A strict no-scroll guarantee is not claimed until live screenshot verification proves the complete form fits.
- Styles.xaml was not changed because field direction is semantic and should not be forced globally.

## Account dialog and statement grid
- Existing account opening balance and credit limit fields already use LTR numeric entry; semantic rules were normalized where the bound control could be identified.
- The account statement grid was not blindly replaced: multiple unrelated DataGrids exist, and no safely isolated account-transaction grid with the exact requested debit/credit/balance/action schema was established during this edit. The requested six-column statement layout remains a follow-up gap pending view-model/column mapping confirmation.
- A dedicated beneficiary property is not currently exposed by ExpensesViewModel; no misleading unpersisted recipient field was added. The voucher remains category, amount, date, payment method, and note until recipient persistence is designed end-to-end.

## Preservation
- BackupService.cs was not edited. SHA-256 after edits: $backupHash
- Existing application commands and data bindings were retained by the XAML transformation.

## Before/after metrics
- Product dialog outer width: 820px (max 960px) -> 1120px (max 1180px).
- Product card padding: 16px -> 10px; card bottom spacing: 12px -> 7px; title: 22px -> 20px; barcode/generator height: 44px -> 36px.
- Expense amount field: 38px / default typography -> 42px / 20px bold.
- Remaining local input height overrides 42/44/46 in TextBox/ComboBox/DatePicker tags: $localOverrides.

## Build, test, and runtime verification
Results are appended after running the required build, test suite, and live UI screenshot capture. No UX score above 9.5 is claimed without verified screenshots for the compact product form and account statement grid.
"@
[IO.File]::WriteAllText($reportPath,$report,[Text.UTF8Encoding]::new($false))
Write-Output "XAML remediation written. Product scope chars=$($prod.Length); remaining local height overrides=$localOverrides; BackupService SHA256=$backupHash"
