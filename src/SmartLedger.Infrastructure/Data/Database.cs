using Microsoft.Data.Sqlite;

namespace SmartLedger.Infrastructure.Data;

public sealed class Database
{
    private readonly string _connectionString;
    public Database(string rootPath)
    {
        Directory.CreateDirectory(rootPath);
        _connectionString = new SqliteConnectionStringBuilder { DataSource=Path.Combine(rootPath,"smartledger.db"), Mode=SqliteOpenMode.ReadWriteCreate, ForeignKeys=true, Pooling=true, DefaultTimeout=10, Cache=SqliteCacheMode.Shared }.ToString();
    }
    public SqliteConnection Open()=>new(_connectionString);
    public async Task InitializeAsync(CancellationToken ct=default)
    {
        await using var db=Open(); await db.OpenAsync(ct);
        await ExecuteAsync(db,"PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=10000; PRAGMA foreign_keys=ON; PRAGMA temp_store=MEMORY;",ct);
        var version=await ScalarLongAsync(db,"PRAGMA user_version;",ct);
        if(version<1)await MigrationV1Async(db,ct); if(version<2)await MigrationV2Async(db,ct); if(version<3)await MigrationV3Async(db,ct); if(version<4)await MigrationV4Async(db,ct); if(version<5)await MigrationV5Async(db,ct); if(version<6)await MigrationV6Async(db,ct); if(version<7)await MigrationV7Async(db,ct); if(version<8)await MigrationV8Async(db,ct); if(version<9)await MigrationV9Async(db,ct); if(version<10)await MigrationV10Async(db,ct); await SeedAsync(db,ct);
    }
    private static async Task MigrationV1Async(SqliteConnection db,CancellationToken ct)
    {
        await ExecuteAsync(db,"""
        CREATE TABLE IF NOT EXISTS Users(Id TEXT PRIMARY KEY,UserName TEXT NOT NULL UNIQUE,DisplayName TEXT NOT NULL,PasswordHash TEXT NOT NULL,RecoveryHash TEXT NOT NULL DEFAULT '',RecoveryHint TEXT NOT NULL DEFAULT '',IsAdministrator INTEGER NOT NULL,IsActive INTEGER NOT NULL,CreatedAt TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS Books(Id TEXT PRIMARY KEY,Name TEXT NOT NULL,Kind INTEGER NOT NULL,SortOrder INTEGER NOT NULL,IsSystem INTEGER NOT NULL,Color TEXT NOT NULL,Icon TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS People(Id TEXT PRIMARY KEY,BookId TEXT NOT NULL REFERENCES Books(Id),Name TEXT NOT NULL,Phone TEXT NOT NULL DEFAULT '',Note TEXT NOT NULL DEFAULT '',CreatedAt TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS Operations(Id TEXT PRIMARY KEY,PersonId TEXT NOT NULL REFERENCES People(Id),Type INTEGER NOT NULL,Amount TEXT NOT NULL,Note TEXT NOT NULL DEFAULT '',OccurredAt TEXT NOT NULL,CreatedBy TEXT NOT NULL REFERENCES Users(Id));
        CREATE TABLE IF NOT EXISTS Products(Id TEXT PRIMARY KEY,Name TEXT NOT NULL,Barcode TEXT NOT NULL DEFAULT '',Category TEXT NOT NULL DEFAULT '',LargeUnit TEXT NOT NULL,SmallUnit TEXT NOT NULL,ConversionFactor TEXT NOT NULL,PurchasePrice TEXT NOT NULL,SalePrice TEXT NOT NULL,StockSmallUnits TEXT NOT NULL,AlertThreshold TEXT NOT NULL,CreatedAt TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS StockMovements(Id TEXT PRIMARY KEY,ProductId TEXT NOT NULL REFERENCES Products(Id),Type INTEGER NOT NULL,QuantitySmallUnits TEXT NOT NULL,UnitPrice TEXT NOT NULL,Note TEXT NOT NULL DEFAULT '',OccurredAt TEXT NOT NULL,CreatedBy TEXT NOT NULL REFERENCES Users(Id));
        CREATE TABLE IF NOT EXISTS Invoices(Id TEXT PRIMARY KEY,Type INTEGER NOT NULL,PersonId TEXT NULL REFERENCES People(Id),IssuedAt TEXT NOT NULL,Subtotal TEXT NOT NULL,Discount TEXT NOT NULL,Total TEXT NOT NULL,Paid TEXT NOT NULL,PaymentMode INTEGER NOT NULL,Note TEXT NOT NULL DEFAULT '',CreatedBy TEXT NOT NULL REFERENCES Users(Id));
        CREATE TABLE IF NOT EXISTS InvoiceLines(Id TEXT PRIMARY KEY,InvoiceId TEXT NOT NULL REFERENCES Invoices(Id) ON DELETE CASCADE,ProductId TEXT NOT NULL REFERENCES Products(Id),Description TEXT NOT NULL,Quantity TEXT NOT NULL,Unit TEXT NOT NULL,UnitPrice TEXT NOT NULL,Discount TEXT NOT NULL,LineTotal TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS Expenses(Id TEXT PRIMARY KEY,PersonId TEXT NULL REFERENCES People(Id),Category TEXT NOT NULL,Amount TEXT NOT NULL,Note TEXT NOT NULL DEFAULT '',OccurredAt TEXT NOT NULL,CreatedBy TEXT NOT NULL REFERENCES Users(Id));
        CREATE TABLE IF NOT EXISTS BusinessIdentity(Id INTEGER PRIMARY KEY CHECK(Id=1),BusinessName TEXT NOT NULL,LogoPath TEXT NOT NULL DEFAULT '',Phone TEXT NOT NULL DEFAULT '',Address TEXT NOT NULL DEFAULT '',Currency TEXT NOT NULL DEFAULT 'ريال');
        CREATE TABLE IF NOT EXISTS AuditLogs(Id TEXT PRIMARY KEY,UserId TEXT NULL,Action TEXT NOT NULL,EntityType TEXT NOT NULL,EntityId TEXT NULL,Details TEXT NOT NULL,OccurredAt TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS DeletedItems(Id TEXT PRIMARY KEY,EntityType TEXT NOT NULL,EntityId TEXT NOT NULL,DeletedAt TEXT NOT NULL,DeletedBy TEXT NOT NULL,Payload TEXT NOT NULL);
        CREATE INDEX IF NOT EXISTS IX_People_Book_Name ON People(BookId,Name); CREATE INDEX IF NOT EXISTS IX_Operations_Person_Date ON Operations(PersonId,OccurredAt); CREATE INDEX IF NOT EXISTS IX_Operations_Date ON Operations(OccurredAt); CREATE INDEX IF NOT EXISTS IX_Products_Name ON Products(Name); CREATE INDEX IF NOT EXISTS IX_Stock_Product_Date ON StockMovements(ProductId,OccurredAt); CREATE INDEX IF NOT EXISTS IX_Expenses_Date ON Expenses(OccurredAt);
        """,ct); await SetVersionAsync(db,1,ct);
    }
    private static async Task MigrationV2Async(SqliteConnection db,CancellationToken ct){await ExecuteAsync(db,"CREATE INDEX IF NOT EXISTS IX_Invoices_Date_Type ON Invoices(IssuedAt,Type); CREATE INDEX IF NOT EXISTS IX_Audit_Date ON AuditLogs(OccurredAt);",ct);await SetVersionAsync(db,2,ct);}
    private static async Task MigrationV3Async(SqliteConnection db,CancellationToken ct){await EnsureColumnAsync(db,"BusinessIdentity","LogoPath","TEXT NOT NULL DEFAULT ''",ct);await EnsureColumnAsync(db,"BusinessIdentity","Phone","TEXT NOT NULL DEFAULT ''",ct);await EnsureColumnAsync(db,"BusinessIdentity","Address","TEXT NOT NULL DEFAULT ''",ct);await EnsureColumnAsync(db,"BusinessIdentity","Currency","TEXT NOT NULL DEFAULT 'ريال'",ct);await SetVersionAsync(db,3,ct);}
    private static async Task MigrationV4Async(SqliteConnection db,CancellationToken ct){foreach(var table in new[]{"Operations","StockMovements","Expenses","AuditLogs"})await RepairOccurredAtAsync(db,table,ct);await SetVersionAsync(db,4,ct);}
    private static async Task MigrationV5Async(SqliteConnection db,CancellationToken ct){await ExecuteAsync(db,"CREATE TABLE IF NOT EXISTS MessageTemplates(Id TEXT PRIMARY KEY,Name TEXT NOT NULL,Body TEXT NOT NULL,IsSystem INTEGER NOT NULL DEFAULT 0); CREATE TABLE IF NOT EXISTS DeletedItems(Id TEXT PRIMARY KEY,EntityType TEXT NOT NULL,EntityId TEXT NOT NULL,DeletedAt TEXT NOT NULL,DeletedBy TEXT NOT NULL,Payload TEXT NOT NULL); CREATE INDEX IF NOT EXISTS IX_DeletedItems_Date ON DeletedItems(DeletedAt);",ct);await SetVersionAsync(db,5,ct);}
    private static async Task MigrationV7Async(SqliteConnection db,CancellationToken ct)
    {
        await EnsureColumnAsync(db,"Products","DefaultSupplierId","TEXT NULL REFERENCES People(Id)",ct);
        await EnsureColumnAsync(db,"Products","Note","TEXT NOT NULL DEFAULT ''",ct);
        await EnsureColumnAsync(db,"Products","ShelfLocation","TEXT NOT NULL DEFAULT ''",ct);
        await EnsureColumnAsync(db,"Products","WarehouseName","TEXT NOT NULL DEFAULT 'المخزن الرئيسي'",ct);
        await EnsureColumnAsync(db,"StockMovements","InvoiceId","TEXT NULL REFERENCES Invoices(Id)",ct);
        // Optional indexes are created by V1 for normal databases; legacy minimal fixtures may omit these tables.
        await SetVersionAsync(db,7,ct);
    }
    private static async Task MigrationV8Async(SqliteConnection db,CancellationToken ct){await EnsureColumnAsync(db,"Products","WarehouseName","TEXT NOT NULL DEFAULT 'المخزن الرئيسي'",ct);await SetVersionAsync(db,8,ct);}
    private static async Task MigrationV10Async(SqliteConnection db,CancellationToken ct)
    {
        // Additive-only migration: preserve existing people and ledger rows.
        await EnsureColumnAsync(db,"People","AccountCode","TEXT NOT NULL DEFAULT ''",ct);
        await EnsureColumnAsync(db,"People","CreditLimit","TEXT NULL",ct);
        await EnsureColumnAsync(db,"People","Address","TEXT NOT NULL DEFAULT ''",ct);
        await EnsureColumnAsync(db,"People","SecondaryPhone","TEXT NOT NULL DEFAULT ''",ct);
        await EnsureColumnAsync(db,"People","AccountNature","TEXT NOT NULL DEFAULT 'Flexible'",ct);
        await EnsureColumnAsync(db,"People","WhatsAppEnabled","INTEGER NOT NULL DEFAULT 1",ct);
        await EnsureColumnAsync(db,"People","UpdatedAt","TEXT NOT NULL DEFAULT ''",ct);
        await EnsureColumnAsync(db,"Operations","ReferenceNumber","TEXT NULL",ct);
        await ExecuteAsync(db,"UPDATE People SET AccountCode='ACC-'||substr(replace(Id,'-',''),1,12) WHERE AccountCode IS NULL OR trim(AccountCode)=''; UPDATE People SET UpdatedAt=CreatedAt WHERE UpdatedAt IS NULL OR trim(UpdatedAt)=''; UPDATE People SET WhatsAppEnabled=CASE WHEN trim(Phone)<>'' THEN 1 ELSE 0 END;",ct);
        await ExecuteAsync(db,"CREATE UNIQUE INDEX IF NOT EXISTS UX_People_AccountCode ON People(AccountCode); CREATE INDEX IF NOT EXISTS IX_People_Phone ON People(Phone); CREATE INDEX IF NOT EXISTS IX_Operations_Person_OccurredAt ON Operations(PersonId,OccurredAt);",ct);
        await SetVersionAsync(db,10,ct);
    }
    private static async Task MigrationV9Async(SqliteConnection db,CancellationToken ct){await ExecuteAsync(db,"CREATE TABLE IF NOT EXISTS UserPermissions(UserId TEXT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,Permission TEXT NOT NULL,PRIMARY KEY(UserId,Permission)); CREATE INDEX IF NOT EXISTS IX_UserPermissions_User ON UserPermissions(UserId);",ct); await ExecuteAsync(db,"INSERT OR IGNORE INTO UserPermissions(UserId,Permission) SELECT Id,'reset_data' FROM Users WHERE IsAdministrator=1;",ct); await SetVersionAsync(db,9,ct);}
    private static async Task MigrationV6Async(SqliteConnection db,CancellationToken ct)
    {
        await ExecuteAsync(db,"CREATE TABLE IF NOT EXISTS UserPermissions(UserId TEXT NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,Permission TEXT NOT NULL,PRIMARY KEY(UserId,Permission)); CREATE INDEX IF NOT EXISTS IX_UserPermissions_User ON UserPermissions(UserId);",ct);
        foreach(var permission in SmartLedger.Core.Abstractions.PermissionKeys.All)
        {
            await ExecuteAsync(db,$"INSERT OR IGNORE INTO UserPermissions(UserId,Permission) SELECT Id,'{permission}' FROM Users WHERE IsAdministrator=1;",ct);
        }
        await SetVersionAsync(db,6,ct);
    }
    private static async Task RepairOccurredAtAsync(SqliteConnection db,string table,CancellationToken ct){var columns=new HashSet<string>(StringComparer.OrdinalIgnoreCase);await using(var c=db.CreateCommand()){c.CommandText=$"PRAGMA table_info({table});";await using var r=await c.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))columns.Add(r.GetString(1));}if(columns.Contains("OccurredAt"))return;if(columns.Contains("OccrredAt")){await ExecuteAsync(db,$"ALTER TABLE {table} RENAME COLUMN OccrredAt TO OccurredAt;",ct);return;}await ExecuteAsync(db,$"ALTER TABLE {table} ADD COLUMN OccurredAt TEXT NOT NULL DEFAULT ''",ct);}
    private static async Task SeedAsync(SqliteConnection db,CancellationToken ct){await ExecuteAsync(db,"INSERT OR IGNORE INTO Books VALUES('00000000000000000000000000000001','كل الحسابات',0,0,1,'#00B4D8','all');INSERT OR IGNORE INTO Books VALUES('00000000000000000000000000000002','العملاء',1,1,1,'#06D6A0','users');INSERT OR IGNORE INTO Books VALUES('00000000000000000000000000000003','الموردون',2,2,1,'#FFD166','truck');INSERT OR IGNORE INTO Books VALUES('00000000000000000000000000000004','العاملون',4,3,1,'#9B8AFB','users');INSERT OR IGNORE INTO Books VALUES('00000000000000000000000000000005','أخرى',5,4,1,'#7C8AA5','folder');",ct);if(await ScalarLongAsync(db,"SELECT COUNT(*) FROM BusinessIdentity;",ct)==0)await ExecuteAsync(db,"INSERT INTO BusinessIdentity VALUES(1,'الدفتر الذكي','','','','ريال');",ct);if(await ScalarLongAsync(db,"SELECT COUNT(*) FROM MessageTemplates;",ct)==0)await ExecuteAsync(db,"INSERT INTO MessageTemplates VALUES('00000000000000000000000000000001','تذكير رصيد','السلام عليكم {الاسم}، نذكركم بأن الرصيد الحالي هو {الرصيد}. نشكركم.',1);INSERT INTO MessageTemplates VALUES('00000000000000000000000000000002','تأكيد عملية','السلام عليكم {الاسم}، تم تسجيل العملية رقم {رقم العملية} بتاريخ {التاريخ} بمبلغ {المبلغ}.',1);",ct);}
    private static async Task EnsureColumnAsync(SqliteConnection db,string table,string column,string definition,CancellationToken ct){await using var c=db.CreateCommand();c.CommandText="SELECT EXISTS(SELECT 1 FROM sqlite_master WHERE type='table' AND name=$table);";c.Parameters.AddWithValue("$table",table);if(Convert.ToInt32(await c.ExecuteScalarAsync(ct))==0)return;c.Parameters.Clear();c.CommandText=$"PRAGMA table_info({table});";var exists=false;await using var r=await c.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))if(string.Equals(r.GetString(1),column,StringComparison.OrdinalIgnoreCase))exists=true;if(!exists)await ExecuteAsync(db,$"ALTER TABLE {table} ADD COLUMN {column} {definition};",ct);}
    private static async Task ExecuteAsync(SqliteConnection db,string sql,CancellationToken ct){await using var c=db.CreateCommand();c.CommandText=sql;await c.ExecuteNonQueryAsync(ct);}
    private static async Task<long> ScalarLongAsync(SqliteConnection db,string sql,CancellationToken ct){await using var c=db.CreateCommand();c.CommandText=sql;return Convert.ToInt64(await c.ExecuteScalarAsync(ct));}
    private static Task SetVersionAsync(SqliteConnection db,int version,CancellationToken ct)=>ExecuteAsync(db,$"PRAGMA user_version={version};",ct);
}

