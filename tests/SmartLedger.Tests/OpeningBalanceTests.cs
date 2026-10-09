using Xunit;
using Microsoft.Data.Sqlite;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Data;

namespace SmartLedger.Tests;

public sealed class OpeningBalanceTests
{
    [Fact]
    public async Task OpeningBalance_UsesLedgerAsSingleSourceOfTruth()
    {
        var root=Path.Combine(Path.GetTempPath(),"SL_"+Guid.NewGuid().ToString("N"));
        try
        {
            var db=new Database(root);
            await db.InitializeAsync();
            var users=new UserRepository(db);
            var hasher=new PasswordHasher();
            await users.CreateAdministratorAsync(new AdminSetup("admin",hasher.Hash("Password!123"),hasher.Hash("recovery"),"hint"));
            var admin=await users.FindAsync("admin");
            Assert.NotNull(admin);
            var session=new AppSession();
            session.Start(admin!);
            var repo=new AppRepository(db,session);
            var book=(await repo.GetBooksAsync()).First(x=>x.Kind==BookKind.Customers);

            var noOpening=await repo.CreatePersonWithOpeningBalanceAsync(book.Id,"بدون افتتاحي","1","",0m,OperationType.DebtToUs);
            Assert.Empty(await repo.GetOperationsAsync(noOpening.Id));

            var ours=await repo.CreatePersonWithOpeningBalanceAsync(book.Id,"رصيد لنا","2","",250m,OperationType.DebtOnUs);
            var oursOp=(await repo.GetOperationsAsync(ours.Id)).Single();
            Assert.Equal(250m,oursOp.Amount);
            Assert.Equal(OperationType.DebtOnUs,oursOp.Type);
            Assert.Equal("رصيد افتتاحي",oursOp.Note);
            var oursSummary=(await repo.GetPeopleAsync(book.Id)).Single(x=>x.PersonId==ours.Id);
            Assert.Equal(250m,oursSummary.NetBalance);
            Assert.Equal(250m,oursSummary.OwedToUs);
            Assert.Equal(0m,oursSummary.WeOwe);

            var against=await repo.CreatePersonWithOpeningBalanceAsync(book.Id,"رصيد علينا","3","",100m,OperationType.DebtToUs);
            var againstSummary=(await repo.GetPeopleAsync(book.Id)).Single(x=>x.PersonId==against.Id);
            Assert.Equal(-100m,againstSummary.NetBalance);
            Assert.Equal(0m,againstSummary.OwedToUs);
            Assert.Equal(100m,againstSummary.WeOwe);

            await repo.CreateOperationAsync(new LedgerOperation(Guid.NewGuid(),ours.Id,OperationType.Receive,50m,"استلام اختبار",DateTimeOffset.UtcNow,admin!.Id));
            Assert.Equal(200m,(await repo.GetPeopleAsync(book.Id)).Single(x=>x.PersonId==ours.Id).NetBalance);
            await repo.CreateOperationAsync(new LedgerOperation(Guid.NewGuid(),ours.Id,OperationType.Payment,25m,"سداد اختبار",DateTimeOffset.UtcNow,admin.Id));
            Assert.Equal(225m,(await repo.GetPeopleAsync(book.Id)).Single(x=>x.PersonId==ours.Id).NetBalance);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if(Directory.Exists(root)) Directory.Delete(root,true);
        }
    }
}
