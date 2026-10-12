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
        if(version<1)await MigrationV1Async(db,ct); if(version<2)await MigrationV2Async(db,ct); if(version<3)await MigrationV3Async(db,ct); if(version<4)await MigrationV4Async(db,ct); if(version<5)await MigrationV5Async(db,ct); if(version<6)await MigrationV6Async(db,ct); if(version<7)await MigrationV7Async(db,ct); if(version<8)await MigrationV8Async(db,ct); if(version<9)await MigrationV9Async(db,ct); if(version<10)await MigrationV10Async(db,ct); if(version<11)await MigrationV11Async(db,ct); if(version<12)await MigrationV12Async(db,ct); if(version<13)await MigrationV13Async(db,ct); if(version<14)await MigrationV14Async(db,ct); if(version<15)await MigrationV15Async(db,ct); if(version<16)await MigrationV16Async(db,ct); if(version<17)await MigrationV17Async(db,ct); if(version<18)await MigrationV18Async(db,ct); await EnsureColumnAsync(db,"Operations","ReferenceNumber","TEXT NULL",ct); await EnsureColumnAsync(db,"Operations","SourceInvoiceId","TEXT NULL",ct); await EnsureColumnAsync(db,"Operations","SourceStockMovementId","TEXT NULL",ct); await EnsureColumnAsync(db,"Invoices","Status","TEXT NOT NULL DEFAULT 'Posted'",ct); await SeedAsync(db,ct);
    }
    private static async Task MigrationV1Async(SqliteConnection db,CancellationToken ct)
    {
        await ExecuteAsync(db,"""
        CREATE TABLE IF NOT EXISTS Users(Id TEXT PRIMARY KEY,UserName TEXT NOT NULL UNIQUE,DisplayName TEXT NOT NULL,PasswordHash TEXT NOT NULL,RecoveryHash TEXT NOT NULL DEFAULT '',RecoveryHint TEXT NOT NULL DEFAULT '',IsAdministrator INTEGER NOT NULL,IsActive INTEGER NOT NULL,CreatedAt TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS Books(Id TEXT PRIMARY KEY,Name TEXT NOT NULL,Kind INTEGER NOT NULL,SortOrder INTEGER NOT NULL,IsSystem INTEGER NOT NULL,Color TEXT NOT NULL,Icon TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS People(Id TEXT PRIMARY KEY,BookId TEXT NOT NULL REFERENCES Books(Id),Name TEXT NOT NULL,Phone TEXT NOT NULL DEFAULT '',Note TEXT NOT NULL DEFAULT '',CreatedAt TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS Operations(Id TEXT PRIMARY KEY,PersonId TEXT NOT NULL REFERENCES People(Id),Type INTEGER NOT NULL,Amount TEXT NOT NULL,Note TEXT NOT NULL DEFAULT '',OccurredAt TEXT NOT NULL,CreatedBy TEXT NOT NULL REFERENCES Users(Id),ReferenceNumber TEXT NULL,SourceInvoiceId TEXT NULL,SourceStockMovementId TEXT NULL);
        CREATE TABLE IF NOT EXISTS Products(Id TEXT PRIMARY KEY,Name TEXT NOT NULL,Barcode TEXT NOT NULL DEFAULT '',Category TEXT NOT NULL DEFAULT '',LargeUnit TEXT NOT NULL,SmallUnit TEXT NOT NULL,ConversionFactor TEXT NOT NULL,PurchasePrice TEXT NOT NULL,SalePrice TEXT NOT NULL,StockSmallUnits TEXT NOT NULL,AlertThreshold TEXT NOT NULL,CreatedAt TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS StockMovements(Id TEXT PRIMARY KEY,ProductId TEXT NOT NULL REFERENCES Products(Id),Type INTEGER NOT NULL,QuantitySmallUnits TEXT NOT NULL,UnitPrice TEXT NOT NULL,Note TEXT NOT NULL DEFAULT '',OccurredAt TEXT NOT NULL,CreatedBy TEXT NOT NULL REFERENCES Users(Id));
        CREATE TABLE IF NOT EXISTS Invoices(Id TEXT PRIMARY KEY,Type INTEGER NOT NULL,PersonId TEXT NULL REFERENCES People(Id),IssuedAt TEXT NOT NULL,Subtotal TEXT NOT NULL,Discount TEXT NOT NULL,Total TEXT NOT NULL,Paid TEXT NOT NULL,PaymentMode INTEGER NOT NULL,Note TEXT NOT NULL DEFAULT '',CreatedBy TEXT NOT NULL REFERENCES Users(Id),Status TEXT NOT NULL DEFAULT 'Posted');
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
        await EnsureColumnAsync(db,"Operations","SourceInvoiceId","TEXT NULL",ct);
        await EnsureColumnAsync(db,"Operations","SourceStockMovementId","TEXT NULL",ct);
        if(await TableExistsAsync(db,"People",ct))
        {
            await ExecuteAsync(db,"UPDATE People SET AccountCode='ACC-'||substr(replace(Id,'-',''),1,12) WHERE AccountCode IS NULL OR trim(AccountCode)=''; UPDATE People SET UpdatedAt=CreatedAt WHERE UpdatedAt IS NULL OR trim(UpdatedAt)=''; UPDATE People SET WhatsAppEnabled=CASE WHEN trim(Phone)<>'' THEN 1 ELSE 0 END;",ct);
            await ExecuteAsync(db,"CREATE UNIQUE INDEX IF NOT EXISTS UX_People_AccountCode ON People(AccountCode); CREATE INDEX IF NOT EXISTS IX_People_Phone ON People(Phone);",ct);
        }
        if(await TableExistsAsync(db,"Operations",ct))
            await ExecuteAsync(db,"CREATE INDEX IF NOT EXISTS IX_Operations_Person_OccurredAt ON Operations(PersonId,OccurredAt);",ct);
        await SetVersionAsync(db,10,ct);
    }
    private static async Task MigrationV18Async(SqliteConnection db,CancellationToken ct)
    {
        // Cover invoice, party-ledger and stock lookup paths without duplicating existing unique indexes.
        await ExecuteAsync(db,"CREATE INDEX IF NOT EXISTS IX_Invoices_Type_Person_IssuedAt ON Invoices(Type,PersonId,IssuedAt); CREATE INDEX IF NOT EXISTS IX_InvoiceLines_ProductId ON InvoiceLines(ProductId); CREATE INDEX IF NOT EXISTS IX_StockMovements_Product_OccurredAt ON StockMovements(ProductId,OccurredAt); CREATE INDEX IF NOT EXISTS IX_Operations_Person_OccurredAt ON Operations(PersonId,OccurredAt);",ct);
        await SetVersionAsync(db,18,ct);
    }
    private static async Task MigrationV17Async(SqliteConnection db,CancellationToken ct)
    {
        // Older migrations defaulted QuantityMinor to Quantity. For lines whose saved unit factor
        // proves that a larger unit was used, repair the base-unit quantity before reversals rely on it.
        await ExecuteAsync(db,"UPDATE InvoiceLines SET QuantityMinor=CAST(Quantity AS REAL)*CAST(ConversionFactorApplied AS REAL) WHERE CAST(ConversionFactorApplied AS REAL)>1 AND CAST(QuantityMinor AS REAL)=CAST(Quantity AS REAL);",ct);
        await SetVersionAsync(db,17,ct);
    }

    private static async Task MigrationV16Async(SqliteConnection db,CancellationToken ct)
    {
        await ExecuteAsync(db,"CREATE TABLE IF NOT EXISTS OperationCashEntries(Id TEXT PRIMARY KEY,OperationId TEXT NOT NULL UNIQUE REFERENCES Operations(Id) ON DELETE CASCADE,Amount TEXT NOT NULL,EntryType TEXT NOT NULL,OccurredAt TEXT NOT NULL,CreatedBy TEXT NOT NULL REFERENCES Users(Id)); CREATE INDEX IF NOT EXISTS IX_OperationCashEntries_Date ON OperationCashEntries(OccurredAt);",ct);
        await ExecuteAsync(db,"INSERT OR IGNORE INTO OperationCashEntries(Id,OperationId,Amount,EntryType,OccurredAt,CreatedBy) SELECT lower(hex(randomblob(16))),o.Id,o.Amount,CASE o.Type WHEN 0 THEN 'OPERATION_RECEIPT' ELSE 'OPERATION_PAYMENT' END,o.OccurredAt,o.CreatedBy FROM Operations o WHERE o.Type IN (0,1) AND EXISTS(SELECT 1 FROM Users u WHERE u.Id=o.CreatedBy) AND NOT EXISTS(SELECT 1 FROM Invoices i WHERE trim(COALESCE(o.ReferenceNumber,''))<>'' AND lower(trim(i.InvoiceNumber))=lower(trim(o.ReferenceNumber)));",ct);
        await SetVersionAsync(db,16,ct);
    }

    private static async Task MigrationV15Async(SqliteConnection db,CancellationToken ct)
    {
        // Keep exact batch consumption per invoice so a reversal can restore the same lots.
        await ExecuteAsync(db,"""
            CREATE TABLE IF NOT EXISTS InvoiceBatchAllocations(
                Id TEXT PRIMARY KEY,
                InvoiceId TEXT NOT NULL REFERENCES Invoices(Id) ON DELETE CASCADE,
                InvoiceLineId TEXT NOT NULL REFERENCES InvoiceLines(Id) ON DELETE CASCADE,
                ProductId TEXT NOT NULL REFERENCES Products(Id),
                BatchId TEXT NOT NULL REFERENCES ProductBatches(Id),
                QuantityMinor TEXT NOT NULL CHECK(CAST(QuantityMinor AS REAL)>0),
                CreatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_InvoiceBatchAllocations_Invoice ON InvoiceBatchAllocations(InvoiceId);
            CREATE INDEX IF NOT EXISTS IX_InvoiceBatchAllocations_Batch ON InvoiceBatchAllocations(BatchId);
            """,ct);
        await SetVersionAsync(db,15,ct);
    }

    private static async Task MigrationV14Async(SqliteConnection db,CancellationToken ct)
    {
        // Additive Phase 4 purchasing, expense cash-flow and runtime preference schema.
        if(await TableExistsAsync(db,"Expenses",ct))
        {
            await EnsureColumnAsync(db,"Expenses","CategoryId","TEXT NOT NULL DEFAULT ''",ct);
            await EnsureColumnAsync(db,"Expenses","PaymentMethod","TEXT NOT NULL DEFAULT 'Cash'",ct);
        }
        await ExecuteAsync(db,"""
            CREATE TABLE IF NOT EXISTS ExpenseCashEntries(
                Id TEXT PRIMARY KEY,
                ExpenseId TEXT NOT NULL UNIQUE REFERENCES Expenses(Id) ON DELETE CASCADE,
                Amount TEXT NOT NULL,
                EntryType TEXT NOT NULL DEFAULT 'EXPENSE_PAYMENT',
                OccurredAt TEXT NOT NULL,
                CreatedBy TEXT NOT NULL REFERENCES Users(Id)
            );
            CREATE INDEX IF NOT EXISTS IX_ExpenseCashEntries_Date ON ExpenseCashEntries(OccurredAt);
            CREATE TABLE IF NOT EXISTS AppPreferences(
                [Key] TEXT PRIMARY KEY,
                [Value] TEXT NOT NULL DEFAULT '',
                UpdatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Expenses_Category_Date ON Expenses(Category,OccurredAt);
            """,ct);
        await SetVersionAsync(db,14,ct);
    }

    private static async Task MigrationV13Async(SqliteConnection db,CancellationToken ct)
    {
        // Follow-up additive settings migration so installations already at v12 also receive invoice identity fields.
        if (await TableExistsAsync(db,"BusinessIdentity",ct))
        {
            await EnsureColumnAsync(db,"BusinessIdentity","TaxRegistrationNumber","TEXT NOT NULL DEFAULT ''",ct);
            await EnsureColumnAsync(db,"BusinessIdentity","CommercialRegistrationNumber","TEXT NOT NULL DEFAULT ''",ct);
            await EnsureColumnAsync(db,"BusinessIdentity","InvoiceTerms","TEXT NOT NULL DEFAULT ''",ct);
        }
        await SetVersionAsync(db,13,ct);
    }

    private static async Task MigrationV12Async(SqliteConnection db,CancellationToken ct)
    {
        // Additive commercial invoice migration. Legacy invoices receive stable identifiers.
        if (await TableExistsAsync(db,"BusinessIdentity",ct))
        {
            await EnsureColumnAsync(db,"BusinessIdentity","TaxRegistrationNumber","TEXT NOT NULL DEFAULT ''",ct);
            await EnsureColumnAsync(db,"BusinessIdentity","CommercialRegistrationNumber","TEXT NOT NULL DEFAULT ''",ct);
            await EnsureColumnAsync(db,"BusinessIdentity","InvoiceTerms","TEXT NOT NULL DEFAULT ''",ct);
        }
        await ExecuteAsync(db,"CREATE TABLE IF NOT EXISTS Invoices(Id TEXT PRIMARY KEY,Type INTEGER NOT NULL,PersonId TEXT NULL REFERENCES People(Id),IssuedAt TEXT NOT NULL,Subtotal TEXT NOT NULL,Discount TEXT NOT NULL,Total TEXT NOT NULL,Paid TEXT NOT NULL,PaymentMode INTEGER NOT NULL,Note TEXT NOT NULL DEFAULT '',CreatedBy TEXT NOT NULL);",ct);
        await EnsureColumnAsync(db,"Invoices","InvoiceNumber","TEXT NOT NULL DEFAULT ''",ct);
        await EnsureColumnAsync(db,"Invoices","DiscountPercentage","TEXT NOT NULL DEFAULT '0'",ct);
        await EnsureColumnAsync(db,"Invoices","TaxAmount","TEXT NOT NULL DEFAULT '0'",ct);
        await EnsureColumnAsync(db,"Invoices","CustomerName","TEXT NOT NULL DEFAULT ''",ct);
        await EnsureColumnAsync(db,"Invoices","StoreName","TEXT NOT NULL DEFAULT ''",ct);
        await EnsureColumnAsync(db,"Invoices","Currency","TEXT NOT NULL DEFAULT 'ريال'",ct);
        await EnsureColumnAsync(db,"Invoices","IssuedByUserId","TEXT NOT NULL DEFAULT ''",ct);
        await ExecuteAsync(db,"UPDATE Invoices SET InvoiceNumber='LEGACY-'||replace(Id,'-','') WHERE trim(InvoiceNumber)=''; UPDATE Invoices SET IssuedByUserId=CreatedBy WHERE trim(IssuedByUserId)='';",ct);
        await ExecuteAsync(db,"CREATE TABLE IF NOT EXISTS InvoiceLines(Id TEXT PRIMARY KEY,InvoiceId TEXT NOT NULL REFERENCES Invoices(Id) ON DELETE CASCADE,ProductId TEXT NOT NULL REFERENCES Products(Id),Description TEXT NOT NULL,Quantity TEXT NOT NULL,Unit TEXT NOT NULL,UnitPrice TEXT NOT NULL,Discount TEXT NOT NULL,LineTotal TEXT NOT NULL);",ct);
        await EnsureColumnAsync(db,"InvoiceLines","Barcode","TEXT NOT NULL DEFAULT ''",ct);
        await EnsureColumnAsync(db,"InvoiceLines","UnitSelected","TEXT NOT NULL DEFAULT ''",ct);
        await EnsureColumnAsync(db,"InvoiceLines","ConversionFactorApplied","TEXT NOT NULL DEFAULT '1'",ct);
        await EnsureColumnAsync(db,"InvoiceLines","QuantityMinor","TEXT NOT NULL DEFAULT '0'",ct);
        await EnsureColumnAsync(db,"InvoiceLines","CostPriceSnapshot","TEXT NOT NULL DEFAULT '0'",ct);
        await ExecuteAsync(db,"UPDATE InvoiceLines SET UnitSelected=Unit WHERE trim(UnitSelected)=''; UPDATE InvoiceLines SET QuantityMinor=Quantity WHERE CAST(QuantityMinor AS REAL)=0; CREATE UNIQUE INDEX IF NOT EXISTS UX_Invoices_InvoiceNumber ON Invoices(InvoiceNumber) WHERE trim(InvoiceNumber)<>''; CREATE INDEX IF NOT EXISTS IX_Invoices_PersonId ON Invoices(PersonId); CREATE INDEX IF NOT EXISTS IX_Invoices_CreatedAt ON Invoices(IssuedAt); CREATE INDEX IF NOT EXISTS IX_InvoiceLines_InvoiceId ON InvoiceLines(InvoiceId); CREATE TABLE IF NOT EXISTS CashRegisterEntries(Id TEXT PRIMARY KEY,InvoiceId TEXT NOT NULL REFERENCES Invoices(Id),Amount TEXT NOT NULL,EntryType TEXT NOT NULL,OccurredAt TEXT NOT NULL,CreatedBy TEXT NOT NULL REFERENCES Users(Id)); CREATE INDEX IF NOT EXISTS IX_CashRegisterEntries_InvoiceId ON CashRegisterEntries(InvoiceId);",ct);
        await EnsureColumnAsync(db,"Invoices","Status","TEXT NOT NULL DEFAULT 'Posted'",ct);
        await SetVersionAsync(db,12,ct);
    }
    private static async Task MigrationV11Async(SqliteConnection db,CancellationToken ct)
    {
        // Additive inventory migration; the partial barcode index preserves legacy blank barcodes.
        if (await TableExistsAsync(db,"Products",ct))
        {
        await EnsureColumnAsync(db,"Products","CategoryId","TEXT NOT NULL DEFAULT ''",ct);
        await EnsureColumnAsync(db,"Products","MajorUnit","TEXT NOT NULL DEFAULT ''",ct);
        await EnsureColumnAsync(db,"Products","MinorUnit","TEXT NOT NULL DEFAULT ''",ct);
        await EnsureColumnAsync(db,"Products","ConversionFactor","TEXT NOT NULL DEFAULT '1'",ct);
        await EnsureColumnAsync(db,"Products","SellingPriceMajor","TEXT NOT NULL DEFAULT '0'",ct);
        await EnsureColumnAsync(db,"Products","SellingPriceMinor","TEXT NOT NULL DEFAULT '0'",ct);
        await EnsureColumnAsync(db,"Products","MinSellingPrice","TEXT NOT NULL DEFAULT '0'",ct);
        await EnsureColumnAsync(db,"Products","ReorderLevel","TEXT NOT NULL DEFAULT '0'",ct);
        await EnsureColumnAsync(db,"Products","UpdatedAt","TEXT NOT NULL DEFAULT ''",ct);
        await ExecuteAsync(db,"UPDATE Products SET MajorUnit=LargeUnit WHERE trim(MajorUnit)=''; UPDATE Products SET MinorUnit=SmallUnit WHERE trim(MinorUnit)=''; UPDATE Products SET SellingPriceMajor=CAST(SalePrice AS REAL)*CAST(ConversionFactor AS REAL) WHERE CAST(SellingPriceMajor AS REAL)=0; UPDATE Products SET SellingPriceMinor=SalePrice WHERE CAST(SellingPriceMinor AS REAL)=0; UPDATE Products SET MinSellingPrice=PurchasePrice WHERE CAST(MinSellingPrice AS REAL)=0; UPDATE Products SET ReorderLevel=AlertThreshold WHERE CAST(ReorderLevel AS REAL)=0; UPDATE Products SET UpdatedAt=CreatedAt WHERE trim(UpdatedAt)='';",ct);
        await ExecuteAsync(db,"CREATE TABLE IF NOT EXISTS ProductBatches(Id TEXT PRIMARY KEY,ProductId TEXT NOT NULL REFERENCES Products(Id),BatchCode TEXT NOT NULL DEFAULT '',CostPrice TEXT NOT NULL,QuantityMinor TEXT NOT NULL,ExpiryDate TEXT NULL,SupplierId TEXT NULL REFERENCES People(Id),PurchaseInvoiceRef TEXT NULL,CreatedAt TEXT NOT NULL); CREATE INDEX IF NOT EXISTS IX_Products_CategoryId ON Products(CategoryId); CREATE INDEX IF NOT EXISTS IX_ProductBatches_ProductId ON ProductBatches(ProductId);",ct);
        // Keep legacy data intact: an existing duplicate barcode must not abort the v11 upgrade.
        var duplicateBarcodeGroups = await ScalarLongAsync(db,"SELECT COUNT(*) FROM (SELECT lower(trim(Barcode)) FROM Products WHERE trim(Barcode)<>'' GROUP BY lower(trim(Barcode)) HAVING COUNT(*)>1);",ct);
        if (duplicateBarcodeGroups == 0)
            await ExecuteAsync(db,"CREATE UNIQUE INDEX IF NOT EXISTS UX_Products_Barcode_NonEmpty ON Products(Barcode) WHERE trim(Barcode)<>'';",ct);
        }
        await SetVersionAsync(db,11,ct);
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
    private static async Task SeedAsync(SqliteConnection db,CancellationToken ct){await ExecuteAsync(db,"INSERT OR IGNORE INTO Books VALUES('00000000000000000000000000000001','كل الحسابات',0,0,1,'#00B4D8','all');INSERT OR IGNORE INTO Books VALUES('00000000000000000000000000000002','العملاء',1,1,1,'#06D6A0','users');INSERT OR IGNORE INTO Books VALUES('00000000000000000000000000000003','الموردون',2,2,1,'#FFD166','truck');INSERT OR IGNORE INTO Books VALUES('00000000000000000000000000000004','العاملون',4,3,1,'#9B8AFB','users');INSERT OR IGNORE INTO Books VALUES('00000000000000000000000000000005','أخرى',5,4,1,'#7C8AA5','folder');",ct);if(await ScalarLongAsync(db,"SELECT COUNT(*) FROM BusinessIdentity;",ct)==0)await ExecuteAsync(db,"INSERT INTO BusinessIdentity(Id,BusinessName,LogoPath,Phone,Address,Currency) VALUES(1,'الدفتر الذكي','','','','ريال');",ct);if(await ScalarLongAsync(db,"SELECT COUNT(*) FROM MessageTemplates;",ct)==0)await ExecuteAsync(db,"INSERT INTO MessageTemplates VALUES('00000000000000000000000000000001','تذكير رصيد','السلام عليكم {الاسم}، نذكركم بأن الرصيد الحالي هو {الرصيد}. نشكركم.',1);INSERT INTO MessageTemplates VALUES('00000000000000000000000000000002','تأكيد عملية','السلام عليكم {الاسم}، تم تسجيل العملية رقم {رقم العملية} بتاريخ {التاريخ} بمبلغ {المبلغ}.',1);",ct);}
    private static async Task<bool> TableExistsAsync(SqliteConnection db,string table,CancellationToken ct)
    {
        await using var command=db.CreateCommand();
        command.CommandText="SELECT EXISTS(SELECT 1 FROM sqlite_master WHERE type='table' AND name=$table);";
        command.Parameters.AddWithValue("$table",table);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct))==1;
    }
    private static async Task EnsureColumnAsync(SqliteConnection db,string table,string column,string definition,CancellationToken ct){await using var c=db.CreateCommand();c.CommandText="SELECT EXISTS(SELECT 1 FROM sqlite_master WHERE type='table' AND name=$table);";c.Parameters.AddWithValue("$table",table);if(Convert.ToInt32(await c.ExecuteScalarAsync(ct))==0)return;c.Parameters.Clear();c.CommandText=$"PRAGMA table_info({table});";var exists=false;await using var r=await c.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))if(string.Equals(r.GetString(1),column,StringComparison.OrdinalIgnoreCase))exists=true;if(!exists)await ExecuteAsync(db,$"ALTER TABLE {table} ADD COLUMN {column} {definition};",ct);}
    private static async Task ExecuteAsync(SqliteConnection db,string sql,CancellationToken ct){await using var c=db.CreateCommand();c.CommandText=sql;await c.ExecuteNonQueryAsync(ct);}
    private static async Task<long> ScalarLongAsync(SqliteConnection db,string sql,CancellationToken ct){await using var c=db.CreateCommand();c.CommandText=sql;return Convert.ToInt64(await c.ExecuteScalarAsync(ct));}
    private static Task SetVersionAsync(SqliteConnection db,int version,CancellationToken ct)=>ExecuteAsync(db,$"PRAGMA user_version={version};",ct);
}
