from pathlib import Path
import re, xml.etree.ElementTree as ET
root=Path(r'C:\Users\pc\SmartLedger-Build-Clean-20261010')
# Repository contract
p=root/'src/SmartLedger.Core/Abstractions/IAppRepository.cs'; s=p.read_text(encoding='utf-8')
needle='    Task<Invoice> CreateInvoiceAsync(Invoice invoice, IReadOnlyList<InvoiceLine> lines, CancellationToken ct = default);'
if 'Task<bool> ReverseInvoiceAsync(int invoiceId, string reason, string userId);' not in s:
    s=s.replace(needle, '    Task<bool> ReverseInvoiceAsync(int invoiceId, string reason, string userId);\n'+needle)
p.write_text(s,encoding='utf-8')
# Add persistent status column during latest migration
p=root/'src/SmartLedger.Infrastructure/Data/Database.cs'; s=p.read_text(encoding='utf-8')
needle='        await SetVersionAsync(db,12,ct);'
if 'EnsureColumnAsync(db,"Invoices","Status"' not in s:
    s=s.replace(needle, '        await EnsureColumnAsync(db,"Invoices","Status","TEXT NOT NULL DEFAULT \'Posted\'",ct);\n'+needle)
p.write_text(s,encoding='utf-8')
# Add transaction reversal implementation using schema names actually present in this repository
p=root/'src/SmartLedger.Infrastructure/Data/AppRepository.cs'; s=p.read_text(encoding='utf-8')
if 'Task<bool> ReverseInvoiceAsync(int invoiceId' not in s:
    marker='    public async Task<Invoice> CreateInvoiceAsync(Invoice i,IReadOnlyList<InvoiceLine> lines,CancellationToken ct=default)'
    method='''    public async Task<bool> ReverseInvoiceAsync(int invoiceId, string reason, string userId)
    {
        if (invoiceId <= 0) throw new ArgumentOutOfRangeException(nameof(invoiceId));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reversal reason is required.", nameof(reason));
        if (!Guid.TryParse(userId, out var actorId)) throw new ArgumentException("userId must be a valid user GUID.", nameof(userId));
        await EnsurePermissionAsync(PermissionKeys.Edit, CancellationToken.None);
        string invoiceKey; string invoiceNumber; InvoiceType type; Guid? personId; decimal total, paid; PaymentMode mode; string note; Guid createdBy;
        var now = DateTimeOffset.UtcNow;
        await using (var db = database.Open())
        {
            await db.OpenAsync();
            await using var tx = await db.BeginTransactionAsync();
            await using var c = db.CreateCommand(); c.Transaction = (SqliteTransaction)tx;
            c.CommandText = "SELECT Id,COALESCE(InvoiceNumber,''),Type,PersonId,Total,Paid,PaymentMode,Note,CreatedBy,Status FROM Invoices WHERE InvoiceNumber=$number LIMIT 1;";
            P(c,"$number",invoiceId.ToString(CultureInfo.InvariantCulture));
            string status;
            await using (var r = await c.ExecuteReaderAsync())
            {
                if (!await r.ReadAsync()) { await tx.RollbackAsync(); return false; }
                invoiceKey=r.GetString(0); invoiceNumber=r.GetString(1); type=(InvoiceType)r.GetInt32(2);
                personId=r.IsDBNull(3)?null:Guid.Parse(r.GetString(3)); total=D(r.GetString(4)); paid=D(r.GetString(5));
                mode=(PaymentMode)r.GetInt32(6); note=r.GetString(7); createdBy=Guid.Parse(r.GetString(8)); status=r.GetString(9);
            }
            if (string.Equals(status,"Reversed",StringComparison.OrdinalIgnoreCase) || string.Equals(status,"Cancelled",StringComparison.OrdinalIgnoreCase)) { await tx.RollbackAsync(); return false; }
            var lines = new List<(string ProductId, decimal Qty, decimal Price, string Description)>();
            c.Parameters.Clear(); c.CommandText="SELECT ProductId,QuantityMinor,UnitPrice,Description FROM InvoiceLines WHERE InvoiceId=$id;"; P(c,"$id",invoiceKey);
            await using (var r=await c.ExecuteReaderAsync()) while(await r.ReadAsync()) lines.Add((r.GetString(0),D(r.GetString(1)),D(r.GetString(2)),r.GetString(3)));
            foreach(var line in lines)
            {
                bool sale = type == InvoiceType.Sale || type == InvoiceType.SaleReturn;
                c.Parameters.Clear(); c.CommandText="SELECT CAST(StockSmallUnits AS REAL) FROM Products WHERE Id=$id;"; P(c,"$id",line.ProductId);
                var currentObj=await c.ExecuteScalarAsync(); if(currentObj is null || currentObj is DBNull) throw new InvalidOperationException("Invoice references a missing product.");
                var current=Convert.ToDecimal(currentObj,CultureInfo.InvariantCulture); var delta=sale?line.Qty:-line.Qty; var next=current+delta;
                if(next<0) throw new InvalidOperationException("Purchase reversal would make product stock negative.");
                c.Parameters.Clear(); c.CommandText="UPDATE Products SET StockSmallUnits=$stock WHERE Id=$id;"; P(c,"$stock",S(next)); P(c,"$id",line.ProductId); await c.ExecuteNonQueryAsync();
                c.Parameters.Clear(); c.CommandText="INSERT INTO StockMovements(Id,ProductId,Type,QuantitySmallUnits,UnitPrice,Note,OccurredAt,CreatedBy,InvoiceId) VALUES($id,$product,$type,$qty,$price,$note,$when,$user,$invoice);";
                P(c,"$id",Guid.NewGuid().ToString("N")); P(c,"$product",line.ProductId); P(c,"$type",(int)(sale?StockMovementType.ReturnIn:StockMovementType.AdjustmentOut)); P(c,"$qty",S(line.Qty)); P(c,"$price",S(line.Price)); P(c,"$note",$"Invoice reversal #{invoiceNumber}: {reason}"); P(c,"$when",now.ToString("O")); P(c,"$user",actorId.ToString("N")); P(c,"$invoice",invoiceKey); await c.ExecuteNonQueryAsync();
            }
            if(personId is Guid person)
            {
                decimal amount = type == InvoiceType.Sale ? total-paid : total;
                if(amount>0)
                {
                    var opType = type == InvoiceType.Sale ? OperationType.Receive : OperationType.Payment;
                    c.Parameters.Clear(); c.CommandText="INSERT INTO Operations(Id,PersonId,Type,Amount,Note,OccurredAt,CreatedBy) VALUES($id,$person,$type,$amount,$note,$when,$user);";
                    P(c,"$id",Guid.NewGuid().ToString("N")); P(c,"$person",person.ToString("N")); P(c,"$type",(int)opType); P(c,"$amount",S(amount)); P(c,"$note",$"Reversal of invoice #{invoiceNumber}: {reason}"); P(c,"$when",now.ToString("O")); P(c,"$user",actorId.ToString("N")); await c.ExecuteNonQueryAsync();
                }
            }
            if(type==InvoiceType.Sale && paid>0)
            {
                c.Parameters.Clear(); c.CommandText="INSERT INTO CashRegisterEntries(Id,InvoiceId,Amount,EntryType,OccurredAt,CreatedBy) VALUES($id,$invoice,$amount,'Refund',$when,$user);";
                P(c,"$id",Guid.NewGuid().ToString("N")); P(c,"$invoice",invoiceKey); P(c,"$amount",S(paid)); P(c,"$when",now.ToString("O")); P(c,"$user",actorId.ToString("N")); await c.ExecuteNonQueryAsync();
            }
            c.Parameters.Clear(); c.CommandText="UPDATE Invoices SET Status='Reversed',Note=Note || $suffix WHERE Id=$id;"; P(c,"$suffix",$" [REVERSED: {reason}]"); P(c,"$id",invoiceKey); await c.ExecuteNonQueryAsync();
            await tx.CommitAsync();
        }
        await audit.LogActionAsync(actorId,"INVOICE_REVERSED","Invoice",Guid.TryParse(invoiceKey,out var parsed)?parsed:null,new { InvoiceNumber=invoiceNumber, Reason=reason, Type=type.ToString(), Total=total, Paid=paid });
        return true;
    }

'''
    if marker not in s: raise SystemExit('Could not locate invoice create method insertion point')
    s=s.replace(marker,method+marker)
p.write_text(s,encoding='utf-8')
# Resource keys: ensure symmetric additions, preserve existing dictionary content
translations={
'NavAccounts':('الحسابات والعملاء','Accounts & Customers'),'NavInventory':('المخزن والمنتجات','Inventory & Products'),'NavPOS':('المبيعات ونقاط البيع','Sales & POS'),'NavPurchases':('المشتريات والتوريد','Purchases & Suppliers'),'NavExpenses':('المصروفات التشغيلية','Operating Expenses'),'NavAudit':('سجل النشاطات والتدقيق','Audit Log'),'NavSettings':('إعدادات النظام','System Settings'),'NavAbout':('عن النظام والترخيص','About & Licensing'),
'ColIndex':('م','No.'),'ColDateTime':('التاريخ والوقت','Date & Time'),'ColDocRef':('رقم السند / المرجع','Document / Reference'),'ColDescription':('البيان / الحركة','Description / Transaction'),'ColDebit':('عليه (مدين)','Debit'),'ColCredit':('له (دائن)','Credit'),'ColRunningBalance':('الرصيد التراكمي','Running Balance'),'ColActions':('الإجراءات','Actions'),'BtnSaveAndNew':('حفظ ومتابعة (Ctrl+Enter)','Save & Continue (Ctrl+Enter)'),'BtnSaveAndClose':('حفظ وإغلاق (Ctrl+S)','Save & Close (Ctrl+S)'),'BtnCancel':('إلغاء (Esc)','Cancel (Esc)'),
'ColBarcode':('الباركود','Barcode'),'ColProductName':('اسم الصنف','Product Name'),'ColCategory':('التصنيف','Category'),'ColStock':('الرصيد المتوفر','Available Stock'),'ColCost':('سعر التكلفة','Cost Price'),'ColRetail':('سعر التجزئة','Retail Price'),'ColWholesale':('سعر الجملة','Wholesale Price'),
'ColQty':('الكمية','Quantity'),'ColUnit':('الوحدة','Unit'),'ColUnitPrice':('سعر الوحدة','Unit Price'),'ColTotal':('الإجمالي','Total'),'LblSubtotal':('الإجمالي الفرعي','Subtotal'),'LblDiscount':('الخصم','Discount'),'LblTax':('الضريبة','Tax'),'LblNetTotal':('الصافي','Net Total'),'LblPaid':('المدفوع','Paid'),'LblRemaining':('المتبقي','Remaining'),'BtnSaveAndPrint':('حفظ وطباعة','Save & Print'),'BtnHoldInvoice':('تعليق الفاتورة','Hold Invoice'),'TxtBarcodeScan':('امسح الباركود أو أدخله','Scan or enter barcode'),'TxtSearchItem':('ابحث عن صنف','Search product'),'LblExpenseCategory':('فئة المصروف','Expense Category'),'LblExpenseAmount':('مبلغ المصروف','Expense Amount'),'ColActionType':('نوع الإجراء','Action Type'),'ColActor':('المستخدم','User'),'ColTimestamp':('التوقيت','Timestamp'),'ColDetails':('التفاصيل','Details')}
for lang,index in [('ar',0),('en',1)]:
 p=root/f'src/SmartLedger.App/Resources/Strings.{lang}.xaml'; s=p.read_text(encoding='utf-8')
 for key,values in translations.items():
  if re.search(r'x:Key="'+re.escape(key)+r'"',s): continue
  value=values[index].replace('&','&amp;')
  entry=f'  <system:String x:Key="{key}">{value}</system:String>'
  pos=s.rfind('</ResourceDictionary>')
  if pos<0: raise SystemExit(f'Invalid resource dictionary: {p}')
  s=s[:pos]+entry+'\n'+s[pos:]
 p.write_text(s,encoding='utf-8')
# Bind matching literal headers/TextBlocks across templates. Keep replacements narrow and XML-safe.
p=root/'src/SmartLedger.App/Views/ModuleTemplates.xaml'; s=p.read_text(encoding='utf-8')
# Header and Text attribute replacement only for exact known labels, including common older equivalents.
label_map={
'رقم السند / المرجع':'ColDocRef','البيان / الحركة':'ColDescription','عليه (مدين)':'ColDebit','له (دائن)':'ColCredit','الرصيد التراكمي':'ColRunningBalance','الإجراءات':'ColActions','التاريخ والوقت':'ColDateTime','الباركود':'ColBarcode','اسم الصنف':'ColProductName','التصنيف':'ColCategory','الرصيد المتوفر':'ColStock','سعر التكلفة':'ColCost','سعر التجزئة':'ColRetail','سعر الجملة':'ColWholesale','الكمية':'ColQty','الوحدة':'ColUnit','سعر الوحدة':'ColUnitPrice','الإجمالي':'ColTotal','الإجمالي الفرعي':'LblSubtotal','الخصم':'LblDiscount','الضريبة':'LblTax','الصافي':'LblNetTotal','المدفوع':'LblPaid','المتبقي':'LblRemaining','فئة المصروف':'LblExpenseCategory','مبلغ المصروف':'LblExpenseAmount','نوع الإجراء':'ColActionType','المستخدم':'ColActor','التوقيت':'ColTimestamp','التفاصيل':'ColDetails','الحسابات والعملاء':'NavAccounts','المخزن والمنتجات':'NavInventory','المبيعات ونقاط البيع':'NavPOS','المشتريات والتوريد':'NavPurchases','المصروفات التشغيلية':'NavExpenses','سجل النشاطات والتدقيق':'NavAudit','إعدادات النظام':'NavSettings','عن النظام والترخيص':'NavAbout','حفظ ومتابعة (Ctrl+Enter)':'BtnSaveAndNew','حفظ وإغلاق (Ctrl+S)':'BtnSaveAndClose','إلغاء (Esc)':'BtnCancel','حفظ وطباعة':'BtnSaveAndPrint','تعليق الفاتورة':'BtnHoldInvoice','امسح الباركود أو أدخله':'TxtBarcodeScan','ابحث عن صنف':'TxtSearchItem'}
for label,key in label_map.items():
 escaped=re.escape(label)
 s=re.sub(r'Header="'+escaped+r'"', 'Header="{DynamicResource '+key+'}"', s)
 s=re.sub(r'Text="'+escaped+r'"', 'Text="{DynamicResource '+key+'}"', s)
p.write_text(s,encoding='utf-8')
print('Applied repository API, invoice reversal transaction, status migration, symmetric resource keys and targeted DynamicResource bindings.')
