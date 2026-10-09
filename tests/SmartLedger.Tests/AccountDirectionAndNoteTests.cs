using Microsoft.Data.Sqlite;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Data;
using Xunit;

namespace SmartLedger.Tests;

public sealed class AccountDirectionAndNoteTests
{
    [Fact]
    public async Task OpeningBalancesAndAccountNotesPersistWithoutChangingStoredAmounts()
    {
        var root = Path.Combine(Path.GetTempPath(), "SL_AccountAudit_" + Guid.NewGuid().ToString("N"));
        try
        {
            var db = new Database(root);
            await db.InitializeAsync();
            var users = new UserRepository(db);
            var hasher = new PasswordHasher();
            await users.CreateAdministratorAsync(new AdminSetup("admin", hasher.Hash("Password!123"), hasher.Hash("recovery"), "hint"));
            var admin = await users.FindAsync("admin");
            Assert.NotNull(admin);
            var session = new AppSession();
            session.Start(admin!);
            var repo = new AppRepository(db, session);
            var books = await repo.GetBooksAsync();
            var customers = books.Single(x => x.Kind == BookKind.Customers);
            var suppliers = books.Single(x => x.Kind == BookKind.Suppliers);

            var supplierPayable = await repo.CreatePersonWithOpeningBalanceAsync(suppliers.Id, "supplier payable", "", "Supplier account note survives reopen", 120000m, OperationType.DebtToUs);
            var supplierPayableReloaded = (await repo.GetPeopleAsync(suppliers.Id)).Single(x => x.PersonId == supplierPayable.Id);
            Assert.Equal(-120000m, supplierPayableReloaded.NetBalance);
            Assert.Equal(0m, supplierPayableReloaded.OwedToUs);
            Assert.Equal(120000m, supplierPayableReloaded.WeOwe);
            Assert.Equal("Supplier account note survives reopen", supplierPayableReloaded.Person.Note);
            Assert.Equal(OperationType.DebtToUs, Assert.Single(await repo.GetOperationsAsync(supplierPayable.Id)).Type);

            var supplierReceivable = await repo.CreatePersonWithOpeningBalanceAsync(suppliers.Id, "supplier receivable", "", "", 450m, OperationType.DebtOnUs);
            Assert.Equal(450m, (await repo.GetPeopleAsync(suppliers.Id)).Single(x => x.PersonId == supplierReceivable.Id).NetBalance);

            var customerReceivable = await repo.CreatePersonWithOpeningBalanceAsync(customers.Id, "customer receivable", "", "", 700m, OperationType.DebtOnUs);
            Assert.Equal(700m, (await repo.GetPeopleAsync(customers.Id)).Single(x => x.PersonId == customerReceivable.Id).NetBalance);

            var customerPayable = await repo.CreatePersonWithOpeningBalanceAsync(customers.Id, "customer payable", "", "", 300m, OperationType.DebtToUs);
            Assert.Equal(-300m, (await repo.GetPeopleAsync(customers.Id)).Single(x => x.PersonId == customerPayable.Id).NetBalance);

            var zero = await repo.CreatePersonWithOpeningBalanceAsync(customers.Id, "zero account", "", "zero note", 0m, OperationType.DebtToUs);
            Assert.Empty(await repo.GetOperationsAsync(zero.Id));
            var zeroReloaded = (await repo.GetPeopleAsync(customers.Id)).Single(x => x.PersonId == zero.Id);
            Assert.Equal(0m, zeroReloaded.NetBalance);
            Assert.Equal("zero note", zeroReloaded.Person.Note);

            await repo.CreateOperationAsync(new LedgerOperation(Guid.NewGuid(), supplierPayable.Id, OperationType.Payment, 20000m, "payment", DateTimeOffset.UtcNow, admin!.Id));
            Assert.Equal(-100000m, (await repo.GetPeopleAsync(suppliers.Id)).Single(x => x.PersonId == supplierPayable.Id).NetBalance);
            await repo.CreateOperationAsync(new LedgerOperation(Guid.NewGuid(), supplierPayable.Id, OperationType.Receive, 10000m, "receipt", DateTimeOffset.UtcNow, admin.Id));
            Assert.Equal(-110000m, (await repo.GetPeopleAsync(suppliers.Id)).Single(x => x.PersonId == supplierPayable.Id).NetBalance);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
