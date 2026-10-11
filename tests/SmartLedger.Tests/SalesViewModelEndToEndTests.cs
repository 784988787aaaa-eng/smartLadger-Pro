using SmartLedger.App.ViewModels;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Data;
using SmartLedger.Infrastructure.Security;
using Xunit;

namespace SmartLedger.Tests;

public sealed class SalesViewModelEndToEndTests
{
    [Fact]
    public async Task SaveCommand_persists_sale_refreshes_invoice_history_and_deducts_stock()
    {
        var root = Path.Combine(Path.GetTempPath(), "SL_VM_E2E_" + Guid.NewGuid().ToString("N"));
        try
        {
            var database = new Database(root);
            await database.InitializeAsync();
            var users = new UserRepository(database);
            var hasher = new PasswordHasher();
            await users.CreateAdministratorAsync(new AdminSetup("vm-e2e-admin", hasher.Hash("Password!123"), hasher.Hash("recovery"), "hint"));
            var user = await users.FindAsync("vm-e2e-admin");
            Assert.NotNull(user);

            var repository = new AppRepository(database);
            var product = await repository.CreateProductAsync(new Product(
                Guid.NewGuid(), "VM E2E sale item", "VM-E2E-001", "General", "box", "piece",
                12m, 1m, 5m, 10m, 0m, DateTimeOffset.UtcNow));

            var session = new TestSession(new AppUser(user!.Id, user.UserName, user.DisplayName, user.IsAdministrator, user.IsActive));
            var vm = new SalesViewModel(repository, session);
            await vm.LoadAsync();

            Assert.Empty(vm.InvoiceHistory);
            Assert.Equal(10m, Assert.Single(await repository.GetProductsAsync(product.Name)).StockSmallUnits);

            vm.ProductId = product.Id;
            vm.QuantityText = "2";
            vm.PriceText = "5";
            vm.PaymentMode = PaymentMode.Cash;
            vm.Note = "VM end-to-end integration";
            await vm.SaveCommand.ExecuteAsync(null);

            Assert.NotNull(vm.LastSavedInvoice);
            Assert.Contains("تم حفظ الفاتورة", vm.StatusText);
            Assert.Single(vm.InvoiceHistory);
            Assert.Equal(vm.LastSavedInvoice!.InvoiceNumber, Assert.Single(vm.InvoiceHistory).InvoiceNumber);
            Assert.Equal(8m, Assert.Single(await repository.GetProductsAsync(product.Name)).StockSmallUnits);
            Assert.Equal(10m, vm.LastSavedInvoice.Total);
            Assert.False(vm.IsSaving);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class TestSession(AppUser user) : IAppSession
    {
        public AppUser? CurrentUser { get; private set; } = user;
        public bool IsAuthenticated => CurrentUser is not null;
        public void Start(User value) => CurrentUser = new AppUser(value.Id, value.UserName, value.DisplayName, value.IsAdministrator, value.IsActive);
        public void Logout() => CurrentUser = null;
    }
}
