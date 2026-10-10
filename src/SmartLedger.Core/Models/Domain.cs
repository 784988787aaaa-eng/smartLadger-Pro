namespace SmartLedger.Core.Models;

public enum BookKind { All=0, Customers=1, Suppliers=2, Custom=3, Employees=4, Other=5 }
public enum OperationType { Receive, Payment, DebtToUs, DebtOnUs, Sale, Purchase, Return, Settlement, Other }
public enum AccountNature { Debit, Credit, Flexible }
public enum InvoiceType { Sale, Purchase }
public enum PaymentMode { Cash, Credit, Partial }
public enum StockMovementType { Purchase, Sale, ReturnIn, ReturnOut, AdjustmentIn, AdjustmentOut, Inventory }
public enum ExpenseCategoryKind { Rent, Electricity, Maintenance, Transport, Salaries, Other }
public sealed record Book(Guid Id, string Name, BookKind Kind, int SortOrder, bool IsSystem, string Color, string Icon)
{
    public override string ToString()=>Name;
}
public sealed record Person(Guid Id, Guid BookId, string Name, string Phone, string Note, DateTimeOffset CreatedAt)
{
    public string AccountCode { get; init; } = string.Empty;
    public decimal? CreditLimit { get; init; }
    public string Address { get; init; } = string.Empty;
    public string SecondaryPhone { get; init; } = string.Empty;
    public AccountNature Nature { get; init; } = AccountNature.Flexible;
    public bool WhatsAppEnabled { get; init; } = true;
    public DateTimeOffset UpdatedAt { get; init; } = CreatedAt;
    public override string ToString()=>Name;
}
public sealed record LedgerOperation(Guid Id, Guid PersonId, OperationType Type, decimal Amount, string Note, DateTimeOffset OccurredAt, Guid CreatedBy)
{
    public string? ReferenceNumber { get; init; }
    public decimal RunningBalance { get; init; }
    public string TypeLabel=>Type switch { OperationType.Receive=>"استلام", OperationType.Payment=>"سداد", OperationType.DebtToUs=>"له", OperationType.DebtOnUs=>"عليه", OperationType.Sale=>"بيع آجل", OperationType.Purchase=>"شراء آجل", OperationType.Return=>"مرتجع", OperationType.Settlement=>"تسوية", _=>"أخرى" };
};
public sealed record OperationTypeOption(OperationType Value, string Label)
{
    public override string ToString()=>Label;
}
public sealed record PaymentModeOption(PaymentMode Value, string Label)
{
    public override string ToString()=>Label;
}
public sealed record PersonSummary(Person Person, string BookName, decimal OwedToUs, decimal WeOwe, decimal NetBalance, DateTimeOffset? LastOperationAt)
{
    public Guid PersonId=>Person.Id;
    public string PersonName=>Person.Name;
    public override string ToString()=>Person.Name;
};
public sealed record DashboardSummary(decimal OwedToUs, decimal WeOwe, decimal StockValue, int OperationsToday, decimal SalesToday, decimal PurchasesToday, decimal ExpensesToday)
{
    public decimal NetBalance => OwedToUs - WeOwe;
    public bool NetIsOurs => NetBalance >= 0;
};
public sealed record Product(Guid Id, string Name, string Barcode, string Category, string LargeUnit, string SmallUnit, decimal ConversionFactor, decimal PurchasePrice, decimal SalePrice, decimal StockSmallUnits, decimal AlertThreshold, DateTimeOffset CreatedAt)
{
    public string ProductCode { get; init; } = string.Empty;
    public string CategoryId { get; init; } = string.Empty;
    public decimal SellingPriceMajor { get; init; } = SalePrice * ConversionFactor;
    public decimal SellingPriceMinor { get; init; } = SalePrice;
    public decimal MinimumSellingPrice { get; init; } = PurchasePrice;
    public decimal ReorderPoint { get; init; } = AlertThreshold;
    public decimal LowStockThreshold { get; init; } = AlertThreshold;
    public decimal TotalStockMinor => StockSmallUnits;
    public string StockDisplay
    {
        get
        {
            var factor = ConversionFactor > 0 ? ConversionFactor : 1m;
            var major = decimal.Floor(StockSmallUnits / factor);
            var minor = StockSmallUnits - major * factor;
            if (major > 0 && minor > 0) return $"{major:0.##} {LargeUnit} + {minor:0.##} {SmallUnit}";
            if (major > 0) return $"{major:0.##} {LargeUnit}";
            return $"{minor:0.##} {SmallUnit}";
        }
    }
    public DateTimeOffset UpdatedAt { get; init; } = CreatedAt;
    public Guid? DefaultSupplierId { get; init; }
    public string DefaultSupplierName { get; init; } = "";
    public string Note { get; init; } = "";
    public string ShelfLocation { get; init; } = "";
    public string WarehouseName { get; init; } = "المخزن الرئيسي";
    public string AvailabilityLabel => StockSmallUnits <= 0 ? "نفد" : StockSmallUnits <= AlertThreshold ? "منخفض" : "متوفر";
};
public sealed record ProductBatch(Guid Id, Guid ProductId, string BatchNumber, DateTimeOffset? ExpiryDate, decimal QuantityMinor, decimal CostPrice, Guid? SupplierId, string? PurchaseInvoiceRef, DateTimeOffset CreatedAt);
public sealed record StockMovement(Guid Id, Guid ProductId, StockMovementType Type, decimal QuantitySmallUnits, decimal UnitPrice, string Note, DateTimeOffset OccurredAt, Guid CreatedBy)
{
    public Guid? InvoiceId { get; init; }
};
public sealed record ProductMovementRow(DateTimeOffset OccurredAt, string TypeLabel, decimal QuantitySmallUnits, decimal RunningBalance, string Note, Guid? InvoiceId);
public sealed record Invoice(Guid Id, InvoiceType Type, Guid? PersonId, DateTimeOffset IssuedAt, decimal Subtotal, decimal Discount, decimal Total, decimal Paid, PaymentMode PaymentMode, string Note, Guid CreatedBy);
public sealed record InvoiceLine(Guid Id, Guid InvoiceId, Guid ProductId, string Description, decimal Quantity, string Unit, decimal UnitPrice, decimal Discount, decimal LineTotal);
public sealed record Expense(Guid Id, Guid? PersonId, string Category, decimal Amount, string Note, DateTimeOffset OccurredAt, Guid CreatedBy);
public sealed record AppUser(Guid Id, string UserName, string DisplayName, bool IsAdministrator, bool IsActive);
public sealed record User(Guid Id, string UserName, string DisplayName, string PasswordHash, string RecoveryHash, string RecoveryHint, bool IsAdministrator, bool IsActive);
public sealed record AdminSetup(string UserName, string PasswordHash, string RecoveryHash, string RecoveryHint);
public sealed record BusinessIdentity(string BusinessName, string LogoPath, string Phone, string Address, string Currency);
public sealed record RecentOperationRow(DateTimeOffset Date, string PersonName, string Type, decimal Amount, string Note);
public sealed record GlobalSearchResult(Guid Id, string Category, string Title, string Detail, string EntityType);
public sealed record ReportSummary(decimal Sales, decimal Purchases, decimal Expenses, decimal Receivables, decimal Payables, decimal StockValue);
public sealed record BackupInfo(string FilePath, DateTimeOffset CreatedAt, long SizeBytes, bool IsSecondaryDrive, string Sha256);
public sealed record DeletedItem(Guid Id,string EntityType,Guid EntityId,DateTimeOffset DeletedAt,Guid DeletedBy,string Payload)
{
    public string EntityTypeLabel=>EntityType switch { "Person"=>"حساب", "Book"=>"دفتر", "Operation"=>"عملية", "Product"=>"صنف", "StockMovement"=>"حركة مخزون", "Invoice"=>"فاتورة", "Expense"=>"نفقة", _=>EntityType };
}
public sealed record MessageTemplate(Guid Id,string Name,string Body,bool IsSystem);
public sealed record AuditEntry(Guid Id,Guid? UserId,string UserName,string Action,string EntityType,Guid? EntityId,string Details,DateTimeOffset OccurredAt);
