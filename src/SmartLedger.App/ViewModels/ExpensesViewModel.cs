using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;
namespace SmartLedger.App.ViewModels;
public partial class ExpensesViewModel(IAppRepository repo,IAppSession session):ObservableObject
{
 [ObservableProperty] private IReadOnlyList<Expense> expenses=Array.Empty<Expense>();
 [ObservableProperty] private CashRegisterSummary cashSummary=new(0m,0m,0m,0);
 [ObservableProperty] private string category="أخرى";
 [ObservableProperty] private string paymentMethod="Cash";
 [ObservableProperty] private string amountText="";
 [ObservableProperty] private string note="";
 [ObservableProperty] private DateTime expenseDate=DateTime.Today;
 [ObservableProperty] private bool isDialogOpen;
 [ObservableProperty] private bool isSaving;
 [ObservableProperty] private string statusText="";
 public IReadOnlyList<string> SuggestedCategories { get; } = new[] { "إيجار", "كهرباء ومياه", "رواتب وأجور", "صيانة", "نقل وتوزيع", "بوفيه ونثريات", "أخرى" };
 public async Task LoadAsync(){Expenses=await repo.GetExpensesAsync();CashSummary=await repo.GetCashRegisterSummaryAsync(DateTimeOffset.Now.Date,DateTimeOffset.Now);}
 [RelayCommand] private void OpenAdd(){Category="أخرى";PaymentMethod="Cash";AmountText="";Note="";ExpenseDate=DateTime.Today;StatusText="";IsDialogOpen=true;}
 [RelayCommand] private void CloseDialog(){if(!IsSaving)IsDialogOpen=false;}
  [RelayCommand]
  private async Task SaveAsync()
  {
      if (IsSaving) return;
      var currentUser = session.CurrentUser;
      if (currentUser is null)
      {
          StatusText = "تعذر إتمام العملية: جلسة المستخدم غير صالحة أو انتهت صلاحيتها. يرجى إعادة تسجيل الدخول.";
          return;
      }

      if (!SmartLedger.Core.Services.NumericNormalizer.TryParseDecimal(AmountText, out var amount) || amount <= 0m)
      {
          StatusText = "أدخل مبلغًا صحيحًا أكبر من صفر.";
          return;
      }

      IsSaving = true;
      StatusText = "جارٍ حفظ سند الصرف…";
      try
      {
          var expense = new Expense(
              Guid.NewGuid(),
              null,
              Category.Trim(),
              amount,
              Note.Trim(),
              new DateTimeOffset(ExpenseDate.Date),
              currentUser.Id)
          {
              CategoryId = Category.Trim(),
              PaymentMethod = PaymentMethod
          };

          await repo.CreateExpenseAsync(expense);
          StatusText = "تم حفظ سند الصرف بنجاح.";
          IsDialogOpen = false;
          AmountText = "";
          Note = "";
          await LoadAsync();
      }
      catch (InvalidOperationException ex)
      {
          StatusText = $"تعذر حفظ السند: {ex.Message}";
      }
      catch (Exception)
      {
          StatusText = "حدث خطأ غير متوقع أثناء حفظ السند. يرجى مراجعة سجل التدقيق أو مسؤول النظام.";
      }
      finally
      {
          IsSaving = false;
      }
  }
}