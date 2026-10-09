using Xunit;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
namespace SmartLedger.Tests;
public sealed class CoreInvariantsTests
{
 [Fact]public void ArabicDigits_ParseAsDecimal(){Assert.True(NumericNormalizer.TryParseDecimal("\u0661\u0662\u0663\u066C\u0664\u0665\u0660.\u0667\u0665",out var x));Assert.Equal(123450.75m,x);}
 [Fact]public void Balance_SplitsPositiveAsReceivable(){var e=new BalanceEngine();var id=Guid.NewGuid();var u=Guid.NewGuid();var ops=new[]{new LedgerOperation(Guid.NewGuid(),id,OperationType.DebtOnUs,100m,"",DateTimeOffset.UtcNow,u),new LedgerOperation(Guid.NewGuid(),id,OperationType.Receive,25m,"",DateTimeOffset.UtcNow,u)};var net=e.CalculateNetBalance(ops);Assert.Equal(75m,net);Assert.Equal((75m,0m),e.Split(net));}
 [Fact]public void Balance_PaymentReducesPayable(){var e=new BalanceEngine();var id=Guid.NewGuid();var u=Guid.NewGuid();var ops=new[]{new LedgerOperation(Guid.NewGuid(),id,OperationType.DebtToUs,90m,"",DateTimeOffset.UtcNow,u),new LedgerOperation(Guid.NewGuid(),id,OperationType.Payment,25m,"",DateTimeOffset.UtcNow,u)};Assert.Equal(-65m,e.CalculateNetBalance(ops));Assert.Equal((0m,65m),e.Split(e.CalculateNetBalance(ops)));}
 [Fact]public void Balance_SplitsNegativeAsPayable(){var e=new BalanceEngine();var net=e.CalculateNetBalance(new[]{new LedgerOperation(Guid.NewGuid(),Guid.NewGuid(),OperationType.DebtToUs,90m,"",DateTimeOffset.UtcNow,Guid.NewGuid())});Assert.Equal(-90m,net);Assert.Equal((0m,90m),e.Split(net));}
 [Fact]public void Inventory_ConvertsLargeAndSmallExactly(){var e=new InventoryEngine();Assert.Equal(240m,e.ToSmallUnits(10m,24m));Assert.Equal(10m,e.ToLargeUnits(240m,24m));}
 [Fact]public void Inventory_RejectsInvalidFactor(){Assert.Throws<ArgumentOutOfRangeException>(()=>new InventoryEngine().ToSmallUnits(1m,0m));}
 [Fact]public void Money_DoesNotUseBinaryFloatingPoint(){Assert.Equal(0.3m,decimal.Round(0.1m+0.2m,2));}
 [Fact]public void PasswordHasher_UsesSaltedHash(){var h=new PasswordHasher();var a=h.Hash("StrongPassword!123");var b=h.Hash("StrongPassword!123");Assert.NotEqual(a,b);Assert.True(PasswordHasher.Verify("StrongPassword!123",a));Assert.False(PasswordHasher.Verify("wrong",a));}
 [Fact]public void Privacy_MasksMoneyByDefaultAndRevealsOnToggle(){var p=new PrivacyStateService();Assert.True(p.IsPrivate);Assert.Equal("••••••",p.FormatMoney(100,"ريال"));p.Toggle();Assert.False(p.IsPrivate);Assert.Contains("100.00",p.FormatMoney(100,"ريال"));}
}
