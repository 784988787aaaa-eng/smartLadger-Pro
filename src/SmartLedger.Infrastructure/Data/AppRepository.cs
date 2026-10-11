using System.Globalization;
using Microsoft.Data.Sqlite;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Audit;

namespace SmartLedger.Infrastructure.Data;

public sealed class AppRepository(Database database, IAppSession? session = null) : IAppRepository
{
    private readonly IAppSession? appSession = session;
    private readonly AuditRepository audit = new AuditRepository(database);
    private readonly BalanceEngine balanceEngine = new BalanceEngine();
    private static string S(decimal v)=>v.ToString(CultureInfo.InvariantCulture);
    private static decimal D(string v)=>decimal.Parse(v,CultureInfo.InvariantCulture);
    private static void P(SqliteCommand c,string n,object v)=>c.Parameters.AddWithValue(n,v);
    private async Task EnsurePermissionAsync(string permission, CancellationToken ct)
    {
        if (appSession is null) return;
        if (appSession.CurrentUser is null) throw new UnauthorizedAccessException("لا توجد جلسة مستخدم صالحة.");
        if (!await new UserRepository(database).CanAsync(appSession.CurrentUser.Id, permission, ct))
            throw new UnauthorizedAccessException("لا تملك الصلاحية اللازمة لتنفيذ هذه العملية.");
    }

    public async Task<IReadOnlyList<Book>> GetBooksAsync(CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.View,ct);var x=new List<Book>();await using var db=database.Open();await db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="SELECT Id,Name,Kind,SortOrder,IsSystem,Color,Icon FROM Books ORDER BY SortOrder,Name;";await using var r=await c.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))x.Add(new Book(Guid.Parse(r.GetString(0)),r.GetString(1),(BookKind)r.GetInt32(2),r.GetInt32(3),r.GetBoolean(4),r.GetString(5),r.GetString(6)));return x;}
    public async Task<Book> CreateBookAsync(string name,string color,string icon,CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.Create,ct);var b=new Book(Guid.NewGuid(),name.Trim(),BookKind.Custom,100,false,color,icon);await using var db=database.Open();await db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="INSERT INTO Books VALUES($id,$name,$kind,$sort,0,$color,$icon);";P(c,"$id",b.Id.ToString("N"));P(c,"$name",b.Name);P(c,"$kind",(int)b.Kind);P(c,"$sort",b.SortOrder);P(c,"$color",b.Color);P(c,"$icon",b.Icon);await c.ExecuteNonQueryAsync(ct);await audit.LogActionAsync(appSession?.CurrentUser?.Id, "BOOK_CREATE", "Book", b.Id,new { b.Name },ct);return b;}
    public async Task RenameBookAsync(Guid id,string name,CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.Edit,ct);await using var db=database.Open();await db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="UPDATE Books SET Name=$n WHERE Id=$id AND IsSystem=0;";P(c,"$n",name.Trim());P(c,"$id",id.ToString("N"));await c.ExecuteNonQueryAsync(ct);}
    public async Task DeleteBookAsync(Guid id,bool deleteAccounts,CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.Delete,ct);await using var db=database.Open();await db.OpenAsync(ct);await using var tx=await db.BeginTransactionAsync(ct);await using var c=db.CreateCommand();c.Transaction=(SqliteTransaction)tx;P(c,"$id",id.ToString("N"));c.CommandText=deleteAccounts?"DELETE FROM Operations WHERE PersonId IN(SELECT Id FROM People WHERE BookId=$id);DELETE FROM People WHERE BookId=$id;DELETE FROM Books WHERE Id=$id AND IsSystem=0;":"UPDATE People SET BookId=(SELECT Id FROM Books WHERE Kind=0 LIMIT 1) WHERE BookId=$id;DELETE FROM Books WHERE Id=$id AND IsSystem=0;";await c.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);}
    public async Task<IReadOnlyList<PersonSummary>> GetPeopleAsync(Guid? bookId=null,string? search=null,int? take=null,CancellationToken ct=default)
    {
        var x=new List<PersonSummary>();
        await using var db=database.Open();
        await db.OpenAsync(ct);
        await using var c=db.CreateCommand();
        c.CommandText="""
SELECT p.Id,p.BookId,p.Name,p.Phone,p.Note,p.CreatedAt,b.Name,
COALESCE(SUM(CASE
    WHEN o.Type IN(3,4,7) THEN CAST(o.Amount AS REAL)
    WHEN o.Type IN(0,2,5) THEN -CAST(o.Amount AS REAL)
    WHEN o.Type=1 THEN CAST(o.Amount AS REAL)
    ELSE 0
END),0),
MAX(o.OccurredAt),p.AccountCode,p.CreditLimit,p.Address,p.SecondaryPhone,p.AccountNature,p.WhatsAppEnabled,p.UpdatedAt
FROM People p
JOIN Books b ON b.Id=p.BookId
LEFT JOIN Operations o ON o.PersonId=p.Id
WHERE($book IS NULL OR p.BookId=$book OR EXISTS(SELECT 1 FROM Books ab WHERE ab.Id=$book AND ab.Kind=0))
  AND($search='' OR p.Name LIKE '%'||$search||'%' OR p.Phone LIKE '%'||$search||'%')
GROUP BY p.Id
ORDER BY CASE WHEN $search='' THEN p.Name ELSE CASE WHEN p.Name LIKE $search||'%' THEN 0 ELSE 1 END END, p.Name
LIMIT COALESCE($take,-1);
""";
        P(c,"$book",bookId?.ToString("N")??(object)DBNull.Value);
        P(c,"$search",(search??"").Trim());
        P(c,"$take",take.HasValue?take.Value:(object)DBNull.Value);
        await using var r=await c.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct))
        {
            var netBalance=r.GetDecimal(7);
            var split=balanceEngine.Split(netBalance);
            x.Add(new PersonSummary(
                new Person(Guid.Parse(r.GetString(0)),Guid.Parse(r.GetString(1)),r.GetString(2),r.GetString(3),r.GetString(4),DateTimeOffset.Parse(r.GetString(5)))
                {
                    AccountCode=r.IsDBNull(9) ? string.Empty : r.GetString(9),
                    CreditLimit=r.IsDBNull(10) ? null : decimal.Parse(r.GetString(10),CultureInfo.InvariantCulture),
                    Address=r.IsDBNull(11) ? string.Empty : r.GetString(11),
                    SecondaryPhone=r.IsDBNull(12) ? string.Empty : r.GetString(12),
                    Nature=Enum.TryParse<AccountNature>(r.IsDBNull(13) ? string.Empty : r.GetString(13),true,out var nature) ? nature : AccountNature.Flexible,
                    WhatsAppEnabled=!r.IsDBNull(14) && (r.GetValue(14) is long enabled ? enabled!=0 : Convert.ToInt32(r.GetValue(14),CultureInfo.InvariantCulture)!=0),
                    UpdatedAt=r.IsDBNull(15) || string.IsNullOrWhiteSpace(r.GetString(15)) ? DateTimeOffset.Parse(r.GetString(5),CultureInfo.InvariantCulture) : DateTimeOffset.Parse(r.GetString(15),CultureInfo.InvariantCulture)
                },
                r.GetString(6),
                split.OwedToUs,
                split.WeOwe,
                netBalance,
                r.IsDBNull(8)?null:DateTimeOffset.Parse(r.GetString(8))));
        }
        return x;
    }
    private static void ValidatePersonInput(Person person)
    {
        if (string.IsNullOrWhiteSpace(person.Name) || person.Name.Trim().Length < 2)
            throw new ArgumentException("اسم الحساب مطلوب ويجب ألا يقل عن حرفين.", nameof(person));
        if (person.CreditLimit is <= 0)
            throw new ArgumentOutOfRangeException(nameof(person), "حد الائتمان يجب أن يكون موجباً أو فارغاً.");
        foreach (var phone in new[] { person.Phone, person.SecondaryPhone })
        {
            if (string.IsNullOrWhiteSpace(phone)) continue;
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length is < 7 or > 15 || phone.Any(ch => !char.IsDigit(ch) && ch is not ('+' or ' ' or '-' or '(' or ')' or '.')))
                throw new ArgumentException("رقم الهاتف غير صالح؛ استخدم 7 إلى 15 رقماً مع بادئة دولية اختيارية.", nameof(person));
        }
    }

    public async Task<Person> CreatePersonAsync(Person person, CancellationToken ct=default)
    {
        ArgumentNullException.ThrowIfNull(person);
        await EnsurePermissionAsync(PermissionKeys.Create, ct);
        ValidatePersonInput(person);
        var now = DateTimeOffset.UtcNow;
        var code = string.IsNullOrWhiteSpace(person.AccountCode)
            ? "ACC-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()
            : person.AccountCode.Trim();
        var p = person with { Name=person.Name.Trim(), Phone=person.Phone.Trim(), Note=person.Note.Trim(),
            AccountCode=code, UpdatedAt=now };
        await using var db=database.Open();
        await db.OpenAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        await using var c=db.CreateCommand();
        c.Transaction=(SqliteTransaction)tx;
        if(!await ExistsAsync(c,"Books",p.BookId,ct)) throw new InvalidOperationException("الدفتر المحدد غير موجود.");
        c.Parameters.Clear();
        c.CommandText="SELECT COUNT(*) FROM People WHERE AccountCode=$code;";
        P(c,"$code",p.AccountCode);
        if(Convert.ToInt64(await c.ExecuteScalarAsync(ct),CultureInfo.InvariantCulture)>0)
            throw new InvalidOperationException("رمز الحساب مستخدم بالفعل.");
        c.Parameters.Clear();
        c.CommandText="INSERT INTO People(Id,BookId,Name,Phone,Note,CreatedAt,AccountCode,CreditLimit,Address,SecondaryPhone,AccountNature,WhatsAppEnabled,UpdatedAt) VALUES($id,$book,$name,$phone,$note,$created,$code,$limit,$address,$secondary,$nature,$whatsapp,$updated);";
        P(c,"$id",p.Id.ToString("N")); P(c,"$book",p.BookId.ToString("N")); P(c,"$name",p.Name);
        P(c,"$phone",p.Phone); P(c,"$note",p.Note); P(c,"$created",p.CreatedAt.ToString("O",CultureInfo.InvariantCulture));
        P(c,"$code",p.AccountCode); P(c,"$limit",p.CreditLimit is null ? DBNull.Value : S(p.CreditLimit.Value));
        P(c,"$address",p.Address.Trim()); P(c,"$secondary",p.SecondaryPhone.Trim()); P(c,"$nature",p.Nature.ToString());
        P(c,"$whatsapp",p.WhatsAppEnabled?1:0); P(c,"$updated",p.UpdatedAt.ToString("O",CultureInfo.InvariantCulture));
        await c.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return p;
    }

    public async Task UpdatePersonAsync(Person person, CancellationToken ct=default)
    {
        ArgumentNullException.ThrowIfNull(person);
        await EnsurePermissionAsync(PermissionKeys.Edit, ct);
        ValidatePersonInput(person);
        var code = string.IsNullOrWhiteSpace(person.AccountCode)
            ? throw new ArgumentException("رمز الحساب مطلوب عند التعديل.", nameof(person))
            : person.AccountCode.Trim();
        await using var db=database.Open();
        await db.OpenAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        await using var c=db.CreateCommand();
        c.Transaction=(SqliteTransaction)tx;
        c.CommandText="SELECT COUNT(*) FROM People WHERE AccountCode=$code AND Id<>$id;";
        P(c,"$code",code); P(c,"$id",person.Id.ToString("N"));
        if(Convert.ToInt64(await c.ExecuteScalarAsync(ct),CultureInfo.InvariantCulture)>0)
            throw new InvalidOperationException("رمز الحساب مستخدم بالفعل.");
        c.Parameters.Clear();
        c.CommandText="UPDATE People SET Name=$name,Phone=$phone,SecondaryPhone=$secondary,Address=$address,CreditLimit=$limit,AccountNature=$nature,WhatsAppEnabled=$whatsapp,Note=$note,AccountCode=$code,UpdatedAt=$updated WHERE Id=$id;";
        P(c,"$id",person.Id.ToString("N")); P(c,"$name",person.Name.Trim()); P(c,"$phone",person.Phone.Trim());
        P(c,"$secondary",person.SecondaryPhone.Trim()); P(c,"$address",person.Address.Trim());
        P(c,"$limit",person.CreditLimit is null ? DBNull.Value : S(person.CreditLimit.Value));
        P(c,"$nature",person.Nature.ToString()); P(c,"$whatsapp",person.WhatsAppEnabled?1:0);
        P(c,"$note",person.Note.Trim()); P(c,"$code",code); P(c,"$updated",DateTimeOffset.UtcNow.ToString("O",CultureInfo.InvariantCulture));
        if(await c.ExecuteNonQueryAsync(ct)==0) throw new InvalidOperationException("الحساب غير موجود.");
        await tx.CommitAsync(ct);
        await audit.LogActionAsync(appSession?.CurrentUser?.Id,"PERSON_UPDATE","Person",person.Id,new { person.Name, person.Phone, person.AccountCode },ct);
    }

    public async Task<Person> CreatePersonWithOpeningBalanceAsync(Person person, decimal openingBalance, OperationType openingDirection, CancellationToken ct=default)
    {
        ArgumentNullException.ThrowIfNull(person);
        await EnsurePermissionAsync(PermissionKeys.Create,ct);
        ValidatePersonInput(person);
        if(openingBalance<0) throw new ArgumentOutOfRangeException(nameof(openingBalance),"الرصيد الافتتاحي لا يمكن أن يكون سالباً.");
        if(openingBalance>0 && openingDirection is not (OperationType.DebtToUs or OperationType.DebtOnUs))
            throw new ArgumentException("اتجاه الرصيد الافتتاحي غير صالح.",nameof(openingDirection));
        var now=DateTimeOffset.UtcNow;
        var code=string.IsNullOrWhiteSpace(person.AccountCode) ? "ACC-"+Guid.NewGuid().ToString("N")[..12].ToUpperInvariant() : person.AccountCode.Trim();
        var p=person with { Name=person.Name.Trim(),Phone=person.Phone.Trim(),Note=person.Note.Trim(),AccountCode=code,UpdatedAt=now };
        var userId=appSession?.CurrentUser?.Id ?? Guid.Empty;
        if(openingBalance>0 && userId==Guid.Empty) throw new InvalidOperationException("لا توجد جلسة مستخدم صالحة لتسجيل الرصيد الافتتاحي.");
        await using var db=database.Open();
        await db.OpenAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        await using var c=db.CreateCommand();
        c.Transaction=(SqliteTransaction)tx;
        if(!await ExistsAsync(c,"Books",p.BookId,ct)) throw new InvalidOperationException("الدفتر المحدد غير موجود.");
        c.Parameters.Clear();
        c.CommandText="SELECT COUNT(*) FROM People WHERE AccountCode=$code;";
        P(c,"$code",p.AccountCode);
        if(Convert.ToInt64(await c.ExecuteScalarAsync(ct),CultureInfo.InvariantCulture)>0) throw new InvalidOperationException("رمز الحساب مستخدم بالفعل.");
        if(openingBalance>0 && !await ExistsAsync(c,"Users",userId,ct)) throw new InvalidOperationException("المستخدم المنفذ للعملية غير موجود.");
        c.Parameters.Clear();
        c.CommandText="INSERT INTO People(Id,BookId,Name,Phone,Note,CreatedAt,AccountCode,CreditLimit,Address,SecondaryPhone,AccountNature,WhatsAppEnabled,UpdatedAt) VALUES($id,$book,$name,$phone,$note,$created,$code,$limit,$address,$secondary,$nature,$whatsapp,$updated);";
        P(c,"$id",p.Id.ToString("N")); P(c,"$book",p.BookId.ToString("N")); P(c,"$name",p.Name);
        P(c,"$phone",p.Phone); P(c,"$note",p.Note); P(c,"$created",p.CreatedAt.ToString("O",CultureInfo.InvariantCulture));
        P(c,"$code",p.AccountCode); P(c,"$limit",p.CreditLimit is null ? DBNull.Value : S(p.CreditLimit.Value));
        P(c,"$address",p.Address.Trim()); P(c,"$secondary",p.SecondaryPhone.Trim()); P(c,"$nature",p.Nature.ToString());
        P(c,"$whatsapp",p.WhatsAppEnabled?1:0); P(c,"$updated",p.UpdatedAt.ToString("O",CultureInfo.InvariantCulture));
        await c.ExecuteNonQueryAsync(ct);
        if(openingBalance>0)
        {
            c.Parameters.Clear();
            c.CommandText="INSERT INTO Operations(Id,PersonId,Type,Amount,Note,OccurredAt,CreatedBy,ReferenceNumber) VALUES($id,$person,$type,$amount,$note,$when,$user,$reference);";
            P(c,"$id",Guid.NewGuid().ToString("N")); P(c,"$person",p.Id.ToString("N")); P(c,"$type",(int)openingDirection);
            P(c,"$amount",S(decimal.Round(openingBalance,2,MidpointRounding.AwayFromZero))); P(c,"$note","رصيد افتتاحي");
            P(c,"$when",DateTimeOffset.UtcNow.ToString("O",CultureInfo.InvariantCulture)); P(c,"$user",userId.ToString("N")); P(c,"$reference",DBNull.Value);
            await c.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        if (openingBalance > 0)
        {
            await audit.LogActionAsync(userId, "MANUAL_BALANCE_ADJUSTMENT", "Person", p.Id,
                new { PersonName = p.Name, OpeningBalance = openingBalance, Direction = openingDirection.ToString() }, ct);
            await audit.LogActionAsync(userId, "BALANCE_ADJUSTED", "Person", p.Id,
                new { PersonName = p.Name, OpeningBalance = openingBalance, Direction = openingDirection.ToString() }, ct);
        }
        else
            await audit.LogActionAsync(userId, "PERSON_CREATE", "Person", p.Id, new { PersonName = p.Name }, ct);
        return p;
    }

    public async Task<IReadOnlyDictionary<Guid,string>> GetPersonNamesAsync(CancellationToken ct=default)
    {
        await EnsurePermissionAsync(PermissionKeys.View,ct);
        var x=new Dictionary<Guid,string>();
        await using var db=database.Open();
        await db.OpenAsync(ct);
        await using var c=db.CreateCommand();
        c.CommandText="SELECT Id,Name FROM People ORDER BY Name;";
        await using var r=await c.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct)) x[Guid.Parse(r.GetString(0))]=r.GetString(1);
        return x;
    }

    public async Task<Person> CreatePersonAsync(Guid bookId,string name,string phone,string note,CancellationToken ct=default)
    {
        await EnsurePermissionAsync(PermissionKeys.Create,ct);
        var now=DateTimeOffset.UtcNow;
        var p=new Person(Guid.NewGuid(),bookId,name.Trim(),phone.Trim(),note.Trim(),now)
        {
            AccountCode="ACC-"+Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            WhatsAppEnabled=!string.IsNullOrWhiteSpace(phone),
            UpdatedAt=now
        };
        await using var db=database.Open(); await db.OpenAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        await using var c=db.CreateCommand(); c.Transaction=(SqliteTransaction)tx;
        if(!await ExistsAsync(c,"Books",bookId,ct)) throw new InvalidOperationException("الدفتر المحدد غير موجود.");
        c.Parameters.Clear();
        c.CommandText="INSERT INTO People(Id,BookId,Name,Phone,Note,CreatedAt,AccountCode,CreditLimit,Address,SecondaryPhone,AccountNature,WhatsAppEnabled,UpdatedAt) VALUES($id,$book,$name,$phone,$note,$created,$code,NULL,'','',$nature,$whatsapp,$updated);";
        P(c,"$id",p.Id.ToString("N")); P(c,"$book",p.BookId.ToString("N")); P(c,"$name",p.Name); P(c,"$phone",p.Phone); P(c,"$note",p.Note); P(c,"$created",p.CreatedAt.ToString("O")); P(c,"$code",p.AccountCode); P(c,"$nature",(int)p.Nature); P(c,"$whatsapp",p.WhatsAppEnabled?1:0); P(c,"$updated",p.UpdatedAt.ToString("O"));
        await c.ExecuteNonQueryAsync(ct); await tx.CommitAsync(ct); return p;
    }

    public async Task<Person> CreatePersonWithOpeningBalanceAsync(Guid bookId,string name,string phone,string note,decimal openingBalance,OperationType openingDirection,CancellationToken ct=default)
    {
        await EnsurePermissionAsync(PermissionKeys.Create,ct);
        if(openingBalance<0) throw new ArgumentOutOfRangeException(nameof(openingBalance),"الرصيد الافتتاحي لا يمكن أن يكون سالباً.");
        if(openingBalance>0 && openingDirection is not (OperationType.DebtToUs or OperationType.DebtOnUs)) throw new ArgumentException("اتجاه الرصيد الافتتاحي غير صالح.",nameof(openingDirection));
        var now=DateTimeOffset.UtcNow;
        var p=new Person(Guid.NewGuid(),bookId,name.Trim(),phone.Trim(),note.Trim(),now)
        {
            AccountCode="ACC-"+Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
            WhatsAppEnabled=!string.IsNullOrWhiteSpace(phone),
            UpdatedAt=now
        };
        await using var db=database.Open(); await db.OpenAsync(ct); await using var tx=await db.BeginTransactionAsync(ct); await using var c=db.CreateCommand(); c.Transaction=(SqliteTransaction)tx;
        if(!await ExistsAsync(c,"Books",bookId,ct)) throw new InvalidOperationException("الدفتر المحدد غير موجود.");
        c.Parameters.Clear();
        c.CommandText="INSERT INTO People(Id,BookId,Name,Phone,Note,CreatedAt,AccountCode,CreditLimit,Address,SecondaryPhone,AccountNature,WhatsAppEnabled,UpdatedAt) VALUES($id,$book,$name,$phone,$note,$created,$code,NULL,'','',$nature,$whatsapp,$updated);";
        P(c,"$id",p.Id.ToString("N")); P(c,"$book",p.BookId.ToString("N")); P(c,"$name",p.Name); P(c,"$phone",p.Phone); P(c,"$note",p.Note); P(c,"$created",p.CreatedAt.ToString("O")); P(c,"$code",p.AccountCode); P(c,"$nature",(int)p.Nature); P(c,"$whatsapp",p.WhatsAppEnabled?1:0); P(c,"$updated",p.UpdatedAt.ToString("O"));
        await c.ExecuteNonQueryAsync(ct);
        if(openingBalance>0)
        {
            var operation=new LedgerOperation(Guid.NewGuid(),p.Id,openingDirection,decimal.Round(openingBalance,2),"رصيد افتتاحي",DateTimeOffset.UtcNow,appSession?.CurrentUser?.Id ?? Guid.Empty);
            if(operation.CreatedBy==Guid.Empty) throw new InvalidOperationException("لا توجد جلسة مستخدم صالحة لتسجيل الرصيد الافتتاحي.");
            if(!await ExistsAsync(c,"Users",operation.CreatedBy,ct)) throw new InvalidOperationException("المستخدم المنفذ للعملية غير موجود.");
            c.Parameters.Clear(); c.CommandText="INSERT INTO Operations(Id,PersonId,Type,Amount,Note,OccurredAt,CreatedBy,ReferenceNumber) VALUES($id,$person,$type,$amount,$note,$when,$user,$reference);"; P(c,"$id",operation.Id.ToString("N")); P(c,"$person",operation.PersonId.ToString("N")); P(c,"$type",(int)operation.Type); P(c,"$amount",S(operation.Amount)); P(c,"$note",operation.Note); P(c,"$when",operation.OccurredAt.ToString("O")); P(c,"$user",operation.CreatedBy.ToString("N")); P(c,"$reference",(object?)operation.ReferenceNumber ?? DBNull.Value); await c.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        if(openingBalance>0 && appSession?.CurrentUser is AppUser user) await audit.LogActionAsync(user.Id,"PERSON_CREATE","Person",p.Id,new { p.Name,OpeningBalance=openingBalance,OpeningDirection=openingDirection },ct);
        return p;
    }
    public async Task<DashboardSummary> GetDashboardAsync(CancellationToken ct=default)
    {
        await using var db=database.Open();
        await db.OpenAsync(ct);
        await using var c=db.CreateCommand();
        c.CommandText="""
SELECT COALESCE(SUM(CASE
    WHEN Type IN(3,4,7) THEN CAST(Amount AS REAL)
    WHEN Type IN(0,2,5) THEN -CAST(Amount AS REAL)
    WHEN Type=1 THEN CAST(Amount AS REAL)
    ELSE 0
END),0)
FROM Operations;
""";
        await using var r=await c.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        var netBalance=r.GetDecimal(0);
        var split=balanceEngine.Split(netBalance);
        await r.DisposeAsync();
        c.CommandText="SELECT COALESCE(SUM(CAST(StockSmallUnits AS REAL)*CAST(PurchasePrice AS REAL)),0) FROM Products;";
        var stock=Convert.ToDecimal(await c.ExecuteScalarAsync(ct));
        c.CommandText="SELECT COUNT(*) FROM Operations WHERE OccurredAt>=date('now','localtime');";
        var today=Convert.ToInt32(await c.ExecuteScalarAsync(ct));
        c.CommandText="SELECT COALESCE(SUM(CAST(Total AS REAL)),0) FROM Invoices WHERE Type=0 AND IssuedAt>=date('now','localtime');";
        var sales=Convert.ToDecimal(await c.ExecuteScalarAsync(ct));
        c.CommandText="SELECT COALESCE(SUM(CAST(Total AS REAL)),0) FROM Invoices WHERE Type=1 AND IssuedAt>=date('now','localtime');";
        var purchases=Convert.ToDecimal(await c.ExecuteScalarAsync(ct));
        c.CommandText="SELECT COALESCE(SUM(CAST(Amount AS REAL)),0) FROM Expenses WHERE OccurredAt>=date('now','localtime');";
        var expenses=Convert.ToDecimal(await c.ExecuteScalarAsync(ct));
        return new DashboardSummary(split.OwedToUs,split.WeOwe,stock,today,sales,purchases,expenses);
    }
    public async Task<IReadOnlyList<LedgerOperation>> GetOperationsAsync(Guid? personId=null,int take=100,CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.View,ct);var x=new List<LedgerOperation>();await using var db=database.Open();await db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="SELECT Id,PersonId,Type,Amount,Note,OccurredAt,CreatedBy,ReferenceNumber FROM Operations WHERE($p IS NULL OR PersonId=$p) ORDER BY OccurredAt DESC LIMIT $take;";P(c,"$p",personId?.ToString("N")??(object)DBNull.Value);P(c,"$take",Math.Clamp(take,1,1000));await using var r=await c.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))x.Add(new LedgerOperation(Guid.Parse(r.GetString(0)),Guid.Parse(r.GetString(1)),(OperationType)r.GetInt32(2),D(r.GetString(3)),r.GetString(4),DateTimeOffset.Parse(r.GetString(5)),Guid.Parse(r.GetString(6))) { ReferenceNumber = r.IsDBNull(7) ? null : r.GetString(7) });return x;}
    public async Task<IReadOnlyList<LedgerOperation>> GetOperationsInPeriodAsync(DateTimeOffset from,DateTimeOffset toDate,CancellationToken ct=default)
    {
        await EnsurePermissionAsync(PermissionKeys.View,ct);
        if(from>toDate)throw new ArgumentOutOfRangeException(nameof(from),"بداية الفترة لا يمكن أن تتجاوز نهايتها.");
        var operations=new List<LedgerOperation>();
        await using var db=database.Open();
        await db.OpenAsync(ct);
        await using var command=db.CreateCommand();
        command.CommandText="SELECT Id,PersonId,Type,Amount,Note,OccurredAt,CreatedBy,ReferenceNumber FROM Operations WHERE julianday(OccurredAt)>=julianday($from) AND julianday(OccurredAt)<=julianday($to) ORDER BY OccurredAt DESC;";
        P(command,"$from",from.ToString("O",CultureInfo.InvariantCulture));
        P(command,"$to",toDate.ToString("O",CultureInfo.InvariantCulture));
        await using var reader=await command.ExecuteReaderAsync(ct);
        while(await reader.ReadAsync(ct))
            operations.Add(new LedgerOperation(Guid.Parse(reader.GetString(0)),Guid.Parse(reader.GetString(1)),(OperationType)reader.GetInt32(2),D(reader.GetString(3)),reader.GetString(4),DateTimeOffset.Parse(reader.GetString(5),CultureInfo.InvariantCulture),Guid.Parse(reader.GetString(6))) { ReferenceNumber = reader.IsDBNull(7) ? null : reader.GetString(7) });
        return operations;
    }
    public async Task<LedgerOperation> CreateOperationAsync(LedgerOperation op,CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.Create,ct);if(op.Amount<=0)throw new ArgumentOutOfRangeException(nameof(op),"مبلغ العملية يجب أن يكون أكبر من صفر.");await using var db=database.Open();await db.OpenAsync(ct);await using var tx=await db.BeginTransactionAsync(ct);await using var c=db.CreateCommand();c.Transaction=(SqliteTransaction)tx;if(!await ExistsAsync(c,"People",op.PersonId,ct))throw new InvalidOperationException("الحساب المرتبط بالعملية غير موجود.");if(!await ExistsAsync(c,"Users",op.CreatedBy,ct))throw new InvalidOperationException("المستخدم المنفذ للعملية غير موجود.");c.Parameters.Clear();c.CommandText="SELECT EXISTS(SELECT 1 FROM Operations WHERE Id=$id);";P(c,"$id",op.Id.ToString("N"));if(Convert.ToInt32(await c.ExecuteScalarAsync(ct))==1)throw new InvalidOperationException("العملية مسجلة مسبقاً.");c.Parameters.Clear();c.CommandText="INSERT INTO Operations(Id,PersonId,Type,Amount,Note,OccurredAt,CreatedBy,ReferenceNumber) VALUES($id,$person,$type,$amount,$note,$when,$user,$reference);";P(c,"$id",op.Id.ToString("N"));P(c,"$person",op.PersonId.ToString("N"));P(c,"$type",(int)op.Type);P(c,"$amount",S(op.Amount));P(c,"$note",op.Note);P(c,"$when",op.OccurredAt.ToString("O"));P(c,"$user",op.CreatedBy.ToString("N"));P(c,"$reference",(object?)op.ReferenceNumber ?? DBNull.Value);await c.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);await audit.LogActionAsync(op.CreatedBy, "OPERATION_CREATE", "Operation", op.Id,new { op.PersonId,op.Type,op.Amount },ct);return op;}
    public async Task DeleteOperationAsync(Guid id,Guid deletedBy,CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.Delete,ct);await using var db=database.Open();await db.OpenAsync(ct);await using var tx=await db.BeginTransactionAsync(ct);await using var c=db.CreateCommand();c.Transaction=(SqliteTransaction)tx;c.CommandText="INSERT INTO DeletedItems(Id,EntityType,EntityId,DeletedAt,DeletedBy,Payload) SELECT lower(hex(randomblob(16))),'Operation',Id,$now,$by,json_object('Id',Id,'PersonId',PersonId,'Type',Type,'Amount',Amount,'Note',Note,'OccurredAt',OccurredAt,'CreatedBy',CreatedBy,'ReferenceNumber',ReferenceNumber) FROM Operations WHERE Id=$id;";P(c,"$id",id.ToString("N"));P(c,"$now",DateTimeOffset.UtcNow.ToString("O"));P(c,"$by",deletedBy.ToString("N"));if(await c.ExecuteNonQueryAsync(ct)==0)throw new InvalidOperationException("العملية غير موجودة.");c.Parameters.Clear();c.CommandText="DELETE FROM Operations WHERE Id=$id;";P(c,"$id",id.ToString("N"));await c.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);await audit.LogActionAsync(deletedBy, "OPERATION_DELETE", "Operation", id,null,ct);await audit.LogActionAsync(deletedBy, "TRANSACTION_DELETED", "Operation", id,new { OperationId=id },ct);}
    public async Task DeletePersonAsync(Guid id,Guid deletedBy,CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.Delete,ct);await using var db=database.Open();await db.OpenAsync(ct);await using var tx=await db.BeginTransactionAsync(ct);await using var c=db.CreateCommand();c.Transaction=(SqliteTransaction)tx;c.CommandText="SELECT COUNT(*) FROM Operations WHERE PersonId=$id;";P(c,"$id",id.ToString("N"));if(Convert.ToInt64(await c.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("لا يمكن حذف حساب يحتوي على عمليات. احذف العمليات أولاً من سجل العمليات.");c.Parameters.Clear();c.CommandText="INSERT INTO DeletedItems(Id,EntityType,EntityId,DeletedAt,DeletedBy,Payload) SELECT lower(hex(randomblob(16))),'Person',Id,$now,$by,json_object('Id',Id,'BookId',BookId,'Name',Name,'Phone',Phone,'Note',Note,'CreatedAt',CreatedAt,'AccountCode',AccountCode,'CreditLimit',CreditLimit,'Address',Address,'SecondaryPhone',SecondaryPhone,'AccountNature',AccountNature,'WhatsAppEnabled',WhatsAppEnabled,'UpdatedAt',UpdatedAt) FROM People WHERE Id=$id;";P(c,"$id",id.ToString("N"));P(c,"$now",DateTimeOffset.UtcNow.ToString("O"));P(c,"$by",deletedBy.ToString("N"));if(await c.ExecuteNonQueryAsync(ct)==0)throw new InvalidOperationException("الحساب غير موجود.");c.Parameters.Clear();c.CommandText="DELETE FROM People WHERE Id=$id;";P(c,"$id",id.ToString("N"));await c.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);await audit.LogActionAsync(deletedBy, "PERSON_DELETE", "Person", id,null,ct);}
    public async Task DeleteProductAsync(Guid id,Guid deletedBy,CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.Delete,ct);await using var db=database.Open();await db.OpenAsync(ct);await using var tx=await db.BeginTransactionAsync(ct);await using var c=db.CreateCommand();c.Transaction=(SqliteTransaction)tx;c.CommandText="SELECT COUNT(*) FROM InvoiceLines WHERE ProductId=$id;";P(c,"$id",id.ToString("N"));if(Convert.ToInt64(await c.ExecuteScalarAsync(ct))>0)throw new InvalidOperationException("لا يمكن حذف صنف مرتبط بفواتير.");c.Parameters.Clear();c.CommandText="INSERT INTO DeletedItems(Id,EntityType,EntityId,DeletedAt,DeletedBy,Payload) SELECT lower(hex(randomblob(16))),'Product',Id,$now,$by,json_object('Id',Id,'Name',Name,'Barcode',Barcode,'Category',Category,'LargeUnit',LargeUnit,'SmallUnit',SmallUnit,'ConversionFactor',ConversionFactor,'PurchasePrice',PurchasePrice,'SalePrice',SalePrice,'StockSmallUnits',StockSmallUnits,'AlertThreshold',AlertThreshold,'CreatedAt',CreatedAt,'DefaultSupplierId',DefaultSupplierId,'Note',Note,'ShelfLocation',ShelfLocation,'WarehouseName',WarehouseName) FROM Products WHERE Id=$id;";P(c,"$id",id.ToString("N"));P(c,"$now",DateTimeOffset.UtcNow.ToString("O"));P(c,"$by",deletedBy.ToString("N"));if(await c.ExecuteNonQueryAsync(ct)==0)throw new InvalidOperationException("الصنف غير موجود.");c.Parameters.Clear();c.CommandText="DELETE FROM Products WHERE Id=$id;";P(c,"$id",id.ToString("N"));await c.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);await audit.LogActionAsync(deletedBy, "PRODUCT_DELETE", "Product", id,null,ct);}
    public async Task DeleteStockMovementAsync(Guid id,Guid deletedBy,CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.Delete,ct);await using var db=database.Open();await db.OpenAsync(ct);await using var tx=await db.BeginTransactionAsync(ct);await using var c=db.CreateCommand();c.Transaction=(SqliteTransaction)tx;c.CommandText="SELECT ProductId,Type,QuantitySmallUnits,InvoiceId FROM StockMovements WHERE Id=$id;";P(c,"$id",id.ToString("N"));await using var r=await c.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))throw new InvalidOperationException("حركة المخزون غير موجودة.");var productId=r.GetString(0);var type=r.GetInt32(1);var qty=D(r.GetString(2));var invoiceId=r.IsDBNull(3)?null:r.GetString(3);await r.DisposeAsync();if(!string.IsNullOrWhiteSpace(invoiceId))throw new InvalidOperationException("لا يمكن حذف حركة مخزون مرتبطة بفاتورة؛ استخدم عكس الفاتورة حتى تبقى الحسابات والمخزون متطابقين.");var signed=type is (int)StockMovementType.Sale or (int)StockMovementType.ReturnOut or (int)StockMovementType.AdjustmentOut?-qty:qty;if(signed<0){c.Parameters.Clear();c.CommandText="SELECT EXISTS(SELECT 1 FROM ProductBatches WHERE ProductId=$product);";P(c,"$product",productId);if(Convert.ToInt32(await c.ExecuteScalarAsync(ct))==1)throw new InvalidOperationException("لا يمكن حذف حركة مخزون سالبة لصنف ذي دفعات لأن سجل الدفعات لا يحتفظ بتخصيص هذه الحركة؛ سجّل حركة تسوية عكسية بدلاً من حذف التاريخ.");}c.Parameters.Clear();c.CommandText="SELECT CAST(StockSmallUnits AS REAL) FROM Products WHERE Id=$p;";P(c,"$p",productId);var current=Convert.ToDecimal(await c.ExecuteScalarAsync(ct));var next=current-signed;if(next<0)throw new InvalidOperationException("لا يمكن حذف الحركة لأن عكسها سيؤدي إلى رصيد مخزني سالب.");c.Parameters.Clear();c.CommandText="INSERT INTO DeletedItems(Id,EntityType,EntityId,DeletedAt,DeletedBy,Payload) SELECT lower(hex(randomblob(16))),'StockMovement',Id,$now,$by,json_object('Id',Id,'ProductId',ProductId,'Type',Type,'QuantitySmallUnits',QuantitySmallUnits,'UnitPrice',UnitPrice,'Note',Note,'OccurredAt',OccurredAt,'CreatedBy',CreatedBy) FROM StockMovements WHERE Id=$id;";P(c,"$id",id.ToString("N"));P(c,"$now",DateTimeOffset.UtcNow.ToString("O"));P(c,"$by",deletedBy.ToString("N"));await c.ExecuteNonQueryAsync(ct);c.Parameters.Clear();c.CommandText="UPDATE Products SET StockSmallUnits=$stock WHERE Id=$p;DELETE FROM StockMovements WHERE Id=$id;";P(c,"$stock",S(next));P(c,"$p",productId);P(c,"$id",id.ToString("N"));await c.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);await audit.LogActionAsync(deletedBy, "STOCK_DELETE", "StockMovement", id,null,ct);}
    public async Task<IReadOnlyList<Product>> GetProductsAsync(string? search=null,CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.View,ct);var x=new List<Product>();await using var db=database.Open();await db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="SELECT p.Id,p.Name,p.Barcode,p.Category,p.LargeUnit,p.SmallUnit,p.ConversionFactor,p.PurchasePrice,p.SalePrice,p.StockSmallUnits,p.AlertThreshold,p.CreatedAt,p.DefaultSupplierId,p.Note,p.ShelfLocation,p.WarehouseName,COALESCE(s.Name,''),p.CategoryId,p.SellingPriceMajor,p.SellingPriceMinor,p.MinSellingPrice,p.ReorderLevel,p.UpdatedAt FROM Products p LEFT JOIN People s ON s.Id=p.DefaultSupplierId WHERE $s='' OR p.Name LIKE '%'||$s||'%' OR p.Barcode LIKE '%'||$s||'%' OR p.Category LIKE '%'||$s||'%' ORDER BY p.Name;";P(c,"$s",(search??"").Trim());await using var r=await c.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct)){var product=new Product(Guid.Parse(r.GetString(0)),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),D(r.GetString(6)),D(r.GetString(7)),D(r.GetString(8)),D(r.GetString(9)),D(r.GetString(10)),DateTimeOffset.Parse(r.GetString(11))){DefaultSupplierId=r.IsDBNull(12)?null:Guid.Parse(r.GetString(12)),DefaultSupplierName=r.GetString(16),Note=r.GetString(13),ShelfLocation=r.GetString(14),WarehouseName=r.GetString(15),CategoryId=r.GetString(17),SellingPriceMajor=D(r.GetString(18)),SellingPriceMinor=D(r.GetString(19)),MinimumSellingPrice=D(r.GetString(20)),ReorderPoint=D(r.GetString(21)),LowStockThreshold=D(r.GetString(21)),UpdatedAt=DateTimeOffset.TryParse(r.GetString(22),out var updated)?updated:DateTimeOffset.Parse(r.GetString(11))};x.Add(product);}return x;}
    public async Task<Product> CreateProductAsync(Product p,CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.Create,ct);if(string.IsNullOrWhiteSpace(p.Name)||string.IsNullOrWhiteSpace(p.SmallUnit)||string.IsNullOrWhiteSpace(p.LargeUnit))throw new ArgumentException("اسم الصنف ووحدتاه بيانات إلزامية.",nameof(p));if(p.ConversionFactor<=0||p.PurchasePrice<0||p.SalePrice<0||p.StockSmallUnits<0||p.AlertThreshold<0)throw new ArgumentOutOfRangeException(nameof(p),"بيانات الصنف غير صالحة.");if(p.DefaultSupplierId is Guid supplier&&!await SupplierExistsAsync(supplier,ct))throw new InvalidOperationException("المورد المحدد غير موجود.");await using var db=database.Open();await db.OpenAsync(ct);await using var tx=await db.BeginTransactionAsync(ct);await using var c=db.CreateCommand();c.Transaction=(SqliteTransaction)tx;c.CommandText="SELECT EXISTS(SELECT 1 FROM Products WHERE lower(trim(Name))=lower(trim($name)) OR ($barcode<>'' AND lower(trim(Barcode))=lower(trim($barcode))));";P(c,"$name",p.Name.Trim());P(c,"$barcode",p.Barcode.Trim());if(Convert.ToInt32(await c.ExecuteScalarAsync(ct))==1)throw new InvalidOperationException("يوجد صنف بالاسم نفسه أو بالباركود نفسه؛ اختر الصنف الموجود أو غيّر بيانات التعريف.");c.Parameters.Clear();c.CommandText="SELECT Name,Barcode FROM Products;";await using(var duplicateReader=await c.ExecuteReaderAsync(ct)){while(await duplicateReader.ReadAsync(ct)){if(NormalizeIdentity(duplicateReader.GetString(0))==NormalizeIdentity(p.Name)||(p.Barcode.Trim().Length>0&&string.Equals(duplicateReader.GetString(1).Trim(),p.Barcode.Trim(),StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("يوجد صنف بالاسم نفسه أو باسم قريب جدًا أو بالباركود نفسه.");}}c.Parameters.Clear();c.CommandText="INSERT INTO Products(Id,Name,Barcode,Category,LargeUnit,SmallUnit,ConversionFactor,PurchasePrice,SalePrice,StockSmallUnits,AlertThreshold,CreatedAt,DefaultSupplierId,Note,ShelfLocation,WarehouseName,CategoryId,SellingPriceMajor,SellingPriceMinor,MinSellingPrice,ReorderLevel,UpdatedAt) VALUES($id,$name,$barcode,$category,$large,$small,$factor,$purchase,$sale,$stock,$alert,$created,$supplier,$note,$shelf,$warehouse,$categoryId,$majorSale,$minorSale,$minimum,$reorder,$updated);";P(c,"$id",p.Id.ToString("N"));P(c,"$name",p.Name.Trim());P(c,"$barcode",p.Barcode.Trim());P(c,"$category",p.Category.Trim());P(c,"$large",p.LargeUnit.Trim());P(c,"$small",p.SmallUnit.Trim());P(c,"$factor",S(p.ConversionFactor));P(c,"$purchase",S(decimal.Round(p.PurchasePrice,2)));P(c,"$sale",S(decimal.Round(p.SalePrice,2)));P(c,"$stock",S(p.StockSmallUnits));P(c,"$alert",S(p.AlertThreshold));P(c,"$created",p.CreatedAt.ToString("O"));P(c,"$supplier",p.DefaultSupplierId?.ToString("N")??(object)DBNull.Value);P(c,"$note",p.Note.Trim());P(c,"$shelf",p.ShelfLocation.Trim());P(c,"$warehouse",p.WarehouseName.Trim());P(c,"$categoryId",p.CategoryId);P(c,"$majorSale",S(p.SellingPriceMajor));P(c,"$minorSale",S(p.SellingPriceMinor));P(c,"$minimum",S(p.MinimumSellingPrice));P(c,"$reorder",S(p.ReorderPoint));P(c,"$updated",DateTimeOffset.UtcNow.ToString("O"));await c.ExecuteNonQueryAsync(ct);if(p.StockSmallUnits>0&&appSession?.CurrentUser is AppUser currentUser){c.Parameters.Clear();c.CommandText="INSERT INTO StockMovements(Id,ProductId,Type,QuantitySmallUnits,UnitPrice,Note,OccurredAt,CreatedBy,InvoiceId) VALUES($id,$product,$type,$qty,$price,$note,$when,$user,NULL);";P(c,"$id",Guid.NewGuid().ToString("N"));P(c,"$product",p.Id.ToString("N"));P(c,"$type",(int)StockMovementType.Inventory);P(c,"$qty",S(p.StockSmallUnits));P(c,"$price",S(p.PurchasePrice));P(c,"$note","رصيد افتتاحي للصنف");P(c,"$when",p.CreatedAt.ToString("O"));P(c,"$user",appSession?.CurrentUser?.Id??Guid.Empty);await c.ExecuteNonQueryAsync(ct);}await tx.CommitAsync(ct);await audit.LogActionAsync(appSession?.CurrentUser?.Id,"PRODUCT_CREATE","Product",p.Id,new { p.Name,p.Barcode,p.StockSmallUnits,p.DefaultSupplierId },ct);return p;}
    public async Task<Product> UpdateProductAsync(Product p,CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.Edit,ct);if(string.IsNullOrWhiteSpace(p.Name)||string.IsNullOrWhiteSpace(p.SmallUnit)||string.IsNullOrWhiteSpace(p.LargeUnit))throw new ArgumentException("اسم الصنف ووحدتاه بيانات إلزامية.",nameof(p));if(p.ConversionFactor<=0||p.PurchasePrice<0||p.SalePrice<0||p.StockSmallUnits<0||p.AlertThreshold<0)throw new ArgumentOutOfRangeException(nameof(p),"قيم الصنف غير صالحة.");if(p.DefaultSupplierId is Guid supplier&&!await SupplierExistsAsync(supplier,ct))throw new InvalidOperationException("المورد المحدد غير موجود.");await using var db=database.Open();await db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="SELECT EXISTS(SELECT 1 FROM Products WHERE Id<>$id AND (lower(trim(Name))=lower(trim($name)) OR ($barcode<>'' AND lower(trim(Barcode))=lower(trim($barcode)))));";P(c,"$id",p.Id.ToString("N"));P(c,"$name",p.Name.Trim());P(c,"$barcode",p.Barcode.Trim());if(Convert.ToInt32(await c.ExecuteScalarAsync(ct))==1)throw new InvalidOperationException("يوجد صنف آخر بالاسم نفسه أو بالباركود نفسه.");c.Parameters.Clear();c.CommandText="SELECT Name,Barcode FROM Products WHERE Id<>$id;";P(c,"$id",p.Id.ToString("N"));await using(var duplicateReader=await c.ExecuteReaderAsync(ct)){while(await duplicateReader.ReadAsync(ct)){if(NormalizeIdentity(duplicateReader.GetString(0))==NormalizeIdentity(p.Name)||(p.Barcode.Trim().Length>0&&string.Equals(duplicateReader.GetString(1).Trim(),p.Barcode.Trim(),StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("يوجد صنف آخر بالاسم نفسه أو باسم قريب جدًا أو بالباركود نفسه.");}}c.Parameters.Clear();c.CommandText="SELECT LargeUnit,SmallUnit,ConversionFactor,StockSmallUnits,EXISTS(SELECT 1 FROM InvoiceLines WHERE ProductId=Products.Id),EXISTS(SELECT 1 FROM ProductBatches WHERE ProductId=Products.Id) FROM Products WHERE Id=$id;";P(c,"$id",p.Id.ToString("N"));string currentLarge,currentSmall;decimal currentFactor,currentStock;bool hasInvoiceHistory,hasBatches;await using(var currentReader=await c.ExecuteReaderAsync(ct)){if(!await currentReader.ReadAsync(ct))throw new InvalidOperationException("الصنف غير موجود.");currentLarge=currentReader.GetString(0);currentSmall=currentReader.GetString(1);currentFactor=D(Convert.ToString(currentReader.GetValue(2),CultureInfo.InvariantCulture)!);currentStock=D(Convert.ToString(currentReader.GetValue(3),CultureInfo.InvariantCulture)!);hasInvoiceHistory=currentReader.GetInt32(4)!=0;hasBatches=currentReader.GetInt32(5)!=0;}var unitsChanged=!string.Equals(currentLarge.Trim(),p.LargeUnit.Trim(),StringComparison.OrdinalIgnoreCase)||!string.Equals(currentSmall.Trim(),p.SmallUnit.Trim(),StringComparison.OrdinalIgnoreCase)||currentFactor!=p.ConversionFactor;if(unitsChanged&&(currentStock>0||hasInvoiceHistory||hasBatches))throw new InvalidOperationException("لا يمكن تغيير وحدات الصنف أو معامل التحويل بعد وجود رصيد أو دفعات أو فواتير؛ أنشئ صنفاً جديداً حتى لا تتغير دلالة الأرصدة التاريخية.");c.Parameters.Clear();c.CommandText="UPDATE Products SET Name=$name,Barcode=$barcode,Category=$category,LargeUnit=$large,SmallUnit=$small,ConversionFactor=$factor,PurchasePrice=$purchase,SalePrice=$sale,AlertThreshold=$alert,DefaultSupplierId=$supplier,Note=$note,ShelfLocation=$shelf,WarehouseName=$warehouse,CategoryId=$categoryId,SellingPriceMajor=$majorSale,SellingPriceMinor=$minorSale,MinSellingPrice=$minimum,ReorderLevel=$reorder,UpdatedAt=$updated WHERE Id=$id;";P(c,"$id",p.Id.ToString("N"));P(c,"$name",p.Name.Trim());P(c,"$barcode",p.Barcode.Trim());P(c,"$category",p.Category.Trim());P(c,"$large",p.LargeUnit.Trim());P(c,"$small",p.SmallUnit.Trim());P(c,"$factor",S(p.ConversionFactor));P(c,"$purchase",S(decimal.Round(p.PurchasePrice,2)));P(c,"$sale",S(decimal.Round(p.SalePrice,2)));P(c,"$alert",S(p.AlertThreshold));P(c,"$supplier",p.DefaultSupplierId?.ToString("N")??(object)DBNull.Value);P(c,"$note",p.Note.Trim());P(c,"$shelf",p.ShelfLocation.Trim());P(c,"$warehouse",p.WarehouseName.Trim());P(c,"$categoryId",p.CategoryId);P(c,"$majorSale",S(p.SellingPriceMajor));P(c,"$minorSale",S(p.SellingPriceMinor));P(c,"$minimum",S(p.MinimumSellingPrice));P(c,"$reorder",S(p.ReorderPoint));P(c,"$updated",DateTimeOffset.UtcNow.ToString("O"));if(await c.ExecuteNonQueryAsync(ct)==0)throw new InvalidOperationException("الصنف غير موجود.");await audit.LogActionAsync(appSession?.CurrentUser?.Id,"PRODUCT_UPDATE","Product",p.Id,new { p.Name,p.Barcode,p.DefaultSupplierId,p.PurchasePrice,p.SalePrice,p.SellingPriceMajor,p.SellingPriceMinor,p.MinimumSellingPrice,p.ConversionFactor },ct);await audit.LogActionAsync(appSession?.CurrentUser?.Id,"PRODUCT_PRICE_UPDATED","Product",p.Id,new { p.Name,p.Barcode,p.PurchasePrice,p.SalePrice,p.SellingPriceMajor,p.SellingPriceMinor },ct);return p;}
    public async Task<StockMovement> CreateStockMovementAsync(StockMovement m,CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.Create,ct);if(m.QuantitySmallUnits<=0)throw new ArgumentOutOfRangeException(nameof(m),"كمية حركة المخزون يجب أن تكون أكبر من صفر.");if(m.UnitPrice<0)throw new ArgumentOutOfRangeException(nameof(m),"سعر الحركة لا يمكن أن يكون سالباً.");await using var db=database.Open();await db.OpenAsync(ct);await using var tx=await db.BeginTransactionAsync(ct);await using var c=db.CreateCommand();c.Transaction=(SqliteTransaction)tx;if(!await ExistsAsync(c,"Products",m.ProductId,ct))throw new InvalidOperationException("الصنف المرتبط بحركة المخزون غير موجود.");if(!await ExistsAsync(c,"Users",m.CreatedBy,ct))throw new InvalidOperationException("المستخدم المنفذ للعملية غير موجود.");c.Parameters.Clear();c.CommandText="SELECT CAST(StockSmallUnits AS REAL) FROM Products WHERE Id=$product;";P(c,"$product",m.ProductId.ToString("N"));var current=Convert.ToDecimal(await c.ExecuteScalarAsync(ct));var signed=m.Type is StockMovementType.Sale or StockMovementType.ReturnOut or StockMovementType.AdjustmentOut?-m.QuantitySmallUnits:m.QuantitySmallUnits;var next=current+signed;if(next<0)throw new InvalidOperationException("الرصيد المخزني غير كافٍ.");if(signed<0){var remaining=m.QuantitySmallUnits;var lots=new List<(string Id,decimal Quantity)>();c.Parameters.Clear();c.CommandText="SELECT Id,QuantityMinor FROM ProductBatches WHERE ProductId=$product AND CAST(QuantityMinor AS REAL)>0 ORDER BY CASE WHEN ExpiryDate IS NULL THEN 1 ELSE 0 END,ExpiryDate,CreatedAt;";P(c,"$product",m.ProductId.ToString("N"));await using(var lotReader=await c.ExecuteReaderAsync(ct)){while(await lotReader.ReadAsync(ct))lots.Add((lotReader.GetString(0),D(lotReader.GetString(1))));}foreach(var lot in lots){if(remaining<=0)break;var consumed=Math.Min(remaining,lot.Quantity);c.Parameters.Clear();c.CommandText="UPDATE ProductBatches SET QuantityMinor=$qty WHERE Id=$id;";P(c,"$qty",S(lot.Quantity-consumed));P(c,"$id",lot.Id);await c.ExecuteNonQueryAsync(ct);remaining-=consumed;}}c.Parameters.Clear();c.CommandText="INSERT INTO StockMovements(Id,ProductId,Type,QuantitySmallUnits,UnitPrice,Note,OccurredAt,CreatedBy,InvoiceId) VALUES($id,$product,$type,$qty,$price,$note,$when,$user,$invoice);";P(c,"$id",m.Id.ToString("N"));P(c,"$product",m.ProductId.ToString("N"));P(c,"$type",(int)m.Type);P(c,"$qty",S(m.QuantitySmallUnits));P(c,"$price",S(m.UnitPrice));P(c,"$note",m.Note);P(c,"$when",m.OccurredAt.ToString("O"));P(c,"$user",m.CreatedBy.ToString("N"));P(c,"$invoice",m.InvoiceId?.ToString("N")??(object)DBNull.Value);await c.ExecuteNonQueryAsync(ct);c.Parameters.Clear();c.CommandText="UPDATE Products SET StockSmallUnits=$stock WHERE Id=$product;";P(c,"$stock",S(next));P(c,"$product",m.ProductId.ToString("N"));await c.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);await audit.LogActionAsync(m.CreatedBy, "STOCK_CREATE", "StockMovement", m.Id,new { m.ProductId,m.Type,m.QuantitySmallUnits },ct);return m;}
    public Task<bool> ReverseInvoiceAsync(int invoiceId, string reason, string userId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(invoiceId);
        return ReverseInvoiceAsync(invoiceId.ToString(CultureInfo.InvariantCulture), reason, userId);
    }

    public async Task<bool> ReverseInvoiceAsync(string invoiceNumber, string reason, string userId)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber)) throw new ArgumentException("An invoice number is required.", nameof(invoiceNumber));
        invoiceNumber=invoiceNumber.Trim();
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reversal reason is required.", nameof(reason));
        if (!Guid.TryParse(userId, out var actorId)) throw new ArgumentException("userId must be a valid user GUID.", nameof(userId));
        await EnsurePermissionAsync(PermissionKeys.Edit, CancellationToken.None);
        string invoiceKey, invoiceNumber; InvoiceType type; Guid? personId; decimal total, paid;
        var now = DateTimeOffset.UtcNow;
        await using (var db = database.Open())
        {
            await db.OpenAsync(); await using var tx = await db.BeginTransactionAsync(); await using var c = db.CreateCommand(); c.Transaction = (SqliteTransaction)tx;
            c.CommandText = "SELECT Id,COALESCE(InvoiceNumber,''),Type,PersonId,Total,Paid,Status FROM Invoices WHERE trim(InvoiceNumber)=trim($number) LIMIT 1;"; P(c,"$number",invoiceNumber); string status;
            await using (var r = await c.ExecuteReaderAsync()) { if (!await r.ReadAsync()) { await tx.RollbackAsync(); return false; } invoiceKey=r.GetString(0); invoiceNumber=r.GetString(1); type=(InvoiceType)r.GetInt32(2); personId=r.IsDBNull(3)?null:Guid.Parse(r.GetString(3)); total=D(r.GetString(4)); paid=D(r.GetString(5)); status=r.GetString(6); }
            if (string.Equals(status,"Reversed",StringComparison.OrdinalIgnoreCase)||string.Equals(status,"Cancelled",StringComparison.OrdinalIgnoreCase)) { await tx.RollbackAsync(); return false; }
            var lines=new List<(string ProductId,decimal Qty,decimal Price)>(); c.Parameters.Clear(); c.CommandText="SELECT ProductId,QuantityMinor,UnitPrice FROM InvoiceLines WHERE InvoiceId=$id;"; P(c,"$id",invoiceKey); await using(var r=await c.ExecuteReaderAsync()) while(await r.ReadAsync()) lines.Add((r.GetString(0),D(r.GetString(1)),D(r.GetString(2))));
            if(type==InvoiceType.Purchase)
            {
                // A purchase reversal must remove the lots it introduced. If any tracked sale has
                // consumed one of those lots, refuse the reversal instead of leaving phantom stock.
                c.Parameters.Clear();c.CommandText="SELECT EXISTS(SELECT 1 FROM ProductBatches b JOIN InvoiceBatchAllocations a ON a.BatchId=b.Id JOIN Invoices consumed ON consumed.Id=a.InvoiceId WHERE b.PurchaseInvoiceRef=$invoice AND lower(consumed.Status) NOT IN ('reversed','cancelled'));";P(c,"$invoice",invoiceNumber);
                if(Convert.ToInt32(await c.ExecuteScalarAsync())==1)throw new InvalidOperationException("لا يمكن عكس فاتورة الشراء بعد بيع كميات من دفعاتها؛ عالج مرتجع البيع أولاً.");
                // Reversed sales retain allocation history. Remove those references before deleting
                // the original purchase lots, otherwise the BatchId foreign key blocks the reversal.
                c.Parameters.Clear();c.CommandText="DELETE FROM InvoiceBatchAllocations WHERE BatchId IN (SELECT Id FROM ProductBatches WHERE PurchaseInvoiceRef=$invoice);";P(c,"$invoice",invoiceNumber);await c.ExecuteNonQueryAsync();
                c.Parameters.Clear();c.CommandText="DELETE FROM ProductBatches WHERE PurchaseInvoiceRef=$invoice;";P(c,"$invoice",invoiceNumber);await c.ExecuteNonQueryAsync();
            }
            if(type==InvoiceType.Sale)
            {
                // Restore precisely the batches consumed by this sale. Return invoices do not
                // consume the original sale's batch allocation records.
                var allocations=new List<(string BatchId,decimal Quantity)>();
                c.Parameters.Clear();c.CommandText="SELECT BatchId,QuantityMinor FROM InvoiceBatchAllocations WHERE InvoiceId=$invoice;";P(c,"$invoice",invoiceKey);
                await using(var allocationReader=await c.ExecuteReaderAsync()){while(await allocationReader.ReadAsync())allocations.Add((allocationReader.GetString(0),D(allocationReader.GetString(1))));}
                foreach(var allocation in allocations)
                {
                    c.Parameters.Clear();c.CommandText="UPDATE ProductBatches SET QuantityMinor=CAST(QuantityMinor AS REAL)+CAST($quantity AS REAL) WHERE Id=$id;";P(c,"$quantity",S(allocation.Quantity));P(c,"$id",allocation.BatchId);
                    if(await c.ExecuteNonQueryAsync()!=1)throw new InvalidOperationException("A batch referenced by the invoice no longer exists; reversal was cancelled.");
                }
            }
            foreach(var line in lines) { bool restoresStock=type==InvoiceType.Sale||type==InvoiceType.PurchaseReturn; c.Parameters.Clear(); c.CommandText="SELECT CAST(StockSmallUnits AS REAL) FROM Products WHERE Id=$id;"; P(c,"$id",line.ProductId); var value=await c.ExecuteScalarAsync(); if(value is null||value is DBNull) throw new InvalidOperationException("Invoice references a missing product."); var next=Convert.ToDecimal(value,CultureInfo.InvariantCulture)+(restoresStock?line.Qty:-line.Qty); if(next<0) throw new InvalidOperationException("Invoice reversal would make product stock negative."); c.Parameters.Clear(); c.CommandText="UPDATE Products SET StockSmallUnits=$stock WHERE Id=$id;"; P(c,"$stock",S(next)); P(c,"$id",line.ProductId); await c.ExecuteNonQueryAsync(); c.Parameters.Clear(); c.CommandText="INSERT INTO StockMovements(Id,ProductId,Type,QuantitySmallUnits,UnitPrice,Note,OccurredAt,CreatedBy,InvoiceId) VALUES($id,$product,$type,$qty,$price,$note,$when,$user,$invoice);"; P(c,"$id",Guid.NewGuid().ToString("N")); P(c,"$product",line.ProductId); P(c,"$type",(int)(restoresStock?StockMovementType.ReturnIn:StockMovementType.AdjustmentOut)); P(c,"$qty",S(line.Qty)); P(c,"$price",S(line.Price)); P(c,"$note",$"Invoice reversal #{invoiceNumber}: {reason}"); P(c,"$when",now.ToString("O")); P(c,"$user",actorId.ToString("N")); P(c,"$invoice",invoiceKey); await c.ExecuteNonQueryAsync(); }
            if(personId is Guid person && (type==InvoiceType.Sale||type==InvoiceType.Purchase)) { var amount=Math.Max(0m,total-paid); if(amount>0) { c.Parameters.Clear(); c.CommandText="INSERT INTO Operations(Id,PersonId,Type,Amount,Note,OccurredAt,CreatedBy) VALUES($id,$person,$type,$amount,$note,$when,$user);"; P(c,"$id",Guid.NewGuid().ToString("N")); P(c,"$person",person.ToString("N")); P(c,"$type",(int)(type==InvoiceType.Sale?OperationType.Receive:OperationType.Payment)); P(c,"$amount",S(amount)); P(c,"$note",$"Reversal of invoice #{invoiceNumber}: {reason}"); P(c,"$when",now.ToString("O")); P(c,"$user",actorId.ToString("N")); await c.ExecuteNonQueryAsync(); } }
            // Record the cash leg of a reversal explicitly so cash-register totals can net it
            // against the original receipt/payment rather than silently retaining the old balance.
            if(paid>0 && (type==InvoiceType.Sale||type==InvoiceType.Purchase)) { c.Parameters.Clear(); c.CommandText="INSERT INTO CashRegisterEntries(Id,InvoiceId,Amount,EntryType,OccurredAt,CreatedBy) VALUES($id,$invoice,$amount,$entryType,$when,$user);"; P(c,"$id",Guid.NewGuid().ToString("N")); P(c,"$invoice",invoiceKey); P(c,"$amount",S(paid)); P(c,"$entryType",type==InvoiceType.Sale?"SALE_REFUND":"PURCHASE_REFUND"); P(c,"$when",now.ToString("O")); P(c,"$user",actorId.ToString("N")); await c.ExecuteNonQueryAsync(); }
            c.Parameters.Clear(); c.CommandText="UPDATE Invoices SET Status='Reversed',Note=Note || $suffix WHERE Id=$id;"; P(c,"$suffix",$" [REVERSED: {reason}]"); P(c,"$id",invoiceKey); await c.ExecuteNonQueryAsync(); await tx.CommitAsync();
        }
        await audit.LogActionAsync(actorId,"INVOICE_REVERSED","Invoice",Guid.TryParse(invoiceKey,out var parsed)?parsed:null,new { InvoiceNumber=invoiceNumber,Reason=reason,Type=type.ToString(),Total=total,Paid=paid }); return true;
    }
    public async Task<Invoice> CreateInvoiceAsync(Invoice i,IReadOnlyList<InvoiceLine> lines,CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.Create,ct);await using var db=database.Open();await db.OpenAsync(ct);await using var tx=await db.BeginTransactionAsync(ct);await using var c=db.CreateCommand();c.Transaction=(SqliteTransaction)tx;c.CommandText="INSERT INTO Invoices(Id,Type,PersonId,IssuedAt,Subtotal,Discount,Total,Paid,PaymentMode,Note,CreatedBy,InvoiceNumber,DiscountPercentage,TaxAmount,CustomerName,StoreName,Currency,IssuedByUserId) VALUES($id,$type,$person,$date,$sub,$discount,$total,$paid,$mode,$note,$user,$number,$discountPercentage,$tax,$customer,$store,$currency,$issuedBy);";P(c,"$id",i.Id.ToString("N"));P(c,"$type",(int)i.Type);P(c,"$person",i.PersonId?.ToString("N")??(object)DBNull.Value);P(c,"$date",i.IssuedAt.ToString("O"));P(c,"$sub",S(i.Subtotal));P(c,"$discount",S(i.Discount));P(c,"$total",S(i.Total));P(c,"$paid",S(i.Paid));P(c,"$mode",(int)i.PaymentMode);P(c,"$note",i.Note);P(c,"$user",i.CreatedBy.ToString("N"));P(c,"$number",i.InvoiceNumber);P(c,"$discountPercentage",S(i.DiscountPercentage));P(c,"$tax",S(i.TaxAmount));P(c,"$customer",i.CustomerName);P(c,"$store",i.StoreName);P(c,"$currency",i.Currency);P(c,"$issuedBy",i.CreatedBy.ToString("N"));await c.ExecuteNonQueryAsync(ct);foreach(var l in lines){c.Parameters.Clear();c.CommandText="INSERT INTO InvoiceLines(Id,InvoiceId,ProductId,Description,Quantity,Unit,UnitPrice,Discount,LineTotal,Barcode,UnitSelected,ConversionFactorApplied,QuantityMinor,CostPriceSnapshot) VALUES($id,$invoice,$product,$desc,$qty,$unit,$price,$discount,$total,$barcode,$unitSelected,$factor,$quantityMinor,$costSnapshot);";P(c,"$id",l.Id.ToString("N"));P(c,"$invoice",l.InvoiceId.ToString("N"));P(c,"$product",l.ProductId.ToString("N"));P(c,"$desc",l.Description);P(c,"$qty",S(l.Quantity));P(c,"$unit",l.Unit);P(c,"$price",S(l.UnitPrice));P(c,"$discount",S(l.Discount));P(c,"$total",S(l.LineTotal));P(c,"$barcode",l.Barcode);P(c,"$unitSelected",string.IsNullOrWhiteSpace(l.UnitSelected)?l.Unit:l.UnitSelected);P(c,"$factor",S(l.ConversionFactorApplied));P(c,"$quantityMinor",S(l.QuantityMinor));P(c,"$costSnapshot",S(l.CostPriceSnapshot));await c.ExecuteNonQueryAsync(ct);}await tx.CommitAsync(ct);await audit.LogActionAsync(i.CreatedBy, "INVOICE_CREATE", "Invoice", i.Id,new { i.Type,i.PersonId,i.Total,i.Paid,i.PaymentMode },ct);return i;}
    public Task<Invoice> SaveSalesInvoiceAsync(Invoice invoice,IReadOnlyList<InvoiceLine> lines,IReadOnlyList<StockMovement> movements,LedgerOperation? operation,CancellationToken ct=default)=>CreateInvoiceTransactionWithMovementsAsync(invoice,lines,movements,operation,ct);
    public Task<Invoice> CreateInvoiceTransactionAsync(Invoice i,IReadOnlyList<InvoiceLine> lines,StockMovement? movement,LedgerOperation? operation,CancellationToken ct=default)=>CreateInvoiceTransactionWithMovementsAsync(i,lines,movement is null?Array.Empty<StockMovement>():new[]{movement!},operation,ct);
    public async Task<Invoice> CreateInvoiceTransactionWithMovementsAsync(Invoice i,IReadOnlyList<InvoiceLine> lines,IReadOnlyList<StockMovement> movements,LedgerOperation? operation,CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.Create,ct);
        if(i.Type is not (InvoiceType.Sale or InvoiceType.Purchase))throw new InvalidOperationException("نوع الفاتورة غير مدعوم في مسار البيع والشراء؛ استخدم مسار المرتجعات المخصص.");
        if(i.Subtotal<0||i.Discount<0||i.TaxAmount<0||i.Discount>i.Subtotal||i.Total<=0||i.Paid<0||i.Paid>i.Total)throw new ArgumentOutOfRangeException(nameof(i),"قيم الفاتورة غير منطقية.");
        if(decimal.Round(i.Subtotal-i.Discount+i.TaxAmount,2)!=decimal.Round(i.Total,2))throw new InvalidOperationException("إجمالي الفاتورة لا يطابق الإجمالي بعد الخصم والضريبة.");
        if(i.PaymentMode==PaymentMode.Cash&&i.Paid!=i.Total)throw new InvalidOperationException("الفاتورة النقدية يجب أن تكون مسددة بالكامل.");
        if(i.PaymentMode==PaymentMode.Credit&&i.Paid!=0)throw new InvalidOperationException("الفاتورة الآجلة يجب ألا تحتوي دفعة مقدمة.");
        if(i.PaymentMode==PaymentMode.Partial&&(i.Paid<=0||i.Paid>=i.Total))throw new InvalidOperationException("السداد الجزئي يجب أن يكون أكبر من صفر وأقل من الإجمالي.");
        if(i.PaymentMode!=PaymentMode.Cash&&i.PersonId is null)throw new InvalidOperationException("الفاتورة الآجلة أو الجزئية تحتاج إلى حساب مرتبط.");
        if(lines.Count==0)throw new InvalidOperationException("الفاتورة يجب أن تحتوي على صنف واحد على الأقل.");
        if(lines.Any(l=>l.Quantity<=0||l.UnitPrice<=0||l.Discount<0||l.Discount>l.Quantity*l.UnitPrice||l.LineTotal!=decimal.Round(l.Quantity*l.UnitPrice-l.Discount,2)))throw new InvalidOperationException("بيانات كمية أو سعر أو خصم أحد بنود الفاتورة غير صالحة.");
        if(decimal.Round(lines.Sum(l=>l.Quantity*l.UnitPrice),2)!=i.Subtotal||decimal.Round(lines.Sum(l=>l.Discount),2)!=i.Discount||decimal.Round(lines.Sum(l=>l.LineTotal),2)!=decimal.Round(i.Subtotal-i.Discount,2))throw new InvalidOperationException("إجمالي الفاتورة لا يطابق مجموع بنودها وخصوماتها.");
        if(movements.Count>0&&movements.Count!=lines.Count)throw new InvalidOperationException("عدد حركات المخزون يجب أن يطابق عدد بنود الفاتورة.");
        var expectedMovementType=i.Type==InvoiceType.Sale?StockMovementType.Sale:StockMovementType.Purchase;
        if(movements.Any(m=>m.Type!=expectedMovementType))throw new InvalidOperationException("نوع حركة المخزون لا يطابق نوع الفاتورة.");
        var outstanding=decimal.Round(i.Total-i.Paid,2);
        if(outstanding>0&&operation is null)throw new InvalidOperationException("الفاتورة غير المسددة تحتاج إلى قيد مالي مرتبط بالحساب.");
        if(operation is not null&&decimal.Round(operation.Amount,2)!=outstanding)throw new InvalidOperationException("قيمة القيد المالي يجب أن تساوي المتبقي من الفاتورة.");
        var lineTotal=0m;
        foreach(var l in lines){if(l.InvoiceId!=i.Id||l.Quantity<=0||l.UnitPrice<0||l.Discount<0||l.LineTotal<0)throw new ArgumentException("بيانات سطر الفاتورة غير صالحة.",nameof(lines));lineTotal+=l.LineTotal;}
        if(decimal.Round(lineTotal,2)!=decimal.Round(i.Subtotal-i.Discount,2))throw new InvalidOperationException("مجموع بنود الفاتورة لا يطابق الإجمالي بعد الخصم.");
        if(movements.Any(m=>m.QuantitySmallUnits<=0||m.UnitPrice<0))throw new ArgumentException("بيانات حركة المخزون غير صالحة.",nameof(movements));
        if(operation is not null&&(operation.Amount<=0||operation.PersonId!=i.PersonId))throw new ArgumentException("بيانات القيد المالي غير صالحة.",nameof(operation));
        await using var db=database.Open(); await db.OpenAsync(ct); await using var tx=await db.BeginTransactionAsync(ct); await using var c=db.CreateCommand(); c.Transaction=(SqliteTransaction)tx;
        if(i.PersonId is Guid person&&!await ExistsAsync(c,"People",person,ct))throw new InvalidOperationException("الحساب المرتبط بالفاتورة غير موجود.");
        if(!await ExistsAsync(c,"Users",i.CreatedBy,ct))throw new InvalidOperationException("المستخدم المنفذ للعملية غير موجود.");
        foreach(var movement in movements)if(!await ExistsAsync(c,"Products",movement.ProductId,ct))throw new InvalidOperationException("الصنف المرتبط بالحركة غير موجود.");if(movements.Count>0){for(var index=0;index<lines.Count;index++){var line=lines[index];var movement=movements[index];if(movement.ProductId!=line.ProductId)throw new InvalidOperationException("حركة المخزون لا تطابق صنف بند الفاتورة.");c.Parameters.Clear();c.CommandText="SELECT SmallUnit,LargeUnit,ConversionFactor FROM Products WHERE Id=$product;";P(c,"$product",line.ProductId.ToString("N"));string smallUnit;string largeUnit;decimal factor;await using(var productReader=await c.ExecuteReaderAsync(ct)){if(!await productReader.ReadAsync(ct))throw new InvalidOperationException("الصنف المرتبط ببند الفاتورة غير موجود.");smallUnit=productReader.GetString(0);largeUnit=productReader.GetString(1);factor=D(Convert.ToString(productReader.GetValue(2),System.Globalization.CultureInfo.InvariantCulture)!);}if(!string.Equals(line.Unit,smallUnit,StringComparison.OrdinalIgnoreCase)&&!string.Equals(line.Unit,largeUnit,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("وحدة بند الفاتورة لا تطابق وحدات الصنف.");var usesLargeUnit=!string.Equals(smallUnit,largeUnit,StringComparison.OrdinalIgnoreCase)&&string.Equals(line.Unit,largeUnit,StringComparison.OrdinalIgnoreCase);var expectedQuantity=line.Quantity*(usesLargeUnit?factor:1m);var expectedMovementPrice=decimal.Round(line.UnitPrice/(usesLargeUnit?factor:1m),4);if(decimal.Round(expectedQuantity,4)!=decimal.Round(movement.QuantitySmallUnits,4)||decimal.Round(expectedMovementPrice,4)!=decimal.Round(movement.UnitPrice,4))throw new InvalidOperationException("كمية أو سعر حركة المخزون لا يطابق وحدة وسعر بند الفاتورة.");}}
        if(operation is not null&&!await ExistsAsync(c,"People",operation.PersonId,ct))throw new InvalidOperationException("الحساب المرتبط بالقيد غير موجود.");
        c.Parameters.Clear();c.CommandText="SELECT EXISTS(SELECT 1 FROM Invoices WHERE Id=$id);";P(c,"$id",i.Id.ToString("N"));if(Convert.ToInt32(await c.ExecuteScalarAsync(ct))==1)throw new InvalidOperationException("الفاتورة مسجلة مسبقاً.");
        if(string.IsNullOrWhiteSpace(i.InvoiceNumber))
        {
            var prefix=$"INV-{i.IssuedAt:yyyy}-";
            c.Parameters.Clear();c.CommandText="SELECT COALESCE(MAX(CAST(substr(InvoiceNumber,10) AS INTEGER)),0)+1 FROM Invoices WHERE InvoiceNumber LIKE $prefix||'%';";P(c,"$prefix",prefix);
            var nextNumber=Convert.ToInt32(await c.ExecuteScalarAsync(ct),CultureInfo.InvariantCulture);
            i=i with { InvoiceNumber=prefix+nextNumber.ToString("0000",CultureInfo.InvariantCulture) };
        }
        if(string.IsNullOrWhiteSpace(i.InvoiceNumber))throw new InvalidOperationException("تعذر إنشاء رقم الفاتورة.");
        c.Parameters.Clear();c.CommandText="SELECT EXISTS(SELECT 1 FROM Invoices WHERE trim(InvoiceNumber)=trim($number));";P(c,"$number",i.InvoiceNumber);
        if(Convert.ToInt32(await c.ExecuteScalarAsync(ct))==1)throw new InvalidOperationException("رقم الفاتورة مستخدم مسبقاً؛ أعد المحاولة لإنشاء رقم جديد.");
        foreach(var movement in movements){c.Parameters.Clear();c.CommandText="SELECT EXISTS(SELECT 1 FROM StockMovements WHERE Id=$id);";P(c,"$id",movement.Id.ToString("N"));if(Convert.ToInt32(await c.ExecuteScalarAsync(ct))==1)throw new InvalidOperationException("حركة المخزون مسجلة مسبقاً.");}
        if(operation is not null){c.Parameters.Clear();c.CommandText="SELECT EXISTS(SELECT 1 FROM Operations WHERE Id=$id);";P(c,"$id",operation.Id.ToString("N"));if(Convert.ToInt32(await c.ExecuteScalarAsync(ct))==1)throw new InvalidOperationException("القيد المالي مسجل مسبقاً.");}
        c.Parameters.Clear();c.CommandText="INSERT INTO Invoices(Id,Type,PersonId,IssuedAt,Subtotal,Discount,Total,Paid,PaymentMode,Note,CreatedBy,InvoiceNumber,DiscountPercentage,TaxAmount,CustomerName,StoreName,Currency,IssuedByUserId) VALUES($id,$type,$person,$date,$sub,$discount,$total,$paid,$mode,$note,$user,$number,$discountPercentage,$tax,$customer,$store,$currency,$issuedBy);";P(c,"$id",i.Id.ToString("N"));P(c,"$type",(int)i.Type);P(c,"$person",i.PersonId?.ToString("N")??(object)DBNull.Value);P(c,"$date",i.IssuedAt.ToString("O"));P(c,"$sub",S(i.Subtotal));P(c,"$discount",S(i.Discount));P(c,"$total",S(i.Total));P(c,"$paid",S(i.Paid));P(c,"$mode",(int)i.PaymentMode);P(c,"$note",i.Note);P(c,"$user",i.CreatedBy.ToString("N"));P(c,"$number",i.InvoiceNumber);P(c,"$discountPercentage",S(i.DiscountPercentage));P(c,"$tax",S(i.TaxAmount));P(c,"$customer",i.CustomerName);P(c,"$store",i.StoreName);P(c,"$currency",i.Currency);P(c,"$issuedBy",i.CreatedBy.ToString("N"));await c.ExecuteNonQueryAsync(ct);
        foreach(var l in lines){c.Parameters.Clear();c.CommandText="INSERT INTO InvoiceLines(Id,InvoiceId,ProductId,Description,Quantity,Unit,UnitPrice,Discount,LineTotal,Barcode,UnitSelected,ConversionFactorApplied,QuantityMinor,CostPriceSnapshot) VALUES($id,$invoice,$product,$desc,$qty,$unit,$price,$discount,$total,$barcode,$unitSelected,$factor,$quantityMinor,$costSnapshot);";P(c,"$id",l.Id.ToString("N"));P(c,"$invoice",l.InvoiceId.ToString("N"));P(c,"$product",l.ProductId.ToString("N"));P(c,"$desc",l.Description);P(c,"$qty",S(l.Quantity));P(c,"$unit",l.Unit);P(c,"$price",S(l.UnitPrice));P(c,"$discount",S(l.Discount));P(c,"$total",S(l.LineTotal));P(c,"$barcode",l.Barcode);P(c,"$unitSelected",string.IsNullOrWhiteSpace(l.UnitSelected)?l.Unit:l.UnitSelected);P(c,"$factor",S(l.ConversionFactorApplied));P(c,"$quantityMinor",S(l.QuantityMinor));P(c,"$costSnapshot",S(l.CostPriceSnapshot));await c.ExecuteNonQueryAsync(ct);}
        for(var movementIndex=0;movementIndex<movements.Count;movementIndex++){var movement=movements[movementIndex];c.Parameters.Clear();c.CommandText="SELECT StockSmallUnits FROM Products WHERE Id=$product;";P(c,"$product",movement.ProductId.ToString("N"));var current=D(Convert.ToString(await c.ExecuteScalarAsync(ct))!);var signed=movement.Type is StockMovementType.Sale or StockMovementType.ReturnOut or StockMovementType.AdjustmentOut?-movement.QuantitySmallUnits:movement.QuantitySmallUnits;var next=current+signed;if(next<0)throw new InvalidOperationException("الرصيد المخزني غير كافٍ.");c.Parameters.Clear();c.CommandText="INSERT INTO StockMovements(Id,ProductId,Type,QuantitySmallUnits,UnitPrice,Note,OccurredAt,CreatedBy,InvoiceId) VALUES($id,$product,$type,$qty,$price,$note,$when,$user,$invoice);";P(c,"$id",movement.Id.ToString("N"));P(c,"$product",movement.ProductId.ToString("N"));P(c,"$type",(int)movement.Type);P(c,"$qty",S(movement.QuantitySmallUnits));P(c,"$price",S(movement.UnitPrice));P(c,"$note",movement.Note);P(c,"$when",movement.OccurredAt.ToString("O"));P(c,"$user",movement.CreatedBy.ToString("N"));P(c,"$invoice",i.Id.ToString("N"));await c.ExecuteNonQueryAsync(ct);c.Parameters.Clear();c.CommandText="UPDATE Products SET StockSmallUnits=$stock WHERE Id=$product;";P(c,"$stock",S(next));P(c,"$product",movement.ProductId.ToString("N"));await c.ExecuteNonQueryAsync(ct);
            if(movement.Type==StockMovementType.Purchase)
            {
                c.Parameters.Clear();
                c.CommandText="INSERT INTO ProductBatches(Id,ProductId,BatchCode,CostPrice,QuantityMinor,ExpiryDate,SupplierId,PurchaseInvoiceRef,CreatedAt) VALUES($id,$product,$batch,$cost,$quantity,$expiry,$supplier,$invoice,$created);";
                var purchaseLine=lines[movementIndex];
                P(c,"$id",Guid.NewGuid().ToString("N"));P(c,"$product",movement.ProductId.ToString("N"));P(c,"$batch",string.IsNullOrWhiteSpace(purchaseLine.BatchNumber)?i.InvoiceNumber:purchaseLine.BatchNumber.Trim());P(c,"$cost",S(movement.UnitPrice));P(c,"$quantity",S(movement.QuantitySmallUnits));P(c,"$expiry",purchaseLine.ExpiryDate?.ToString("O")??(object)DBNull.Value);P(c,"$supplier",i.PersonId?.ToString("N")??(object)DBNull.Value);P(c,"$invoice",i.InvoiceNumber);P(c,"$created",i.IssuedAt.ToString("O"));
                await c.ExecuteNonQueryAsync(ct);
            }
            if(movement.Type==StockMovementType.Sale)
            {
                c.Parameters.Clear();c.CommandText="SELECT Id,QuantityMinor FROM ProductBatches WHERE ProductId=$product AND CAST(QuantityMinor AS REAL)>0 ORDER BY CASE WHEN ExpiryDate IS NULL OR trim(ExpiryDate)='' THEN 1 ELSE 0 END,ExpiryDate,CreatedAt;";P(c,"$product",movement.ProductId.ToString("N"));
                var batches=new List<(string Id,decimal Quantity)>();
                await using(var batchReader=await c.ExecuteReaderAsync(ct)){while(await batchReader.ReadAsync(ct))batches.Add((batchReader.GetString(0),D(Convert.ToString(batchReader.GetValue(1),CultureInfo.InvariantCulture)!)));}
                var remainingToAllocate=movement.QuantitySmallUnits;
                foreach(var batch in batches)
                {
                    if(remainingToAllocate<=0)break;
                    var taken=Math.Min(batch.Quantity,remainingToAllocate);
                    c.Parameters.Clear();c.CommandText="UPDATE ProductBatches SET QuantityMinor=$quantity WHERE Id=$id;";P(c,"$quantity",S(batch.Quantity-taken));P(c,"$id",batch.Id);await c.ExecuteNonQueryAsync(ct);
                    c.Parameters.Clear();c.CommandText="INSERT INTO InvoiceBatchAllocations(Id,InvoiceId,InvoiceLineId,ProductId,BatchId,QuantityMinor,CreatedAt) VALUES($id,$invoice,$line,$product,$batch,$quantity,$created);";
                    P(c,"$id",Guid.NewGuid().ToString("N"));P(c,"$invoice",i.Id.ToString("N"));P(c,"$line",lines[movementIndex].Id.ToString("N"));P(c,"$product",movement.ProductId.ToString("N"));P(c,"$batch",batch.Id);P(c,"$quantity",S(taken));P(c,"$created",i.IssuedAt.ToString("O"));await c.ExecuteNonQueryAsync(ct);
                    remainingToAllocate-=taken;
                }
            }
        }
        if(operation is not null){c.Parameters.Clear();c.CommandText="INSERT INTO Operations(Id,PersonId,Type,Amount,Note,OccurredAt,CreatedBy,ReferenceNumber) VALUES($id,$person,$type,$amount,$note,$when,$user,$reference);";P(c,"$id",operation.Id.ToString("N"));P(c,"$person",operation.PersonId.ToString("N"));P(c,"$type",(int)operation.Type);P(c,"$amount",S(operation.Amount));P(c,"$note",operation.Note);P(c,"$when",operation.OccurredAt.ToString("O"));P(c,"$user",operation.CreatedBy.ToString("N"));P(c,"$reference",(object?)operation.ReferenceNumber ?? DBNull.Value);await c.ExecuteNonQueryAsync(ct);}
        if(i.Paid>0)
        {
            c.Parameters.Clear();c.CommandText="INSERT INTO CashRegisterEntries(Id,InvoiceId,Amount,EntryType,OccurredAt,CreatedBy) VALUES($id,$invoice,$amount,$type,$when,$user);";
            P(c,"$id",Guid.NewGuid().ToString("N"));P(c,"$invoice",i.Id.ToString("N"));P(c,"$amount",S(i.Paid));P(c,"$type",i.Type==InvoiceType.Purchase?"PURCHASE_PAYMENT":"SALE_RECEIPT");P(c,"$when",i.IssuedAt.ToString("O"));P(c,"$user",i.CreatedBy.ToString("N"));await c.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);await audit.LogActionAsync(i.CreatedBy, "INVOICE_CREATE", "Invoice", i.Id,new { i.Type,i.PersonId,i.Total,i.Paid,i.PaymentMode,LineCount=lines.Count,i.InvoiceNumber },ct);return i;
    }
    private static string NormalizeIdentity(string value)=>string.Join(" ",value.Trim().ToLowerInvariant().Replace("أ","ا").Replace("إ","ا").Replace("آ","ا").Replace("ٱ","ا").Replace("ة","ه").Replace("ى","ي").Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));
    private async Task<bool> SupplierExistsAsync(Guid id,CancellationToken ct){await using var db=database.Open();await db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="SELECT EXISTS(SELECT 1 FROM People p JOIN Books b ON b.Id=p.BookId WHERE p.Id=$id AND b.Kind=2);";P(c,"$id",id.ToString("N"));return Convert.ToInt32(await c.ExecuteScalarAsync(ct))==1;}
    public async Task<IReadOnlyList<PersonSummary>> GetSupplierPeopleAsync(string? search=null,CancellationToken ct=default)=>await GetPeopleAsync(new Guid("00000000000000000000000000000003"),search,null,ct);

    public async Task<Product?> GetProductByBarcodeAsync(string barcode,CancellationToken ct=default)
    {
        var normalized=(barcode??string.Empty).Trim();
        if(normalized.Length==0)return null;
        var products=await GetProductsAsync(normalized,ct);
        return products.FirstOrDefault(p=>string.Equals(p.Barcode.Trim(),normalized,StringComparison.OrdinalIgnoreCase));
    }
    public async Task<ProductBatch> AddStockBatchAsync(ProductBatch batch,CancellationToken ct=default)
    {
        await EnsurePermissionAsync(PermissionKeys.Create,ct);
        if(batch.QuantityMinor<=0||batch.CostPrice<0)throw new ArgumentOutOfRangeException(nameof(batch),"الكمية يجب أن تكون موجبة والتكلفة غير سالبة.");
        await using var db=database.Open();await db.OpenAsync(ct);await using var tx=await db.BeginTransactionAsync(ct);await using var c=db.CreateCommand();c.Transaction=(SqliteTransaction)tx;
        c.CommandText="SELECT StockSmallUnits FROM Products WHERE Id=$id;";P(c,"$id",batch.ProductId.ToString("N"));var currentRaw=await c.ExecuteScalarAsync(ct);if(currentRaw is null)throw new InvalidOperationException("الصنف غير موجود.");
        var current=D(Convert.ToString(currentRaw,CultureInfo.InvariantCulture)??"0");
        c.Parameters.Clear();c.CommandText="INSERT INTO ProductBatches(Id,ProductId,BatchCode,CostPrice,QuantityMinor,ExpiryDate,SupplierId,PurchaseInvoiceRef,CreatedAt) VALUES($id,$product,$code,$cost,$qty,$expiry,$supplier,$invoice,$created);";
        P(c,"$id",batch.Id.ToString("N"));P(c,"$product",batch.ProductId.ToString("N"));P(c,"$code",batch.BatchNumber);P(c,"$cost",S(batch.CostPrice));P(c,"$qty",S(batch.QuantityMinor));P(c,"$expiry",batch.ExpiryDate?.ToString("O")??(object)DBNull.Value);P(c,"$supplier",batch.SupplierId?.ToString("N")??(object)DBNull.Value);P(c,"$invoice",batch.PurchaseInvoiceRef??(object)DBNull.Value);P(c,"$created",batch.CreatedAt.ToString("O"));await c.ExecuteNonQueryAsync(ct);
        c.Parameters.Clear();c.CommandText="UPDATE Products SET StockSmallUnits=$stock,UpdatedAt=$updated WHERE Id=$id;";P(c,"$stock",S(current+batch.QuantityMinor));P(c,"$updated",DateTimeOffset.UtcNow.ToString("O"));P(c,"$id",batch.ProductId.ToString("N"));await c.ExecuteNonQueryAsync(ct);if(appSession?.CurrentUser is AppUser stockUser){c.Parameters.Clear();c.CommandText="INSERT INTO StockMovements(Id,ProductId,Type,QuantitySmallUnits,UnitPrice,Note,OccurredAt,CreatedBy,InvoiceId) VALUES($id,$product,$type,$qty,$price,$note,$when,$user,NULL);";P(c,"$id",Guid.NewGuid().ToString("N"));P(c,"$product",batch.ProductId.ToString("N"));P(c,"$type",(int)StockMovementType.Purchase);P(c,"$qty",S(batch.QuantityMinor));P(c,"$price",S(batch.CostPrice));P(c,"$note","استلام دفعة "+batch.BatchNumber);P(c,"$when",batch.CreatedAt.ToString("O"));P(c,"$user",stockUser.Id.ToString("N"));await c.ExecuteNonQueryAsync(ct);}await tx.CommitAsync(ct);
        await audit.LogActionAsync(appSession?.CurrentUser?.Id,"STOCK_BATCH_ADD","ProductBatch",batch.Id,new { batch.ProductId,batch.BatchNumber,batch.QuantityMinor,batch.CostPrice },ct);return batch;
    }
    public async Task<IReadOnlyList<ProductBatch>> GetProductBatchesAsync(Guid productId,CancellationToken ct=default)
    {
        await EnsurePermissionAsync(PermissionKeys.View,ct);var result=new List<ProductBatch>();await using var db=database.Open();await db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="SELECT Id,ProductId,BatchCode,ExpiryDate,QuantityMinor,CostPrice,SupplierId,PurchaseInvoiceRef,CreatedAt FROM ProductBatches WHERE ProductId=$id AND CAST(QuantityMinor AS REAL)>0 ORDER BY CASE WHEN ExpiryDate IS NULL THEN 1 ELSE 0 END,ExpiryDate,CreatedAt;";P(c,"$id",productId.ToString("N"));await using var r=await c.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))result.Add(new ProductBatch(Guid.Parse(r.GetString(0)),Guid.Parse(r.GetString(1)),r.GetString(2),r.IsDBNull(3)?null:DateTimeOffset.Parse(r.GetString(3)),D(r.GetString(4)),D(r.GetString(5)),r.IsDBNull(6)?null:Guid.Parse(r.GetString(6)),r.IsDBNull(7)?null:r.GetString(7),DateTimeOffset.Parse(r.GetString(8))));return result;
    }
    public async Task<IReadOnlyList<StockMovement>> GetStockMovementsAsync(Guid productId,int take=200,CancellationToken ct=default)
    {
        await EnsurePermissionAsync(PermissionKeys.View,ct);var result=new List<StockMovement>();await using var db=database.Open();await db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="SELECT Id,ProductId,Type,QuantitySmallUnits,UnitPrice,Note,OccurredAt,CreatedBy,InvoiceId FROM StockMovements WHERE ProductId=$id ORDER BY OccurredAt DESC LIMIT $take;";P(c,"$id",productId.ToString("N"));P(c,"$take",Math.Clamp(take,1,1000));await using var r=await c.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))result.Add(new StockMovement(Guid.Parse(r.GetString(0)),Guid.Parse(r.GetString(1)),(StockMovementType)r.GetInt32(2),D(r.GetString(3)),D(r.GetString(4)),r.GetString(5),DateTimeOffset.Parse(r.GetString(6)),Guid.Parse(r.GetString(7))){InvoiceId=r.IsDBNull(8)?null:Guid.Parse(r.GetString(8))});return result;
    }

    public async Task<IReadOnlyList<ProductMovementRow>> GetProductMovementsAsync(Guid productId,int take=30,CancellationToken ct=default)
    {
        await EnsurePermissionAsync(PermissionKeys.View,ct);var x=new List<ProductMovementRow>();await using var db=database.Open();await db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="SELECT OccurredAt,Type,QuantitySmallUnits,Note,InvoiceId FROM StockMovements WHERE ProductId=$p ORDER BY OccurredAt,Id;";P(c,"$p",productId.ToString("N"));await using var r=await c.ExecuteReaderAsync(ct);var all=new List<(DateTimeOffset At,int Type,decimal Qty,string Note,Guid? Invoice)>();while(await r.ReadAsync(ct))all.Add((DateTimeOffset.Parse(r.GetString(0)),r.GetInt32(1),D(r.GetString(2)),r.GetString(3),r.IsDBNull(4)?null:Guid.Parse(r.GetString(4))));var running=0m;foreach(var m in all){var inbound=m.Type is (int)StockMovementType.Purchase or (int)StockMovementType.ReturnIn or (int)StockMovementType.AdjustmentIn or (int)StockMovementType.Inventory;running+=inbound?m.Qty:-m.Qty;var label=m.Type switch{(int)StockMovementType.Purchase=>"شراء وارد",(int)StockMovementType.Sale=>"بيع منصرف",(int)StockMovementType.ReturnIn=>"مرتجع وارد",(int)StockMovementType.ReturnOut=>"مرتجع منصرف",(int)StockMovementType.AdjustmentIn=>"تسوية وارد",(int)StockMovementType.AdjustmentOut=>"تسوية منصرف",(int)StockMovementType.Inventory=>"رصيد افتتاحي",_=>"حركة مخزون"};x.Add(new ProductMovementRow(m.At,label,m.Qty,running,m.Note,m.Invoice));}return x.OrderByDescending(v=>v.OccurredAt).Take(Math.Clamp(take,1,200)).ToList();
    }
    private static async Task<bool> ExistsAsync(SqliteCommand c,string table,Guid id,CancellationToken ct){c.Parameters.Clear();c.CommandText=$"SELECT EXISTS(SELECT 1 FROM {table} WHERE Id=$id);";P(c,"$id",id.ToString("N"));return Convert.ToInt32(await c.ExecuteScalarAsync(ct))==1;}
    public async Task<Expense> CreateExpenseAsync(Expense e,CancellationToken ct=default)
    {
        await EnsurePermissionAsync(PermissionKeys.Create,ct);
        if(e.Amount<=0)throw new ArgumentOutOfRangeException(nameof(e),"مبلغ النفقة يجب أن يكون أكبر من صفر.");
        if(string.IsNullOrWhiteSpace(e.Category))throw new ArgumentException("تصنيف النفقة مطلوب.",nameof(e));
        await using var db=database.Open();
        await db.OpenAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        await using var c=db.CreateCommand();
        c.Transaction=(SqliteTransaction)tx;
        c.CommandText="INSERT INTO Expenses(Id,PersonId,Category,Amount,Note,OccurredAt,CreatedBy,CategoryId,PaymentMethod) VALUES($id,$person,$cat,$amount,$note,$when,$user,$categoryId,$paymentMethod);";
        P(c,"$id",e.Id.ToString("N"));P(c,"$person",e.PersonId?.ToString("N")??(object)DBNull.Value);P(c,"$cat",e.Category.Trim());P(c,"$amount",S(decimal.Round(e.Amount,2)));P(c,"$note",e.Note);P(c,"$when",e.OccurredAt.ToString("O"));P(c,"$user",e.CreatedBy.ToString("N"));P(c,"$categoryId",string.IsNullOrWhiteSpace(e.CategoryId)?e.Category.Trim():e.CategoryId.Trim());P(c,"$paymentMethod",string.IsNullOrWhiteSpace(e.PaymentMethod)?"Cash":e.PaymentMethod.Trim());
        await c.ExecuteNonQueryAsync(ct);
        c.Parameters.Clear();
        c.CommandText="INSERT INTO ExpenseCashEntries(Id,ExpenseId,Amount,EntryType,OccurredAt,CreatedBy) VALUES($id,$expense,$amount,$type,$when,$user);";
        P(c,"$id",Guid.NewGuid().ToString("N"));P(c,"$expense",e.Id.ToString("N"));P(c,"$amount",S(decimal.Round(e.Amount,2)));P(c,"$type","EXPENSE_PAYMENT:"+(string.IsNullOrWhiteSpace(e.PaymentMethod)?"Cash":e.PaymentMethod.Trim()));P(c,"$when",e.OccurredAt.ToString("O"));P(c,"$user",e.CreatedBy.ToString("N"));
        await c.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        await audit.LogActionAsync(e.CreatedBy,"EXPENSE_CREATE","Expense",e.Id,new { e.Amount,e.Category,e.PaymentMethod },ct);
        return e;
    }
    public async Task<IReadOnlyList<Expense>> GetExpensesAsync(DateTimeOffset? from=null,DateTimeOffset? toDate=null,CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.View,ct);var x=new List<Expense>();await using var db=database.Open();await db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="SELECT Id,PersonId,Category,Amount,Note,OccurredAt,CreatedBy,CategoryId,PaymentMethod FROM Expenses WHERE($f IS NULL OR OccurredAt>=$f) AND($t IS NULL OR OccurredAt<=$t) ORDER BY OccurredAt DESC;";P(c,"$f",from?.ToString("O")??(object)DBNull.Value);P(c,"$t",toDate?.ToString("O")??(object)DBNull.Value);await using var r=await c.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))x.Add(new Expense(Guid.Parse(r.GetString(0)),r.IsDBNull(1)?null:Guid.Parse(r.GetString(1)),r.GetString(2),D(r.GetString(3)),r.GetString(4),DateTimeOffset.Parse(r.GetString(5)),Guid.Parse(r.GetString(6))){CategoryId=r.GetString(7),PaymentMethod=r.GetString(8)});return x;}
    public async Task<IReadOnlyList<RecentOperationRow>> GetRecentOperationsAsync(int take=12,CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.View,ct);var x=new List<RecentOperationRow>();await using var db=database.Open();await db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="SELECT o.OccurredAt,p.Name,CASE o.Type WHEN 0 THEN 'استلام' WHEN 1 THEN 'سداد' WHEN 2 THEN 'له' WHEN 3 THEN 'عليه' WHEN 4 THEN 'بيع آجل' WHEN 5 THEN 'شراء آجل' WHEN 6 THEN 'مرتجع' WHEN 7 THEN 'تسوية' ELSE 'أخرى' END,o.Amount,o.Note FROM Operations o JOIN People p ON p.Id=o.PersonId ORDER BY o.OccurredAt DESC LIMIT $take;";P(c,"$take",Math.Clamp(take,1,100));await using var r=await c.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))x.Add(new RecentOperationRow(DateTimeOffset.Parse(r.GetString(0)),r.GetString(1),r.GetString(2),D(r.GetString(3)),r.GetString(4)));return x;}
    public async Task<IReadOnlyList<GlobalSearchResult>> SearchGlobalAsync(string query,int takePerCategory=8,CancellationToken ct=default)
    {
        await EnsurePermissionAsync(PermissionKeys.View,ct);
        var q=(query??string.Empty).Trim();
        if(q.Length<2) return Array.Empty<GlobalSearchResult>();
        var take=Math.Clamp(takePerCategory,1,20);
        var results=new List<GlobalSearchResult>();
        await using var db=database.Open();
        await db.OpenAsync(ct);
        await using var c=db.CreateCommand();
        P(c,"$q",$"%{q}%");
        P(c,"$take",take);

        c.CommandText="SELECT Id,Name,Phone FROM People WHERE Name LIKE $q OR Phone LIKE $q ORDER BY Name LIMIT $take;";
        await using(var r=await c.ExecuteReaderAsync(ct))
            while(await r.ReadAsync(ct)) results.Add(new GlobalSearchResult(Guid.Parse(r.GetString(0)),"الحسابات",r.GetString(1),r.GetString(2),"Person"));

        c.Parameters.Clear(); P(c,"$q",$"%{q}%"); P(c,"$take",take);
        c.CommandText="SELECT Id,Name,Barcode FROM Products WHERE Name LIKE $q OR Barcode LIKE $q ORDER BY Name LIMIT $take;";
        await using(var r=await c.ExecuteReaderAsync(ct))
            while(await r.ReadAsync(ct)) results.Add(new GlobalSearchResult(Guid.Parse(r.GetString(0)),"المنتجات",r.GetString(1),r.GetString(2),"Product"));

        c.Parameters.Clear(); P(c,"$q",$"%{q}%"); P(c,"$take",take);
        c.CommandText="SELECT Id,Name,'' FROM Books WHERE Name LIKE $q ORDER BY SortOrder,Name LIMIT $take;";
        await using(var r=await c.ExecuteReaderAsync(ct))
            while(await r.ReadAsync(ct)) results.Add(new GlobalSearchResult(Guid.Parse(r.GetString(0)),"الدفاتر",r.GetString(1),"","Book"));

        c.Parameters.Clear(); P(c,"$q",$"%{q}%"); P(c,"$take",take);
        c.CommandText="SELECT o.Id,p.Name,o.Note,CASE o.Type WHEN 0 THEN 'استلام' WHEN 1 THEN 'سداد' WHEN 2 THEN 'له' WHEN 3 THEN 'عليه' WHEN 4 THEN 'بيع آجل' WHEN 5 THEN 'شراء آجل' WHEN 6 THEN 'مرتجع' WHEN 7 THEN 'تسوية' ELSE 'أخرى' END FROM Operations o JOIN People p ON p.Id=o.PersonId WHERE p.Name LIKE $q OR o.Note LIKE $q ORDER BY o.OccurredAt DESC LIMIT $take;";
        await using(var r=await c.ExecuteReaderAsync(ct))
            while(await r.ReadAsync(ct)) results.Add(new GlobalSearchResult(Guid.Parse(r.GetString(0)),"العمليات",r.GetString(1),r.GetString(3)+" — "+r.GetString(2),"Operation"));

        c.Parameters.Clear(); P(c,"$q",$"%{q}%"); P(c,"$take",take);
        c.CommandText="SELECT Id,Type,Note,Total FROM Invoices WHERE Note LIKE $q OR CAST(Total AS TEXT) LIKE $q ORDER BY IssuedAt DESC LIMIT $take;";
        await using(var r=await c.ExecuteReaderAsync(ct))
            while(await r.ReadAsync(ct))
            {
                var type=r.GetInt32(1)==0?"فاتورة بيع":"فاتورة شراء";
                results.Add(new GlobalSearchResult(Guid.Parse(r.GetString(0)),"الفواتير",type,AmountFormatterText(r.GetString(3))+" — "+r.GetString(2),"Invoice"));
            }

        c.Parameters.Clear(); P(c,"$q",$"%{q}%"); P(c,"$take",take);
        c.CommandText="SELECT Id,Category,Note,Amount FROM Expenses WHERE Category LIKE $q OR Note LIKE $q OR CAST(Amount AS TEXT) LIKE $q ORDER BY OccurredAt DESC LIMIT $take;";
        await using(var r=await c.ExecuteReaderAsync(ct))
            while(await r.ReadAsync(ct)) results.Add(new GlobalSearchResult(Guid.Parse(r.GetString(0)),"المصروفات",r.GetString(1),AmountFormatterText(r.GetString(3))+" — "+r.GetString(2),"Expense"));

        return results;
    }

    private static string AmountFormatterText(string value)
        => decimal.TryParse(value,NumberStyles.Any,CultureInfo.InvariantCulture,out var d) ? d.ToString("#,##0.##",CultureInfo.InvariantCulture) : value;

    public async Task<ReportSummary> GetReportSummaryAsync(DateTimeOffset from,DateTimeOffset toDate,CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.Reports,ct);await using var db=database.Open();await db.OpenAsync(ct);await using var c=db.CreateCommand();P(c,"$f",from.ToString("O"));P(c,"$t",toDate.ToString("O"));c.CommandText="SELECT COALESCE(SUM(CASE WHEN Type=0 THEN CAST(Total AS REAL) ELSE 0 END),0),COALESCE(SUM(CASE WHEN Type=1 THEN CAST(Total AS REAL) ELSE 0 END),0) FROM Invoices WHERE IssuedAt BETWEEN $f AND $t;";await using var r=await c.ExecuteReaderAsync(ct);await r.ReadAsync(ct);var sales=Convert.ToDecimal(r.GetValue(0));var purchases=Convert.ToDecimal(r.GetValue(1));await r.DisposeAsync();c.CommandText="SELECT COALESCE(SUM(CAST(Amount AS REAL)),0) FROM Expenses WHERE OccurredAt BETWEEN $f AND $t;";var expenses=Convert.ToDecimal(await c.ExecuteScalarAsync(ct));c.CommandText="SELECT COALESCE(SUM(CASE WHEN Type IN(3,4) THEN CAST(Amount AS REAL) WHEN Type IN(0,6) THEN -CAST(Amount AS REAL) ELSE 0 END),0),COALESCE(SUM(CASE WHEN Type IN(2,5) THEN CAST(Amount AS REAL) WHEN Type=1 THEN -CAST(Amount AS REAL) ELSE 0 END),0) FROM Operations WHERE OccurredAt<=$t;";await using var rr=await c.ExecuteReaderAsync(ct);await rr.ReadAsync(ct);var rec=rr.GetDecimal(0);var pay=rr.GetDecimal(1);await rr.DisposeAsync();c.CommandText="SELECT COALESCE(SUM(CAST(StockSmallUnits AS REAL)*CAST(PurchasePrice AS REAL)),0) FROM Products;";var stock=Convert.ToDecimal(await c.ExecuteScalarAsync(ct));return new ReportSummary(sales,purchases,expenses,rec,pay,stock);}
    public async Task<BusinessIdentity> GetIdentityAsync(CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.View,ct);await using var db=database.Open();await db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="SELECT BusinessName,LogoPath,Phone,Address,Currency,TaxRegistrationNumber,CommercialRegistrationNumber,InvoiceTerms FROM BusinessIdentity WHERE Id=1;";await using var r=await c.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return new BusinessIdentity("الدفتر الذكي","","","","ريال");return new BusinessIdentity(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4)){TaxRegistrationNumber=r.GetString(5),CommercialRegistrationNumber=r.GetString(6),InvoiceTerms=r.GetString(7)};}
    public async Task SaveIdentityAsync(BusinessIdentity i,CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.Settings,ct);await using var db=database.Open();await db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="UPDATE BusinessIdentity SET BusinessName=$n,LogoPath=$l,Phone=$p,Address=$a,Currency=$c,TaxRegistrationNumber=$tax,CommercialRegistrationNumber=$commercial,InvoiceTerms=$terms WHERE Id=1;";P(c,"$n",i.BusinessName);P(c,"$l",i.LogoPath);P(c,"$p",i.Phone);P(c,"$a",i.Address);P(c,"$c",i.Currency);P(c,"$tax",i.TaxRegistrationNumber);P(c,"$commercial",i.CommercialRegistrationNumber);P(c,"$terms",i.InvoiceTerms);await c.ExecuteNonQueryAsync(ct);await audit.LogActionAsync(appSession?.CurrentUser?.Id, "SETTINGS_UPDATE", "Settings", null,new { i.BusinessName,i.Phone,i.Address,i.Currency,i.TaxRegistrationNumber,i.CommercialRegistrationNumber },ct);}    public async Task<IReadOnlyList<DeletedItem>> GetDeletedItemsAsync(CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.View,ct);var x=new List<DeletedItem>();await using var db=database.Open();await db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="SELECT Id,EntityType,EntityId,DeletedAt,DeletedBy,Payload FROM DeletedItems ORDER BY DeletedAt DESC;";await using var r=await c.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))x.Add(new DeletedItem(Guid.Parse(r.GetString(0)),r.GetString(1),Guid.Parse(r.GetString(2)),DateTimeOffset.Parse(r.GetString(3)),Guid.Parse(r.GetString(4)),r.GetString(5)));return x;}
    public async Task RestoreDeletedItemAsync(Guid id,CancellationToken ct=default)
    {
        await EnsurePermissionAsync(PermissionKeys.Delete,ct);
        await using var db=database.Open(); await db.OpenAsync(ct);
        await using var tx=await db.BeginTransactionAsync(ct);
        await using var c=db.CreateCommand(); c.Transaction=(SqliteTransaction)tx;
        c.CommandText="SELECT EntityType,EntityId,Payload FROM DeletedItems WHERE Id=$id;";
        P(c,"$id",id.ToString("N"));
        await using var r=await c.ExecuteReaderAsync(ct);
        if(!await r.ReadAsync(ct)) throw new InvalidOperationException("العنصر غير موجود في سلة المحذوفات.");
        var type=r.GetString(0); var entityId=r.GetString(1); var payload=r.GetString(2);
        await r.DisposeAsync();

        c.Parameters.Clear();
        string insertSql=type switch
        {
            "Person"=>"INSERT INTO People(Id,BookId,Name,Phone,Note,CreatedAt,AccountCode,CreditLimit,Address,SecondaryPhone,AccountNature,WhatsAppEnabled,UpdatedAt) SELECT json_extract($p,'$.Id'),json_extract($p,'$.BookId'),json_extract($p,'$.Name'),json_extract($p,'$.Phone'),json_extract($p,'$.Note'),json_extract($p,'$.CreatedAt'),COALESCE(json_extract($p,'$.AccountCode'),'ACC-'||upper(substr(hex(randomblob(8)),1,12))),json_extract($p,'$.CreditLimit'),COALESCE(json_extract($p,'$.Address'),''),COALESCE(json_extract($p,'$.SecondaryPhone'),''),COALESCE(json_extract($p,'$.AccountNature'),'Flexible'),COALESCE(json_extract($p,'$.WhatsAppEnabled'),0),COALESCE(json_extract($p,'$.UpdatedAt'),json_extract($p,'$.CreatedAt')) WHERE NOT EXISTS(SELECT 1 FROM People WHERE Id=json_extract($p,'$.Id'));",
            "Book"=>"INSERT INTO Books(Id,Name,Kind,SortOrder,IsSystem,Color,Icon) SELECT json_extract($p,'$.Id'),json_extract($p,'$.Name'),json_extract($p,'$.Kind'),json_extract($p,'$.SortOrder'),json_extract($p,'$.IsSystem'),json_extract($p,'$.Color'),json_extract($p,'$.Icon') WHERE NOT EXISTS(SELECT 1 FROM Books WHERE Id=json_extract($p,'$.Id'));",
            "Operation"=>"INSERT INTO Operations(Id,PersonId,Type,Amount,Note,OccurredAt,CreatedBy,ReferenceNumber) SELECT json_extract($p,'$.Id'),json_extract($p,'$.PersonId'),json_extract($p,'$.Type'),json_extract($p,'$.Amount'),json_extract($p,'$.Note'),json_extract($p,'$.OccurredAt'),json_extract($p,'$.CreatedBy'),json_extract($p,'$.ReferenceNumber') WHERE NOT EXISTS(SELECT 1 FROM Operations WHERE Id=json_extract($p,'$.Id'));",
            "Product"=>"INSERT INTO Products(Id,Name,Barcode,Category,LargeUnit,SmallUnit,ConversionFactor,PurchasePrice,SalePrice,StockSmallUnits,AlertThreshold,CreatedAt,DefaultSupplierId,Note,ShelfLocation) SELECT json_extract($p,'$.Id'),json_extract($p,'$.Name'),json_extract($p,'$.Barcode'),json_extract($p,'$.Category'),json_extract($p,'$.LargeUnit'),json_extract($p,'$.SmallUnit'),json_extract($p,'$.ConversionFactor'),json_extract($p,'$.PurchasePrice'),json_extract($p,'$.SalePrice'),json_extract($p,'$.StockSmallUnits'),json_extract($p,'$.AlertThreshold'),json_extract($p,'$.CreatedAt'),json_extract($p,'$.DefaultSupplierId'),json_extract($p,'$.Note'),json_extract($p,'$.ShelfLocation') WHERE NOT EXISTS(SELECT 1 FROM Products WHERE Id=json_extract($p,'$.Id'));",
            "StockMovement"=>"INSERT INTO StockMovements(Id,ProductId,Type,QuantitySmallUnits,UnitPrice,Note,OccurredAt,CreatedBy) SELECT json_extract($p,'$.Id'),json_extract($p,'$.ProductId'),json_extract($p,'$.Type'),json_extract($p,'$.QuantitySmallUnits'),json_extract($p,'$.UnitPrice'),json_extract($p,'$.Note'),json_extract($p,'$.OccurredAt'),json_extract($p,'$.CreatedBy') WHERE NOT EXISTS(SELECT 1 FROM StockMovements WHERE Id=json_extract($p,'$.Id'));",
            _=>throw new InvalidOperationException("نوع العنصر غير مدعوم للاستعادة.")
        };
        c.Parameters.Clear();
        c.Parameters.AddWithValue("@payload",payload);
        c.CommandText=insertSql.Replace("$p","@payload");
        var restored=await c.ExecuteNonQueryAsync(ct);
        if(restored==0) throw new InvalidOperationException("تعذر الاستعادة لأن العنصر موجود بالفعل أو توجد مشكلة في البيانات.");

        if(type=="StockMovement")
        {
            c.Parameters.Clear();
            c.CommandText="SELECT ProductId,Type,QuantitySmallUnits FROM StockMovements WHERE Id=$id;";
            P(c,"$id",entityId);
            await using var movementReader=await c.ExecuteReaderAsync(ct);
            if(!await movementReader.ReadAsync(ct)) throw new InvalidOperationException("تعذر قراءة حركة المخزون المستعادة.");
            var productId=movementReader.GetString(0);
            var movementType=movementReader.GetInt32(1);
            var quantity=D(movementReader.GetString(2));
            await movementReader.DisposeAsync();

            c.Parameters.Clear();
            c.CommandText="SELECT CAST(StockSmallUnits AS REAL) FROM Products WHERE Id=$product;";
            P(c,"$product",productId);
            var current=Convert.ToDecimal(await c.ExecuteScalarAsync(ct));
            var signed=movementType is (int)StockMovementType.Sale or (int)StockMovementType.ReturnOut or (int)StockMovementType.AdjustmentOut ? -quantity : quantity;
            var next=current+signed;
            if(next<0) throw new InvalidOperationException("لا يمكن استعادة الحركة لأن رصيد المخزون سيصبح سالباً.");
            c.Parameters.Clear();
            c.CommandText="UPDATE Products SET StockSmallUnits=$stock WHERE Id=$product;";
            P(c,"$stock",S(next)); P(c,"$product",productId);
            await c.ExecuteNonQueryAsync(ct);
        }

        c.Parameters.Clear(); c.CommandText="DELETE FROM DeletedItems WHERE Id=$id;"; P(c,"$id",id.ToString("N")); await c.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        await audit.LogActionAsync(appSession?.CurrentUser?.Id,"RECYCLE_RESTORE",type,id,new { EntityType=type },ct);
    }
    public async Task HardDeleteDeletedItemAsync(Guid id,CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.Delete,ct);await using var db=database.Open();await db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="DELETE FROM DeletedItems WHERE Id=$id;";P(c,"$id",id.ToString("N"));await c.ExecuteNonQueryAsync(ct);await audit.LogActionAsync(appSession?.CurrentUser?.Id, "RECYCLE_HARD_DELETE", "DeletedItem", id,null,ct);}
    public async Task<CashRegisterSummary> GetCashRegisterSummaryAsync(DateTimeOffset? from=null,DateTimeOffset? toDate=null,CancellationToken ct=default)
    {
        await EnsurePermissionAsync(PermissionKeys.View,ct);
        await using var db=database.Open();await db.OpenAsync(ct);await using var c=db.CreateCommand();
        P(c,"$from",from?.ToString("O")??(object)DBNull.Value);P(c,"$to",toDate?.ToString("O")??(object)DBNull.Value);
        c.CommandText="""
            SELECT
              COALESCE((SELECT SUM(CAST(Amount AS REAL)) FROM CashRegisterEntries WHERE EntryType IN ('SALE_RECEIPT','PURCHASE_REFUND') AND ($from IS NULL OR OccurredAt >= $from) AND ($to IS NULL OR OccurredAt <= $to)),0),
              COALESCE((SELECT SUM(CAST(Amount AS REAL)) FROM CashRegisterEntries WHERE EntryType IN ('PURCHASE_PAYMENT','SALE_REFUND','Refund') AND ($from IS NULL OR OccurredAt >= $from) AND ($to IS NULL OR OccurredAt <= $to)),0)
              + COALESCE((SELECT SUM(CAST(Amount AS REAL)) FROM ExpenseCashEntries WHERE EntryType='EXPENSE_PAYMENT:Cash' AND ($from IS NULL OR OccurredAt >= $from) AND ($to IS NULL OR OccurredAt <= $to)),0),
              (SELECT COUNT(*) FROM CashRegisterEntries WHERE ($from IS NULL OR OccurredAt >= $from) AND ($to IS NULL OR OccurredAt <= $to))
              + (SELECT COUNT(*) FROM ExpenseCashEntries WHERE EntryType='EXPENSE_PAYMENT:Cash' AND ($from IS NULL OR OccurredAt >= $from) AND ($to IS NULL OR OccurredAt <= $to));
            """;
        await using var reader=await c.ExecuteReaderAsync(ct);
        if(!await reader.ReadAsync(ct))return new CashRegisterSummary(0m,0m,0m,0);
        var cashIn=Convert.ToDecimal(reader.GetValue(0),CultureInfo.InvariantCulture);
        var cashOut=Convert.ToDecimal(reader.GetValue(1),CultureInfo.InvariantCulture);
        return new CashRegisterSummary(cashIn,cashOut,decimal.Round(cashIn-cashOut,2),Convert.ToInt32(reader.GetValue(2),CultureInfo.InvariantCulture));
    }
    public async Task<string?> GetPreferenceAsync(string key,CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(key))throw new ArgumentException("مفتاح الإعداد مطلوب.",nameof(key));
        await EnsurePermissionAsync(PermissionKeys.View,ct);
        await using var db=database.Open();await db.OpenAsync(ct);await using var c=db.CreateCommand();
        c.CommandText="SELECT Value FROM AppPreferences WHERE [Key]=$key;";P(c,"$key",key.Trim());
        var value=await c.ExecuteScalarAsync(ct);return value is null or DBNull?null:Convert.ToString(value,CultureInfo.InvariantCulture);
    }
    public async Task SavePreferenceAsync(string key,string value,CancellationToken ct=default)
    {
        await EnsurePermissionAsync(PermissionKeys.Settings,ct);
        if(string.IsNullOrWhiteSpace(key)||key.Length>120)throw new ArgumentException("مفتاح الإعداد غير صالح.",nameof(key));
        if(value.Length>10000)throw new ArgumentOutOfRangeException(nameof(value),"قيمة الإعداد طويلة جدًا.");
        await using var db=database.Open();await db.OpenAsync(ct);await using var c=db.CreateCommand();
        c.CommandText="INSERT INTO AppPreferences([Key],[Value],UpdatedAt) VALUES($key,$value,$updated) ON CONFLICT([Key]) DO UPDATE SET [Value]=$value,UpdatedAt=$updated;";
        P(c,"$key",key.Trim());P(c,"$value",value);P(c,"$updated",DateTimeOffset.UtcNow.ToString("O"));await c.ExecuteNonQueryAsync(ct);
        await audit.LogActionAsync(appSession?.CurrentUser?.Id,"PREFERENCE_UPDATE","Settings",null,new { Key=key.Trim() },ct);
    }
    public async Task<IReadOnlyList<MessageTemplate>> GetMessageTemplatesAsync(CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.View,ct);var x=new List<MessageTemplate>();await using var db=database.Open();await db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="SELECT Id,Name,Body,IsSystem FROM MessageTemplates ORDER BY IsSystem DESC,Name;";await using var r=await c.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))x.Add(new MessageTemplate(Guid.Parse(r.GetString(0)),r.GetString(1),r.GetString(2),r.GetBoolean(3)));return x;}
    public async Task<MessageTemplate> SaveMessageTemplateAsync(MessageTemplate t,CancellationToken ct=default){await EnsurePermissionAsync(PermissionKeys.Edit,ct);await using var db=database.Open();await db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="INSERT INTO MessageTemplates(Id,Name,Body,IsSystem) VALUES($id,$name,$body,$system) ON CONFLICT(Id) DO UPDATE SET Name=$name,Body=$body;";P(c,"$id",t.Id.ToString("N"));P(c,"$name",t.Name);P(c,"$body",t.Body);P(c,"$system",t.IsSystem?1:0);await c.ExecuteNonQueryAsync(ct);await audit.LogActionAsync(appSession?.CurrentUser?.Id, "TEMPLATE_SAVE", "MessageTemplate", t.Id,new { t.Name },ct);return t;}
}


