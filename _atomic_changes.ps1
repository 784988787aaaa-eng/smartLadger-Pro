$ErrorActionPreference='Stop'
Set-Location -LiteralPath 'C:\Users\pc\SmartLedger-Build-Clean-20261010'
$enc=[Text.UTF8Encoding]::new($false)
$p='src\SmartLedger.Core\Abstractions\IAppRepository.cs'
$s=[IO.File]::ReadAllText((Join-Path (Get-Location) $p))
$s=$s.Replace('Task<bool> ReverseInvoiceAsync(int invoiceId, string reason, string userId);','Task<bool> ReverseInvoiceAsync(int invoiceId, string reason, string userId, CancellationToken ct = default);')
[IO.File]::WriteAllText((Join-Path (Get-Location) $p),$s,$enc)
$p='src\SmartLedger.Infrastructure\Data\AppRepository.cs'
$s=[IO.File]::ReadAllText((Join-Path (Get-Location) $p))
$s=$s.Replace('public async Task<bool> ReverseInvoiceAsync(int invoiceId, string reason, string userId)','public async Task<bool> ReverseInvoiceAsync(int invoiceId, string reason, string userId, CancellationToken ct = default)')
$a=$s.IndexOf('public async Task<bool> ReverseInvoiceAsync('); $b=$s.IndexOf('public async Task<Invoice> CreateInvoiceAsync(',$a)
if($a -lt 0 -or $b -lt 0){throw 'Could not isolate reversal method'}
$m=$s.Substring($a,$b-$a)
$m=$m.Replace('await EnsurePermissionAsync(PermissionKeys.Edit, CancellationToken.None);','ct.ThrowIfCancellationRequested(); await EnsurePermissionAsync(PermissionKeys.Edit, ct);').Replace('await db.OpenAsync();','await db.OpenAsync(ct);').Replace('await db.BeginTransactionAsync();','await db.BeginTransactionAsync(ct);').Replace('await c.ExecuteReaderAsync()','await c.ExecuteReaderAsync(ct)').Replace('await r.ReadAsync()','await r.ReadAsync(ct)').Replace('await c.ExecuteScalarAsync()','await c.ExecuteScalarAsync(ct)').Replace('await c.ExecuteNonQueryAsync()','await c.ExecuteNonQueryAsync(ct)').Replace('await tx.RollbackAsync()','await tx.RollbackAsync(ct)').Replace('await tx.CommitAsync()','await tx.CommitAsync(ct)').Replace('new { InvoiceNumber=invoiceNumber,Reason=reason,Type=type.ToString(),Total=total,Paid=paid });','new { InvoiceNumber=invoiceNumber,Reason=reason,Type=type.ToString(),Total=total,Paid=paid },ct);')
$s=$s.Substring(0,$a)+$m+$s.Substring($b)
[IO.File]::WriteAllText((Join-Path (Get-Location) $p),$s,$enc)
$p='src\SmartLedger.App\Views\ModuleTemplates.xaml'
$s=[IO.File]::ReadAllText((Join-Path (Get-Location) $p))
$pairs=@(@('Header="الصنف"','Header="{DynamicResource ColProductName}"'),@('Header="السعر"','Header="{DynamicResource ColUnitPrice}"'),@('Header="الإجمالي"','Header="{DynamicResource ColTotal}"'),@('Header="حذف"','Header="{DynamicResource ColActions}"'),@('Header="التاريخ"','Header="{DynamicResource ColDateTime}"'),@('Header="البيان"','Header="{DynamicResource ColDescription}"'),@('Header="المبلغ"','Header="{DynamicResource Expenses.Amount}"'))
foreach($pair in $pairs){$s=$s.Replace($pair[0],$pair[1])}
[IO.File]::WriteAllText((Join-Path (Get-Location) $p),$s,$enc)
foreach($locale in @('ar','en')){
$p="src\SmartLedger.App\Resources\Strings.$locale.xaml"; $full=Join-Path (Get-Location) $p; $s=[IO.File]::ReadAllText($full)
if($locale -eq 'ar'){$items=@(@('ColProductName','الصنف'),@('ColUnitPrice','السعر'),@('ColTotal','الإجمالي'),@('ColActions','الإجراءات'),@('ColDateTime','التاريخ والوقت'),@('ColDescription','البيان'),@('Expenses.Amount','المبلغ'))}else{$items=@(@('ColProductName','Product'),@('ColUnitPrice','Unit price'),@('ColTotal','Total'),@('ColActions','Actions'),@('ColDateTime','Date / time'),@('ColDescription','Description'),@('Expenses.Amount','Amount'))}
foreach($item in $items){if($s -notmatch ('x:Key="'+[regex]::Escape($item[0])+'"')){$node='  <sys:String x:Key="'+$item[0]+'">'+$item[1]+'</sys:String>';$s=$s.Replace('</ResourceDictionary>',$node+[Environment]::NewLine+'</ResourceDictionary>')}}
[IO.File]::WriteAllText($full,$s,$enc)
}
$p='.gitignore';$full=Join-Path (Get-Location) $p;$s=[IO.File]::ReadAllText($full)
foreach($line in @('*.log','*.tmp','publish/')){if($s -notmatch ('(?m)^'+[regex]::Escape($line)+'$')){$s+=[Environment]::NewLine+$line}}
[IO.File]::WriteAllText($full,$s,$enc)
Get-ChildItem -File | Where-Object {$_.Extension -in '.log','.ps1','.py' -and $_.Name -notin @('README.md')} | Remove-Item -Force -ErrorAction SilentlyContinue
Write-Output 'EDITS_APPLIED'
