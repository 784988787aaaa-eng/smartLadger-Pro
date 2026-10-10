using SmartLedger.Core.Models;
using SmartLedger.Infrastructure.Data;
using Xunit;

namespace SmartLedger.Tests;

public sealed class PersonPersistenceTests
{
    [Fact]
    public async Task CreateAndUpdate_RoundTripsAllAccountMetadata()
    {
        var root=Path.Combine(Path.GetTempPath(),"SmartLedgerTests",Guid.NewGuid().ToString("N"));
        try
        {
            var database=new Database(root);
            await database.InitializeAsync();
            var repository=new AppRepository(database);
            var book=(await repository.GetBooksAsync()).First(x=>x.Kind==BookKind.Customers);
            var now=DateTimeOffset.UtcNow;
            var person=await repository.CreatePersonAsync(new Person(Guid.NewGuid(),book.Id,"حساب تجريبي","0777123456","ملاحظة",now)
            {
                AccountCode="ACC-ROUNDTRIP01",SecondaryPhone="0777000000",Address="صنعاء",CreditLimit=1250m,
                Nature=AccountNature.Credit,WhatsAppEnabled=true
            });
            await repository.UpdatePersonAsync(person with
            {
                SecondaryPhone="0777999999",Address="تعز",CreditLimit=2400m,
                Nature=AccountNature.Debit,WhatsAppEnabled=false,Note="تم التحديث"
            });
            var saved=(await repository.GetPeopleAsync(book.Id)).Single(x=>x.Person.Id==person.Id).Person;
            Assert.Equal("ACC-ROUNDTRIP01",saved.AccountCode);
            Assert.Equal("0777999999",saved.SecondaryPhone);
            Assert.Equal("تعز",saved.Address);
            Assert.Equal(2400m,saved.CreditLimit);
            Assert.Equal(AccountNature.Debit,saved.Nature);
            Assert.False(saved.WhatsAppEnabled);
            Assert.Equal("تم التحديث",saved.Note);
            Assert.True(saved.UpdatedAt>=person.UpdatedAt);
        }
        finally
        {
            if(Directory.Exists(root)) Directory.Delete(root,true);
        }
    }

    [Fact]
    public async Task CreatePerson_GeneratesAccountCodeAndRejectsDuplicates()
    {
        var root=Path.Combine(Path.GetTempPath(),"SmartLedgerTests",Guid.NewGuid().ToString("N"));
        try
        {
            var database=new Database(root);
            await database.InitializeAsync();
            var repository=new AppRepository(database);
            var book=(await repository.GetBooksAsync()).First(x=>x.Kind==BookKind.Customers);
            var now=DateTimeOffset.UtcNow;
            var first=await repository.CreatePersonAsync(new Person(Guid.NewGuid(),book.Id,"الأول","","",now));
            var second=await repository.CreatePersonAsync(new Person(Guid.NewGuid(),book.Id,"الثاني","","",now));
            Assert.StartsWith("ACC-",first.AccountCode,StringComparison.Ordinal);
            Assert.StartsWith("ACC-",second.AccountCode,StringComparison.Ordinal);
            Assert.NotEqual(first.AccountCode,second.AccountCode);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>repository.CreatePersonAsync(
                new Person(Guid.NewGuid(),book.Id,"الثالث","","",now){AccountCode=first.AccountCode}));
        }
        finally
        {
            if(Directory.Exists(root)) Directory.Delete(root,true);
        }
    }
}
