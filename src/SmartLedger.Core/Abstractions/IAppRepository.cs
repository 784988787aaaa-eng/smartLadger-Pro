using SmartLedger.Core.Models;

namespace SmartLedger.Core.Abstractions;

public interface IAppRepository
{
    Task<IReadOnlyList<Book>> GetBooksAsync(CancellationToken ct = default);
    Task<Book> CreateBookAsync(string name, string color, string icon, CancellationToken ct = default);
    Task RenameBookAsync(Guid id, string name, CancellationToken ct = default);
    Task DeleteBookAsync(Guid id, bool deleteAccounts, CancellationToken ct = default);
    Task<IReadOnlyList<PersonSummary>> GetPeopleAsync(Guid? bookId = null, string? search = null, int? take = null, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid,string>> GetPersonNamesAsync(CancellationToken ct = default);
    Task<Person> CreatePersonAsync(Guid bookId, string name, string phone, string note, CancellationToken ct = default);
    Task<Person> CreatePersonAsync(Person person, CancellationToken ct = default);
    Task UpdatePersonAsync(Person person, CancellationToken ct = default);
    Task<Person> CreatePersonWithOpeningBalanceAsync(Guid bookId, string name, string phone, string note, decimal openingBalance, OperationType openingDirection, CancellationToken ct = default);
    Task<Person> CreatePersonWithOpeningBalanceAsync(Person person, decimal openingBalance, OperationType openingDirection, CancellationToken ct = default);
    Task<DashboardSummary> GetDashboardAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LedgerOperation>> GetOperationsAsync(Guid? personId = null, int take = 100, CancellationToken ct = default);
    Task<IReadOnlyList<LedgerOperation>> GetOperationsInPeriodAsync(DateTimeOffset from, DateTimeOffset toDate, CancellationToken ct = default);
    Task<LedgerOperation> CreateOperationAsync(LedgerOperation operation, CancellationToken ct = default);
    Task DeleteOperationAsync(Guid id, Guid deletedBy, CancellationToken ct = default);
    Task DeletePersonAsync(Guid id, Guid deletedBy, CancellationToken ct = default);
    Task DeleteProductAsync(Guid id, Guid deletedBy, CancellationToken ct = default);
    Task<IReadOnlyList<Product>> GetProductsAsync(string? search = null, CancellationToken ct = default);
    Task<Product?> GetProductByBarcodeAsync(string barcode, CancellationToken ct = default);
    Task<IReadOnlyList<ProductBatch>> GetProductBatchesAsync(Guid productId, CancellationToken ct = default);
    Task<ProductBatch> AddStockBatchAsync(ProductBatch batch, CancellationToken ct = default);
    Task<IReadOnlyList<StockMovement>> GetStockMovementsAsync(Guid productId, int take = 200, CancellationToken ct = default);
    Task<Product> CreateProductAsync(Product product, CancellationToken ct = default);
    Task<Product> UpdateProductAsync(Product product, CancellationToken ct = default);
    Task<IReadOnlyList<PersonSummary>> GetSupplierPeopleAsync(string? search = null, CancellationToken ct = default);
    Task<IReadOnlyList<ProductMovementRow>> GetProductMovementsAsync(Guid productId, int take = 30, CancellationToken ct = default);
    Task<StockMovement> CreateStockMovementAsync(StockMovement movement, CancellationToken ct = default);
    Task DeleteStockMovementAsync(Guid id, Guid deletedBy, CancellationToken ct = default);
    Task<Invoice> CreateInvoiceAsync(Invoice invoice, IReadOnlyList<InvoiceLine> lines, CancellationToken ct = default);
    Task<Invoice> SaveSalesInvoiceAsync(Invoice invoice, IReadOnlyList<InvoiceLine> lines, IReadOnlyList<StockMovement> movements, LedgerOperation? operation, CancellationToken ct = default);
    Task<Invoice> CreateInvoiceTransactionAsync(Invoice invoice, IReadOnlyList<InvoiceLine> lines, StockMovement? movement, LedgerOperation? operation, CancellationToken ct = default);
    Task<Invoice> CreateInvoiceTransactionWithMovementsAsync(Invoice invoice, IReadOnlyList<InvoiceLine> lines, IReadOnlyList<StockMovement> movements, LedgerOperation? operation, CancellationToken ct = default);
    Task<Expense> CreateExpenseAsync(Expense expense, CancellationToken ct = default);
    Task<IReadOnlyList<Expense>> GetExpensesAsync(DateTimeOffset? from = null, DateTimeOffset? toDate = null, CancellationToken ct = default);
    Task<IReadOnlyList<RecentOperationRow>> GetRecentOperationsAsync(int take = 12, CancellationToken ct = default);
    Task<IReadOnlyList<GlobalSearchResult>> SearchGlobalAsync(string query, int takePerCategory = 8, CancellationToken ct = default);
    Task<ReportSummary> GetReportSummaryAsync(DateTimeOffset from, DateTimeOffset toDate, CancellationToken ct = default);
    Task<BusinessIdentity> GetIdentityAsync(CancellationToken ct = default);
    Task SaveIdentityAsync(BusinessIdentity identity, CancellationToken ct = default);
    Task<IReadOnlyList<DeletedItem>> GetDeletedItemsAsync(CancellationToken ct = default);
    Task RestoreDeletedItemAsync(Guid id, CancellationToken ct = default);
    Task HardDeleteDeletedItemAsync(Guid id, CancellationToken ct = default);
    Task<string?> GetPreferenceAsync(string key, CancellationToken ct = default);
    Task SavePreferenceAsync(string key, string value, CancellationToken ct = default);
    Task<IReadOnlyList<MessageTemplate>> GetMessageTemplatesAsync(CancellationToken ct = default);
    Task<MessageTemplate> SaveMessageTemplateAsync(MessageTemplate messageTemplate, CancellationToken ct = default);
}


