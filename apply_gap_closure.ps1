$ErrorActionPreference='Stop'
$root='C:\Users\pc\SmartLedger-Build-Clean-20261010'; $utf8=New-Object System.Text.UTF8Encoding($false)
$p=Join-Path $root 'src\SmartLedger.Core\Abstractions\IAppRepository.cs'; $s=[IO.File]::ReadAllText($p); if($s -notmatch 'ReverseInvoiceAsync\(int invoiceId'){ $needle='    Task<Invoice> CreateInvoiceAsync(Invoice invoice, IReadOnlyList<InvoiceLine> lines, CancellationToken ct = default);'; $s=$s.Replace($needle,'    Task<bool> ReverseInvoiceAsync(int invoiceId, string reason, string userId);'+[Environment]::NewLine+$needle); [IO.File]::WriteAllText($p,$s,$utf8) }
$p=Join-Path $root 'src\SmartLedger.Infrastructure\Data\Database.cs'; $s=[IO.File]::ReadAllText($p); if($s -notmatch 'EnsureColumnAsync\(db,"Invoices","Status"'){ $needle='        await SetVersionAsync(db,12,ct);'; $replacement='        await EnsureColumnAsync(db,"Invoices","Status","TEXT NOT NULL DEFAULT ''Posted''",ct);'+[Environment]::NewLine+$needle; $s=$s.Replace($needle,$replacement); [IO.File]::WriteAllText($p,$s,$utf8) }
$p=Join-Path $root 'src\SmartLedger.Infrastructure\Data\AppRepository.cs'; $s=[IO.File]::ReadAllText($p)
if($s -notmatch 'public async Task<bool> ReverseInvoiceAsync\(int invoiceId') {
$method=@'
    public async Task<bool> ReverseInvoiceAsync(int invoiceId, string reason, string userId)
    {
        if (invoiceId <= 0) throw new ArgumentOutOfRangeException(nameof(invoiceId));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reversal reason is required.", nameof(reason));
        if (!Guid.TryParse(userId, out var actorId)) throw new ArgumentException("userId must be a valid user GUID.", nameof(userId));
        await EnsurePermissionAsync(PermissionKeys.Edit, CancellationToken.None);
        string invoiceKey, invoiceNumber; InvoiceType type; Guid? personId; decimal total, paid;
        var now = DateTimeOffset.UtcNow;
        await using (var db = database.Open())
        {
            await db.OpenAsync(); await using var tx = await db.BeginTransactionAsync(); await using var c = db.CreateCommand(); c.Transaction = (SqliteTransaction)tx;
            c.CommandText = "SELECT Id,COALESCE(InvoiceNumber,''),Type,PersonId,Total,Paid,Status FROM Invoices WHERE InvoiceNumber=$number LIMIT 1;"; P(c,"$number",invoiceId.ToString(CultureInfo.InvariantCulture)); string status;
            await using (var r = await c.ExecuteReaderAsync()) { if (!await r.ReadAsync()) { await tx.RollbackAsync(); return false; } invoiceKey=r.GetString(0); invoiceNumber=r.GetString(1); type=(InvoiceType)r.GetInt32(2); personId=r.IsDBNull(3)?null:Guid.Parse(r.GetString(3)); total=D(r.GetString(4)); paid=D(r.GetString(5)); status=r.GetString(6); }
            if (string.Equals(status,"Reversed",StringComparison.OrdinalIgnoreCase)||string.Equals(status,"Cancelled",StringComparison.OrdinalIgnoreCase)) { await tx.RollbackAsync(); return false; }
            var lines=new List<(string ProductId,decimal Qty,decimal Price)>(); c.Parameters.Clear(); c.CommandText="SELECT ProductId,QuantityMinor,UnitPrice FROM InvoiceLines WHERE InvoiceId=$id;"; P(c,"$id",invoiceKey); await using(var r=await c.ExecuteReaderAsync()) while(await r.ReadAsync()) lines.Add((r.GetString(0),D(r.GetString(1)),D(r.GetString(2))));
            foreach(var line in lines) { bool sale=type==InvoiceType.Sale||type==InvoiceType.SaleReturn; c.Parameters.Clear(); c.CommandText="SELECT CAST(StockSmallUnits AS REAL) FROM Products WHERE Id=$id;"; P(c,"$id",line.ProductId); var value=await c.ExecuteScalarAsync(); if(value is null||value is DBNull) throw new InvalidOperationException("Invoice references a missing product."); var next=Convert.ToDecimal(value,CultureInfo.InvariantCulture)+(sale?line.Qty:-line.Qty); if(next<0) throw new InvalidOperationException("Purchase reversal would make product stock negative."); c.Parameters.Clear(); c.CommandText="UPDATE Products SET StockSmallUnits=$stock WHERE Id=$id;"; P(c,"$stock",S(next)); P(c,"$id",line.ProductId); await c.ExecuteNonQueryAsync(); c.Parameters.Clear(); c.CommandText="INSERT INTO StockMovements(Id,ProductId,Type,QuantitySmallUnits,UnitPrice,Note,OccurredAt,CreatedBy,InvoiceId) VALUES($id,$product,$type,$qty,$price,$note,$when,$user,$invoice);"; P(c,"$id",Guid.NewGuid().ToString("N")); P(c,"$product",line.ProductId); P(c,"$type",(int)(sale?StockMovementType.ReturnIn:StockMovementType.AdjustmentOut)); P(c,"$qty",S(line.Qty)); P(c,"$price",S(line.Price)); P(c,"$note",$"Invoice reversal #{invoiceNumber}: {reason}"); P(c,"$when",now.ToString("O")); P(c,"$user",actorId.ToString("N")); P(c,"$invoice",invoiceKey); await c.ExecuteNonQueryAsync(); }
            if(personId is Guid person) { decimal amount=type==InvoiceType.Sale?Math.Max(0m,total-paid):total; if(amount>0) { c.Parameters.Clear(); c.CommandText="INSERT INTO Operations(Id,PersonId,Type,Amount,Note,OccurredAt,CreatedBy) VALUES($id,$person,$type,$amount,$note,$when,$user);"; P(c,"$id",Guid.NewGuid().ToString("N")); P(c,"$person",person.ToString("N")); P(c,"$type",(int)(type==InvoiceType.Sale?OperationType.Receive:OperationType.Payment)); P(c,"$amount",S(amount)); P(c,"$note",$"Reversal of invoice #{invoiceNumber}: {reason}"); P(c,"$when",now.ToString("O")); P(c,"$user",actorId.ToString("N")); await c.ExecuteNonQueryAsync(); } }
            if(type==InvoiceType.Sale&&paid>0) { c.Parameters.Clear(); c.CommandText="INSERT INTO CashRegisterEntries(Id,InvoiceId,Amount,EntryType,OccurredAt,CreatedBy) VALUES($id,$invoice,$amount,'Refund',$when,$user);"; P(c,"$id",Guid.NewGuid().ToString("N")); P(c,"$invoice",invoiceKey); P(c,"$amount",S(paid)); P(c,"$when",now.ToString("O")); P(c,"$user",actorId.ToString("N")); await c.ExecuteNonQueryAsync(); }
            c.Parameters.Clear(); c.CommandText="UPDATE Invoices SET Status='Reversed',Note=Note || $suffix WHERE Id=$id;"; P(c,"$suffix",$" [REVERSED: {reason}]"); P(c,"$id",invoiceKey); await c.ExecuteNonQueryAsync(); await tx.CommitAsync();
        }
        await audit.LogActionAsync(actorId,"INVOICE_REVERSED","Invoice",Guid.TryParse(invoiceKey,out var parsed)?parsed:null,new { InvoiceNumber=invoiceNumber,Reason=reason,Type=type.ToString(),Total=total,Paid=paid }); return true;
    }

'@
$idx=$s.IndexOf('    public async Task<Invoice> CreateInvoiceAsync('); if($idx -lt 0){throw 'Invoice insertion point not found'}; $s=$s.Insert($idx,$method); [IO.File]::WriteAllText($p,$s,$utf8)
}
Write-Output 'Core source files modified.'
