using System.Globalization;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;
namespace SmartLedger.Core.Services;
public sealed class BalanceEngine:IBalanceEngine
{
 public decimal CalculateNetBalance(IEnumerable<LedgerOperation> operations){decimal net=0;foreach(var op in operations)net+=op.Type switch{OperationType.DebtOnUs or OperationType.Sale=>op.Amount,OperationType.Receive or OperationType.Return=>-op.Amount,OperationType.DebtToUs or OperationType.Purchase=>-op.Amount,OperationType.Payment or OperationType.Settlement=>op.Amount,_=>0};return decimal.Round(net,2,MidpointRounding.AwayFromZero);}
 public (decimal OwedToUs,decimal WeOwe) Split(decimal netBalance)=>netBalance>=0?(netBalance,0):(0,decimal.Abs(netBalance));
}
public sealed class InventoryEngine:IInventoryEngine
{
 public decimal ToSmallUnits(decimal quantity,decimal conversionFactor){ArgumentOutOfRangeException.ThrowIfNegative(quantity);ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(conversionFactor,0);return decimal.Round(quantity*conversionFactor,6,MidpointRounding.AwayFromZero);}
 public decimal ToLargeUnits(decimal smallUnits,decimal conversionFactor){ArgumentOutOfRangeException.ThrowIfNegative(smallUnits);ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(conversionFactor,0);return decimal.Round(smallUnits/conversionFactor,6,MidpointRounding.AwayFromZero);}
 public bool IsLow(decimal stockSmallUnits,decimal alertThreshold)=>stockSmallUnits<=alertThreshold;
}
public static class NumericNormalizer
{
 public static string NormalizeDigits(string value){if(string.IsNullOrWhiteSpace(value))return string.Empty;var chars=value.Trim().Replace(",",string.Empty).Replace("\u066C",string.Empty).ToCharArray();for(var i=0;i<chars.Length;i++)chars[i]=chars[i] switch{'\u0660'=>'0','\u0661'=>'1','\u0662'=>'2','\u0663'=>'3','\u0664'=>'4','\u0665'=>'5','\u0666'=>'6','\u0667'=>'7','\u0668'=>'8','\u0669'=>'9',_=>chars[i]};return new string(chars);}
 public static bool TryParseDecimal(string? value,out decimal amount)=>decimal.TryParse(NormalizeDigits(value??string.Empty),NumberStyles.Number,CultureInfo.InvariantCulture,out amount);
}
