using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SmartLedger.Infrastructure.FinalRelease;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Integrations;

namespace SmartLedger.App.ViewModels;

public sealed record AccountStatementRow(Guid Id,string Note,string TypeLabel,decimal Amount,DateTimeOffset OccurredAt,decimal CumulativeBalance,LedgerOperation Operation)
{
    public string? ReferenceNumber => Operation.ReferenceNumber;
    public Guid? SourceInvoiceId => Operation.SourceInvoiceId;
    public Guid? SourceStockMovementId => Operation.SourceStockMovementId;
    public string DocumentTypeLabel => Operation.TypeLabel;
    public decimal Debit => Operation.Type is OperationType.DebtOnUs or OperationType.Sale or OperationType.Payment or OperationType.Settlement ? Amount : 0m;
    public decimal Credit => Operation.Type is OperationType.Receive or OperationType.Return or OperationType.DebtToUs or OperationType.Purchase ? Amount : 0m;
}

public partial class AccountsViewModel(IAppRepository repo,IAppSession session):ObservableObject
{
    [ObservableProperty] private IReadOnlyList<PersonSummary> people=Array.Empty<PersonSummary>();
    [ObservableProperty] private IReadOnlyList<Book> books=Array.Empty<Book>();
    [ObservableProperty] private IReadOnlyList<Book> bookGroups=Array.Empty<Book>();
    [ObservableProperty] private IReadOnlyList<Book> customBookGroups=Array.Empty<Book>();
    [ObservableProperty] private Guid? selectedBookId;
    [ObservableProperty] private bool isBookDialogOpen;
    [ObservableProperty] private bool isBookSaving;
    [ObservableProperty] private Book? editingBook;
    [ObservableProperty] private string bookNameText="";
    [ObservableProperty] private string bookDialogStatus="";
    [ObservableProperty] private Book? selectedGroupBook;
    [ObservableProperty] private string selectedAccountType="عميل";
    [ObservableProperty] private PersonSummary? selectedPerson;
    [ObservableProperty] private string search="";
    [ObservableProperty] private bool isDialogOpen;
    [ObservableProperty] private bool isSaving;
    [ObservableProperty] private string newName="";
    [ObservableProperty] private string newAccountCode="";
    [ObservableProperty] private string newPhone="";
    [ObservableProperty] private string newNote="";
    [ObservableProperty] private string newSecondaryPhone="";
    [ObservableProperty] private string newAddress="";
    [ObservableProperty] private string newCreditLimit="";
    [ObservableProperty] private AccountNature newAccountNature=AccountNature.Flexible;
    [ObservableProperty] private bool newWhatsAppEnabled=true;
    [ObservableProperty] private string openingBalanceText="";
    [ObservableProperty] private bool openingBalanceForUs=true;
    [ObservableProperty] private bool openingBalanceAgainstUs;
    [ObservableProperty] private string statusText="";
    [ObservableProperty] private bool isAccountOpen;
    [ObservableProperty] private IReadOnlyList<LedgerOperation> selectedAccountOperations=Array.Empty<LedgerOperation>();
    [ObservableProperty] private IReadOnlyList<AccountStatementRow> selectedAccountStatementRows=Array.Empty<AccountStatementRow>();
    [ObservableProperty] private string accountStatementStatus="";
    [ObservableProperty] private DateTime? statementFromDate;
    [ObservableProperty] private DateTime? statementToDate;
    [ObservableProperty] private AccountStatementKindFilter statementKindFilter=AccountStatementKindFilter.All;
    [ObservableProperty] private string statementSearch="";
    [ObservableProperty] private string currency="ريال";
    private bool syncingGroupAndType;

    public string SelectedAccountTitle => SelectedPerson is null ? "الحساب" : $"حساب {SelectedPerson.Person.Name}";
    public string SelectedAccountTypeLabel => SelectedPerson is null ? "" : SelectedPerson.BookName;
    public string SelectedAccountNote => SelectedPerson?.Person.Note ?? "";
    public decimal SelectedAccountBalance => SelectedPerson?.NetBalance ?? 0m;
    public decimal SelectedAccountOwedToUs => SelectedPerson?.OwedToUs ?? 0m;
    public decimal SelectedAccountWeOwe => SelectedPerson?.WeOwe ?? 0m;
    public decimal OpeningBalance => SelectedAccountOperations.Where(x=>x.Note=="رصيد افتتاحي").Sum(LedgerBalanceCalculator.SignedEffect);
    public decimal TotalDebit => SelectedAccountOperations.Where(x=>x.Note!="رصيد افتتاحي" && (x.Type is OperationType.DebtOnUs or OperationType.Sale or OperationType.Payment or OperationType.Settlement)).Sum(x=>x.Amount);
    public decimal TotalCredit => SelectedAccountOperations.Where(x=>x.Note!="رصيد افتتاحي" && (x.Type is OperationType.Receive or OperationType.Return or OperationType.DebtToUs or OperationType.Purchase)).Sum(x=>x.Amount);
    public decimal NetBalance => LedgerBalanceCalculator.CalculateNetBalance(SelectedAccountOperations);
    public string BalanceStatusText => NetBalance<0 ? "له (دائن)" : NetBalance>0 ? "عليه (مدين)" : "رصيد خالص";
    public string BalanceStatusColor => NetBalance<0 ? "#10B981" : NetBalance>0 ? "#EF4444" : "#64748B";
    public decimal TotalOwedToUs => People.Sum(x => x.OwedToUs);
    public decimal TotalWeOwe => People.Sum(x => x.WeOwe);
    public int VisibleAccountsCount => People.Count;
    public Guid AllBookId => Books.FirstOrDefault(x=>x.Kind==BookKind.All)?.Id ?? Guid.Empty;
    public Guid CustomersBookId => Books.FirstOrDefault(x=>x.Kind==BookKind.Customers)?.Id ?? Guid.Empty;
    public Guid SuppliersBookId => Books.FirstOrDefault(x=>x.Kind==BookKind.Suppliers)?.Id ?? Guid.Empty;
    public Guid EmployeesBookId => Books.FirstOrDefault(x=>x.Kind==BookKind.Employees)?.Id ?? Guid.Empty;
    public Guid OtherBookId => Books.FirstOrDefault(x=>x.Kind==BookKind.Other)?.Id ?? Guid.Empty;
    public string BookDialogTitle => EditingBook is null ? "إضافة تصنيف" : "تعديل التصنيف";

    public async Task LoadAsync()
    {
        var identity=await repo.GetIdentityAsync();
        Currency=string.IsNullOrWhiteSpace(identity.Currency) ? "ريال" : identity.Currency.Trim();
        Books=await repo.GetBooksAsync();
        BookGroups=Books.Where(x=>x.IsSystem && x.Kind!=BookKind.All).OrderBy(x=>x.SortOrder).ToArray();
        CustomBookGroups=Books.Where(x=>!x.IsSystem && x.Kind==BookKind.Custom).OrderBy(x=>x.SortOrder).ThenBy(x=>x.Name).ToArray();
        OnPropertyChanged(nameof(AllBookId));
        OnPropertyChanged(nameof(CustomersBookId));
        OnPropertyChanged(nameof(SuppliersBookId));
        OnPropertyChanged(nameof(EmployeesBookId));
        OnPropertyChanged(nameof(OtherBookId));
        // عند فتح شاشة الدفاتر والحسابات يكون الفلتر الافتراضي «الكل»،
        // مع إبقاء دفتر العملاء كنوع افتراضي لإضافة حساب جديد.
        SelectedGroupBook=BookGroups.FirstOrDefault(x=>x.Kind==BookKind.Customers)??BookGroups.FirstOrDefault();
        SelectedBookId=Books.FirstOrDefault(x=>x.Kind==BookKind.All && x.IsSystem)?.Id ?? Guid.Empty;
        SyncAccountTypeFromBook(SelectedGroupBook);
        await RefreshAsync();
    }

    partial void OnSelectedPersonChanged(PersonSummary? value)
    {
        OnPropertyChanged(nameof(SelectedAccountTitle));
        OnPropertyChanged(nameof(SelectedAccountTypeLabel));
        OnPropertyChanged(nameof(SelectedAccountNote));
        OnPropertyChanged(nameof(SelectedAccountBalance));
        OnPropertyChanged(nameof(SelectedAccountOwedToUs));
        OnPropertyChanged(nameof(SelectedAccountWeOwe));
    }

    partial void OnSearchChanged(string value)=>_=RefreshAsync();
    partial void OnStatementFromDateChanged(DateTime? value)=>ApplyStatementFilters();
    partial void OnStatementToDateChanged(DateTime? value)=>ApplyStatementFilters();
    partial void OnStatementKindFilterChanged(AccountStatementKindFilter value)=>ApplyStatementFilters();
    partial void OnStatementSearchChanged(string value)=>ApplyStatementFilters();
    public IReadOnlyList<AccountStatementKindFilter> StatementKindOptions { get; } = Enum.GetValues<AccountStatementKindFilter>();
    partial void OnSelectedBookIdChanged(Guid? value)
    {
        if(syncingGroupAndType) return;
        _=RefreshAsync();
    }

    partial void OnSelectedGroupBookChanged(Book? value)
    {
        if(value is null || syncingGroupAndType) return;
        try
        {
            syncingGroupAndType=true;
            SelectedBookId=value.Id;
            SyncAccountTypeFromBook(value);
            SyncOpeningDirectionFromBook(value);
        }
        finally
        {
            syncingGroupAndType=false;
        }
    }

    private void SyncOpeningDirectionFromBook(Book? book)
    {
        if(book?.Kind==BookKind.Suppliers)
        {
            OpeningBalanceForUs=false;
            OpeningBalanceAgainstUs=true;
        }
        else
        {
            OpeningBalanceForUs=true;
            OpeningBalanceAgainstUs=false;
        }
    }

    partial void OnSelectedAccountTypeChanged(string value)
    {
        if(syncingGroupAndType) return;
        var kind=value switch
        {
            "عميل"=>BookKind.Customers,
            "مورد"=>BookKind.Suppliers,
            "عامل"=>BookKind.Employees,
            _=>BookKind.Other
        };
        var book=BookGroups.FirstOrDefault(x=>x.Kind==kind);
        if(book is null || SelectedGroupBook?.Id==book.Id) return;
        try
        {
            syncingGroupAndType=true;
            SelectedGroupBook=book;
            SelectedBookId=book.Id;
            SyncOpeningDirectionFromBook(book);
        }
        finally
        {
            syncingGroupAndType=false;
        }
    }

    private void SyncAccountTypeFromBook(Book? book)
    {
        SelectedAccountType=book?.Kind switch
        {
            BookKind.Customers=>"عميل",
            BookKind.Suppliers=>"مورد",
            BookKind.Employees=>"عامل",
            _=>"أخرى"
        };
    }

    public IReadOnlyList<string> AccountTypeOptions { get; } = new[] { "عميل", "مورد", "عامل", "أخرى" };

    [RelayCommand]
    private async Task SelectBookKind(BookKind kind)
    {
        var book=Books.FirstOrDefault(x=>x.Kind==kind && x.IsSystem);
        if(book is null) return;
        SelectedGroupBook=book;
        SelectedBookId=book.Kind==BookKind.All ? book.Id : book.Id;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task SelectCustomBook(Book book)
    {
        if(book is null || book.IsSystem || book.Kind!=BookKind.Custom) return;
        SelectedGroupBook=book;
        SelectedBookId=book.Id;
        await RefreshAsync();
    }

    [RelayCommand]
    private void OpenBookAdd()
    {
        if(IsBookSaving) return;
        EditingBook=null;
        BookNameText="";
        BookDialogStatus="";
        IsBookDialogOpen=true;
        OnPropertyChanged(nameof(BookDialogTitle));
    }

    [RelayCommand]
    private void OpenBookEdit(Book? book)
    {
        if(IsBookSaving || book is null || book.IsSystem || book.Kind!=BookKind.Custom) return;
        EditingBook=book;
        BookNameText=book.Name;
        BookDialogStatus="";
        IsBookDialogOpen=true;
        OnPropertyChanged(nameof(BookDialogTitle));
    }

    [RelayCommand]
    private void CloseBookDialog()
    {
        if(IsBookSaving) return;
        IsBookDialogOpen=false;
    }

    [RelayCommand]
    private async Task SaveBookAsync()
    {
        if(IsBookSaving) return;
        var name=BookNameText.Trim();
        if(name.Length<2){BookDialogStatus="أدخل اسمًا واضحًا للتصنيف.";return;}
        if(name.Length>60){BookDialogStatus="اسم التصنيف طويل جدًا.";return;}
        if(Books.Any(x=>string.Equals(x.Name.Trim(),name,StringComparison.OrdinalIgnoreCase) && x.Id!=(EditingBook?.Id ?? Guid.Empty)))
        {
            BookDialogStatus="يوجد تصنيف بهذا الاسم بالفعل.";
            return;
        }

        try
        {
            IsBookSaving=true;
            BookDialogStatus=EditingBook is null ? "جارٍ إنشاء التصنيف…" : "جارٍ حفظ التعديل…";
            var previousId=SelectedBookId;
            Book created;
            if(EditingBook is null)
            {
                created=await repo.CreateBookAsync(name,"#3B82F6","folder");
            }
            else
            {
                await repo.RenameBookAsync(EditingBook.Id,name);
                created=EditingBook with { Name=name };
            }

            Books=await repo.GetBooksAsync();
            BookGroups=Books.Where(x=>x.IsSystem && x.Kind!=BookKind.All).OrderBy(x=>x.SortOrder).ToArray();
            CustomBookGroups=Books.Where(x=>!x.IsSystem && x.Kind==BookKind.Custom).OrderBy(x=>x.SortOrder).ThenBy(x=>x.Name).ToArray();
            OnPropertyChanged(nameof(AllBookId));
            OnPropertyChanged(nameof(CustomersBookId));
            OnPropertyChanged(nameof(SuppliersBookId));
            OnPropertyChanged(nameof(EmployeesBookId));
            OnPropertyChanged(nameof(OtherBookId));
            var selected=Books.FirstOrDefault(x=>x.Id==created.Id) ?? Books.FirstOrDefault(x=>x.Id==previousId);
            if(selected is not null)
            {
                SelectedGroupBook=selected;
                SelectedBookId=selected.Id;
                SyncAccountTypeFromBook(selected);
            }
            IsBookDialogOpen=false;
            await RefreshAsync();
        }
        catch(Exception ex)
        {
            BookDialogStatus=ex.Message;
        }
        finally
        {
            IsBookSaving=false;
        }
    }

    [RelayCommand]
    private async Task DeleteBookAsync(Book? book)
    {
        if(IsBookSaving || book is null || book.IsSystem || book.Kind!=BookKind.Custom) return;
        var answer=System.Windows.MessageBox.Show(
            $"سيتم حذف التصنيف «{book.Name}» مع إبقاء الحسابات ونقلها إلى «الكل». هل تريد المتابعة؟",
            "حذف التصنيف",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if(answer!=System.Windows.MessageBoxResult.Yes) return;

        try
        {
            IsBookSaving=true;
            await repo.DeleteBookAsync(book.Id,false);
            Books=await repo.GetBooksAsync();
            BookGroups=Books.Where(x=>x.IsSystem && x.Kind!=BookKind.All).OrderBy(x=>x.SortOrder).ToArray();
            CustomBookGroups=Books.Where(x=>!x.IsSystem && x.Kind==BookKind.Custom).OrderBy(x=>x.SortOrder).ThenBy(x=>x.Name).ToArray();
            OnPropertyChanged(nameof(AllBookId));
            OnPropertyChanged(nameof(CustomersBookId));
            OnPropertyChanged(nameof(SuppliersBookId));
            OnPropertyChanged(nameof(EmployeesBookId));
            OnPropertyChanged(nameof(OtherBookId));
            var fallback=Books.FirstOrDefault(x=>x.Kind==BookKind.Other) ?? Books.FirstOrDefault(x=>x.Kind==BookKind.Customers);
            if(fallback is not null)
            {
                SelectedGroupBook=fallback;
                SelectedBookId=fallback.Id;
                SyncAccountTypeFromBook(fallback);
            }
            await RefreshAsync();
        }
        catch(Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message,"تعذر حذف التصنيف",System.Windows.MessageBoxButton.OK,System.Windows.MessageBoxImage.Warning);
        }
        finally
        {
            IsBookSaving=false;
        }
    }

    private async Task RefreshAsync()
    {
        People=await repo.GetPeopleAsync(SelectedBookId,Normalize(Search));
        OnPropertyChanged(nameof(TotalOwedToUs));
        OnPropertyChanged(nameof(TotalWeOwe));
        OnPropertyChanged(nameof(VisibleAccountsCount));
    }

    [RelayCommand]
    private Task OpenReceiptForSelectedAccountAsync() => OpenQuickOperationAsync(OperationType.Receive);

    [RelayCommand]
    private Task OpenPaymentForSelectedAccountAsync() => OpenQuickOperationAsync(OperationType.Payment);

    [RelayCommand]
    private Task OpenDebtForSelectedAccountAsync() => OpenQuickOperationAsync(OperationType.DebtOnUs);

    private async Task OpenQuickOperationAsync(OperationType type)
    {
        var person = SelectedPerson;
        if (person is null)
        {
            System.Windows.MessageBox.Show("ظٹط±ط¬ظ‰ طھط­ط¯ظٹط¯ ط­ط³ط§ط¨ ط£ظˆظ„ط§ظ‹.", "ط§ظ„ط¹ظ…ظ„ظٹط§طھ ط§ظ„ظ…ط§ظ„ظٹط©",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        var main = System.Windows.Application.Current?.MainWindow?.DataContext as MainViewModel;
        if (main is null) return;

        await main.OpenOperationForPersonAsync(person.PersonId);
        main.Operations.SelectedType = type;
    }
    [RelayCommand]
    private async Task OpenOperationForAccountAsync(PersonSummary? person)
    {
        if(person is null) return;
        var main=System.Windows.Application.Current?.MainWindow?.DataContext as MainViewModel;
        if(main is null) return;
        await main.OpenOperationForPersonAsync(person.PersonId);
    }

    [RelayCommand]
    private async Task OpenAccountForPersonAsync(PersonSummary? person)
    {
        if(person is null) return;
        SelectedPerson=person;
        await OpenAccountAsync();
    }

    [RelayCommand]
    private void OpenAdd()
    {
        NewName="";
        NewAccountCode="ACC-"+Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        NewPhone="";
        NewNote="";
        NewSecondaryPhone="";
        NewAddress="";
        NewCreditLimit="";
        NewAccountNature=AccountNature.Flexible;
        NewWhatsAppEnabled=true;
        OpeningBalanceText="";
        OpeningBalanceForUs=false;
        OpeningBalanceAgainstUs=false;
        StatusText="";
        IsSaving=false;
        var defaultCustomerBook=BookGroups.FirstOrDefault(x=>x.Kind==BookKind.Customers)??BookGroups.FirstOrDefault();
        SelectedGroupBook=defaultCustomerBook;
        SelectedBookId=defaultCustomerBook?.Id;
        SyncAccountTypeFromBook(defaultCustomerBook);
        SyncOpeningDirectionFromBook(defaultCustomerBook);
        IsDialogOpen=true;
    }

    [RelayCommand]
    private void CloseDialog()
    {
        if(IsSaving) return;
        IsDialogOpen=false;
    }

    [RelayCommand] private void SelectOpeningForUs(){OpeningBalanceForUs=true;OpeningBalanceAgainstUs=false;}
    [RelayCommand] private void SelectOpeningAgainstUs(){OpeningBalanceForUs=false;OpeningBalanceAgainstUs=true;}

    [RelayCommand]
    private Task SaveNewAsync() => SaveAccountAsync(false);

    [RelayCommand]
    private Task SaveAndNewAsync() => SaveAccountAsync(true);

    [RelayCommand]
    private Task SaveAndCloseAsync() => SaveAccountAsync(false);

    private async Task SaveAccountAsync(bool continueEntry)
    {
        if(IsSaving) return;
        var name=NewName.Trim();
        if(name.Length<2){StatusText="اسم الحساب مطلوب ويجب ألا يقل عن حرفين.";return;}
        var phone=NewPhone.Trim();
        if(!string.IsNullOrWhiteSpace(phone))
        {
            var digits=new string(phone.Where(char.IsDigit).ToArray());
            if(digits.Length is < 7 or > 15 || phone.Any(ch=>!char.IsDigit(ch) && ch is not ('+' or ' ' or '-' or '(' or ')' or '.')))
            {StatusText="رقم الهاتف غير صالح؛ استخدم 7 إلى 15 رقماً.";return;}
            if(People.Any(x=>!string.IsNullOrWhiteSpace(x.Person.Phone) && string.Equals(NormalizePhoneForValidation(x.Person.Phone),NormalizePhoneForValidation(phone),StringComparison.Ordinal)))
            {StatusText="رقم الهاتف مسجل مسبقاً في قائمة الحسابات المحملة.";return;}
        }

        decimal? creditLimit=null;
        if(!string.IsNullOrWhiteSpace(NewCreditLimit))
        {
            if(!TryParseFlexibleDecimal(NewCreditLimit,out var parsedLimit) || parsedLimit<=0)
            {StatusText="حد الائتمان يجب أن يكون مبلغاً موجباً أو يترك فارغاً.";return;}
            creditLimit=parsedLimit;
        }

        decimal opening=0;
        if(!string.IsNullOrWhiteSpace(OpeningBalanceText) && !TryParseFlexibleDecimal(OpeningBalanceText,out opening))
        {StatusText="أدخل مبلغاً صحيحاً؛ يمكن استخدام النقطة أو الفاصلة العشرية.";return;}
        if(opening<0){StatusText="الرصيد الافتتاحي لا يمكن أن يكون سالباً.";return;}
        if(opening>0 && !OpeningBalanceForUs && !OpeningBalanceAgainstUs)
        {StatusText="اختر اتجاه الرصيد.";return;}
        var book=SelectedGroupBook;
        if(book is null || book.Kind==BookKind.All){StatusText="اختر تصنيفاً صالحاً للحساب.";return;}

        try
        {
            IsSaving=true;
            StatusText="جارٍ حفظ الحساب…";
            var now=DateTimeOffset.UtcNow;
            var person=new Person(Guid.NewGuid(),book.Id,name,phone,NewNote.Trim(),now)
            {
                AccountCode=NewAccountCode.Trim(), SecondaryPhone=NewSecondaryPhone.Trim(), Address=NewAddress.Trim(), CreditLimit=creditLimit,
                Nature=NewAccountNature, WhatsAppEnabled=NewWhatsAppEnabled, UpdatedAt=now
            };
            var direction=OpeningBalanceForUs?OperationType.DebtOnUs:OperationType.DebtToUs;
            await repo.CreatePersonWithOpeningBalanceAsync(person,opening,direction);
            await RefreshAsync();
            if(continueEntry)
            {
                NewName=""; NewAccountCode="ACC-"+Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(); NewPhone=""; NewSecondaryPhone=""; NewAddress=""; NewCreditLimit=""; NewNote="";
                OpeningBalanceText=""; NewAccountNature=AccountNature.Flexible; NewWhatsAppEnabled=true;
                StatusText="تم حفظ الحساب. يمكنك إدخال الحساب التالي.";
            }
            else
            {
                IsDialogOpen=false;
                StatusText="تم حفظ الحساب.";
            }
        }
        catch(Exception ex){StatusText=ex.Message;}
        finally{IsSaving=false;}
    }

    private static bool TryParseFlexibleDecimal(string text,out decimal value)
    {
        var normalized=text.Trim().Replace("٬","").Replace("٫",".").Replace("،",",");
        if(normalized.Contains('.') && normalized.Contains(',')) normalized=normalized.Replace(",","");
        else if(normalized.Contains(',')) normalized=normalized.Replace(',', '.');
        return decimal.TryParse(normalized,System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,out value);
    }

    private static string NormalizePhoneForValidation(string phone)
    {
        try { return WhatsAppService.NormalizePhone(phone); }
        catch (ArgumentException) { return new string(phone.Where(char.IsDigit).ToArray()); }
    }

    [RelayCommand]
    private async Task SendStatementWhatsAppAsync()
    {
        if (SelectedPerson is null) { AccountStatementStatus = "اختر حساباً أولاً."; return; }
        try
        {
            var identity = await repo.GetIdentityAsync();
            var templates = await repo.GetMessageTemplatesAsync();
            var template = templates.FirstOrDefault(x => x.Name.Contains("كشف حساب العميل", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("StatementSummary", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("Account Statement", StringComparison.OrdinalIgnoreCase));
            const string fallback = "مرحباً {CustomerName}،\\nكشف حسابكم لدى {StoreName} بتاريخ {Date}.\\nإجمالي الحركات: {TotalAmount} {Currency}.\\nالرصيد الحالي: {RemainingAmount} {Currency}.\\nشكراً لتعاملكم معنا.";
            var values = new Dictionary<string, string?>
            {
                ["CustomerName"] = SelectedPerson.Person.Name,
                ["StoreName"] = identity.BusinessName,
                ["Date"] = DateTimeOffset.Now.ToString("yyyy/MM/dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                ["TotalAmount"] = (TotalDebit + TotalCredit).ToString("N2", System.Globalization.CultureInfo.InvariantCulture),
                ["RemainingAmount"] = Math.Abs(NetBalance).ToString("N2", System.Globalization.CultureInfo.InvariantCulture),
                ["Currency"] = identity.Currency
            };
            var message = MessageTemplateEngine.Render(template?.Body, values, fallback);
            new WhatsAppService().OpenWhatsApp(SelectedPerson.Person.Phone, message);
            AccountStatementStatus = "تم فتح واتساب برسالة كشف الحساب المخصصة.";
        }
        catch (Exception ex) { AccountStatementStatus = ex.Message; }
    }

    [RelayCommand]
    private async Task SendVoucherWhatsAppAsync(LedgerOperation? operation)
    {
        if (operation is null || SelectedPerson is null) { AccountStatementStatus = "اختر حركة من كشف الحساب."; return; }
        try
        {
            var identity = await repo.GetIdentityAsync();
            var templates = await repo.GetMessageTemplatesAsync();
            var template = templates.FirstOrDefault(x => x.Name.Contains("سند قبض", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("سند صرف", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("VoucherReceipt", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("Voucher Receipt", StringComparison.OrdinalIgnoreCase));
            var balance = SelectedAccountStatementRows.FirstOrDefault(x => x.Id == operation.Id)?.CumulativeBalance ?? NetBalance;
            const string fallback = "السلام عليكم ورحمة الله وبركاته، عزيزنا {CustomerName}، نود إحاطتكم بتفاصيل المعاملة رقم {InvoiceNumber} بتاريخ {Date}: المبلغ: {TotalAmount} {Currency} - البيان: {Description} - الرصيد الحالي: {RemainingAmount} {Currency}. شكراً لتعاملكم معنا.";
            var values = new Dictionary<string, string?>
            {
                ["CustomerName"] = SelectedPerson.Person.Name, ["StoreName"] = identity.BusinessName,
                ["InvoiceNumber"] = operation.ReferenceNumber ?? operation.Id.ToString("N")[..8].ToUpperInvariant(),
                ["TotalAmount"] = operation.Amount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture),
                ["RemainingAmount"] = Math.Abs(balance).ToString("N2", System.Globalization.CultureInfo.InvariantCulture),
                ["PaidAmount"] = operation.Amount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture),
                ["Currency"] = identity.Currency, ["Date"] = operation.OccurredAt.ToString("yyyy/MM/dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                ["Description"] = operation.Note, ["PaymentType"] = operation.TypeLabel
            };
            var message = MessageTemplateEngine.Render(template?.Body, values, fallback);
            new WhatsAppService().OpenMessage(SelectedPerson.Person.Phone, message);
            AccountStatementStatus = "تم فتح واتساب برسالة السند المخصصة.";
        }
        catch (Exception ex) { AccountStatementStatus = ex.Message; }
    }

    [RelayCommand]
    private async Task DeleteOperationAsync(LedgerOperation? operation)
    {
        if(operation is null || session.CurrentUser is null) return;
        var answer=System.Windows.MessageBox.Show(
            $"سيُنقل السند «{operation.ReferenceNumber ?? operation.Id.ToString("N")[..8]}» إلى سلة المحذوفات، وسيعاد احتساب الرصيد التراكمي. هل تريد المتابعة؟",
            "تأكيد حذف/عكس الحركة",System.Windows.MessageBoxButton.YesNo,System.Windows.MessageBoxImage.Warning);
        if(answer!=System.Windows.MessageBoxResult.Yes) return;
        try
        {
            await repo.DeleteOperationAsync(operation.Id,session.CurrentUser.Id);
            SelectedAccountOperations=await repo.GetOperationsAsync(operation.PersonId,1000);
            await OpenAccountAsync();
            await RefreshAsync();
            AccountStatementStatus="تم حذف الحركة وإعادة احتساب الأرصدة.";
        }
        catch(Exception ex){AccountStatementStatus=ex.Message;}
    }

    [RelayCommand]
    private async Task OpenAccountAsync()
    {
        if(SelectedPerson is null) { StatusText="اختر حساباً أولاً."; return; }
        try
        {
            AccountStatementStatus="جاري تحميل كشف الحساب…";
            SelectedAccountOperations=await repo.GetOperationsAsync(SelectedPerson.Person.Id,1000);
            OnPropertyChanged(nameof(OpeningBalance));
            OnPropertyChanged(nameof(TotalDebit));
            OnPropertyChanged(nameof(TotalCredit));
            OnPropertyChanged(nameof(NetBalance));
            OnPropertyChanged(nameof(BalanceStatusText));
            OnPropertyChanged(nameof(BalanceStatusColor));
            ApplyStatementFilters();
            IsAccountOpen=true;
            AccountStatementStatus=$"تم تحميل {SelectedAccountOperations.Count} حركة.";
        }
        catch(Exception ex) { AccountStatementStatus=$"تعذر فتح الحساب: {ex.Message}"; }
    }

    private void ApplyStatementFilters()
    {
        if (SelectedAccountOperations.Count == 0)
        {
            SelectedAccountStatementRows = Array.Empty<AccountStatementRow>();
            OnPropertyChanged(nameof(StatementOpeningBalance));
            OnPropertyChanged(nameof(StatementClosingBalance));
            return;
        }
        var from = StatementFromDate.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(StatementFromDate.Value.Date, DateTimeKind.Local)).ToUniversalTime()
            : SelectedAccountOperations.Min(x => x.OccurredAt);
        var to = StatementToDate.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(StatementToDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local)).ToUniversalTime()
            : SelectedAccountOperations.Max(x => x.OccurredAt);
        if (to < from)
        {
            SelectedAccountStatementRows = Array.Empty<AccountStatementRow>();
            AccountStatementStatus = "End date must not precede start date.";
            return;
        }
        var calculation = AccountStatementFilter.Apply(SelectedAccountOperations, from, to, StatementKindFilter, StatementSearch);
        SelectedAccountStatementRows = calculation.Movements.Select(operation => new AccountStatementRow(
            operation.Id, operation.Note, operation.TypeLabel, operation.Amount, operation.OccurredAt,
            operation.RunningBalance, operation)).Reverse().ToArray();
        OnPropertyChanged(nameof(StatementOpeningBalance));
        OnPropertyChanged(nameof(StatementClosingBalance));
    }

    public decimal StatementOpeningBalance
    {
        get
        {
            if (SelectedAccountOperations.Count == 0) return 0m;
            var from = StatementFromDate.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(StatementFromDate.Value.Date, DateTimeKind.Local)).ToUniversalTime()
                : SelectedAccountOperations.Min(x => x.OccurredAt);
            return SelectedAccountOperations.Where(x => x.OccurredAt < from).Sum(LedgerBalanceCalculator.SignedEffect);
        }
    }

    public decimal StatementClosingBalance
    {
        get
        {
            if (SelectedAccountOperations.Count == 0) return 0m;
            var from = StatementFromDate.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(StatementFromDate.Value.Date, DateTimeKind.Local)).ToUniversalTime()
                : SelectedAccountOperations.Min(x => x.OccurredAt);
            var to = StatementToDate.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(StatementToDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Local)).ToUniversalTime()
                : SelectedAccountOperations.Max(x => x.OccurredAt);
            return FinancialReportCalculations.CalculateAccountStatement(SelectedAccountOperations, from, to).ClosingBalance;
        }
    }
    [RelayCommand]
    private void CloseAccount()
    {
        if(IsAccountOpen) IsAccountOpen=false;
    }

    [RelayCommand]
    private async Task ExportAccountPdfAsync()
    {
        if(SelectedPerson is null) return;
        var dialog=new SaveFileDialog { Filter="PDF (*.pdf)|*.pdf", FileName=$"كشف_{SelectedPerson.Person.Name}.pdf", AddExtension=true, OverwritePrompt=true };
        if(dialog.ShowDialog(System.Windows.Application.Current?.MainWindow)!=true) return;
        try
        {
            var service=new PdfDocumentService(repo);
            await service.GenerateAccountStatementAsync(dialog.FileName,SelectedPerson.Person.Name,SelectedPerson.BookName,SelectedAccountOperations,DocumentPageKind.A4);
            AccountStatementStatus="تم تصدير كشف الحساب إلى PDF.";
        }
        catch(Exception ex){AccountStatementStatus=$"فشل تصدير PDF: {ex.Message}";}
    }

    [RelayCommand]
    private void ExportAccountExcel()
    {
        if(SelectedPerson is null) return;
        var dialog=new SaveFileDialog { Filter="Excel (*.xlsx)|*.xlsx", FileName=$"كشف_{SelectedPerson.Person.Name}.xlsx", AddExtension=true, OverwritePrompt=true };
        if(dialog.ShowDialog(System.Windows.Application.Current?.MainWindow)!=true) return;
        try
        {
            var identity=repo.GetIdentityAsync().GetAwaiter().GetResult();
            new ExcelExportService().ExportAccountStatement(dialog.FileName,identity.BusinessName,SelectedPerson.Person.Name,SelectedPerson.BookName,SelectedAccountOperations,identity.Currency);
            AccountStatementStatus="تم تصدير كشف الحساب إلى Excel.";
        }
        catch(Exception ex){AccountStatementStatus=$"فشل تصدير Excel: {ex.Message}";}
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if(SelectedPerson is null||session.CurrentUser is null)return;
        var historicalOperations = await repo.GetOperationsAsync(SelectedPerson.Person.Id, 10000);
        var currentBalance = LedgerBalanceCalculator.CalculateNetBalance(historicalOperations);
        if (!FinancialReportCalculations.CanDeleteAccount(historicalOperations.Count, currentBalance, out var reason))
        {
            System.Windows.MessageBox.Show(reason, "Account deletion blocked", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            StatusText = reason;
            return;
        }
        var answer=System.Windows.MessageBox.Show($"سيُنقل الحساب «{SelectedPerson.Person.Name}» إلى سلة المحذوفات. هل تريد المتابعة؟","تأكيد حذف الحساب",System.Windows.MessageBoxButton.YesNo,System.Windows.MessageBoxImage.Warning);
        if(answer!=System.Windows.MessageBoxResult.Yes) return;
        try
        {
            await repo.DeletePersonAsync(SelectedPerson.Person.Id,session.CurrentUser.Id);
            StatusText="تم نقل الحساب إلى سلة المحذوفات.";
            SelectedPerson=null;
            await RefreshAsync();
        }
        catch(Exception ex){StatusText=ex.Message;}
    }

    private static string Normalize(string s)=>s.Replace("أ","ا").Replace("إ","ا").Replace("آ","ا").Replace("ة","ه").Trim();
}
