using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;
namespace SmartLedger.App.ViewModels;
public partial class ExpensesViewModel(IAppRepository repo,IAppSession session):ObservableObject
{
 [ObservableProperty] private IReadOnlyList<Expense> expenses=Array.Empty<Expense>();
 [ObservableProperty] private CashRegisterSummary cashSummary=new(0m,0m,0m,0);
 [ObservableProperty] private string category="عام";
 [ObservableProperty] private string paymentMethod="Cash";
 [ObservableProperty] private string amountText="";
 [ObservableProperty] private string note="";
 [ObservableProperty] private bool isDialogOpen;
 [ObservableProperty] private bool isSaving;
 [ObservableProperty] private string statusText="";
 public IReadOnlyList<string> SuggestedCategories { get; } = new[] { "إيجار", "كهرباء ومياه", "رواتب وأجور", "صيانة", "نقل وشحن", "بوفيه ونثريات", "مشتريات تشغيلية", "رسوم حكومية", "اتصالات وإنترنت", "عام" };
 public async Task LoadAsync(){Expenses=await repo.GetExpensesAsync();CashSummary=await repo.GetCashRegisterSummaryAsync(DateTimeOffset.Now.Date,DateTimeOffset.Now);}
 [RelayCommand] private void OpenAdd(){Category="عام";PaymentMethod="Cash";AmountText="";Note="";StatusText="";IsDialogOpen=true;}
 [RelayCommand] private void CloseDialog(){if(!IsSaving)IsDialogOpen=false;}
 [RelayCommand] private async Task SaveAsync(){if(IsSaving)return;if(!SmartLedger.Core.Services.NumericNormalizer.TryParseDecimal(AmountText,out var amount)||amount<=0){StatusText="أدخل مبلغًا صحيحًا.";return;}IsSaving=true;StatusText="جارٍ حفظ النفقة…";try{await repo.CreateExpenseAsync(new Expense(Guid.NewGuid(),null,Category.Trim(),amount,Note.Trim(),DateTimeOffset.Now,session.CurrentUser!.Id){CategoryId=Category.Trim(),PaymentMethod=PaymentMethod});StatusText="تم حفظ النفقة بنجاح.";IsDialogOpen=false;AmountText="";Note="";await LoadAsync();}catch(Exception ex){StatusText=ex.Message;}finally{IsSaving=false;}}
}