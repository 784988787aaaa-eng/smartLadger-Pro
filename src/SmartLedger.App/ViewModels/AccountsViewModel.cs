using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SmartLedger.Infrastructure.FinalRelease;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;

namespace SmartLedger.App.ViewModels;

public sealed record AccountStatementRow(Guid Id,string Note,string TypeLabel,decimal Amount,DateTimeOffset OccurredAt,decimal CumulativeBalance);

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
    [ObservableProperty] private string newPhone="";
    [ObservableProperty] private string newNote="";
    [ObservableProperty] private string openingBalanceText="";
    [ObservableProperty] private bool openingBalanceForUs=true;
    [ObservableProperty] private bool openingBalanceAgainstUs;
    [ObservableProperty] private string statusText="";
    [ObservableProperty] private bool isAccountOpen;
    [ObservableProperty] private IReadOnlyList<LedgerOperation> selectedAccountOperations=Array.Empty<LedgerOperation>();
    [ObservableProperty] private IReadOnlyList<AccountStatementRow> selectedAccountStatementRows=Array.Empty<AccountStatementRow>();
    [ObservableProperty] private string accountStatementStatus="";
    [ObservableProperty] private string currency="ريال";
    private bool syncingGroupAndType;

    public string SelectedAccountTitle => SelectedPerson is null ? "الحساب" : $"حساب {SelectedPerson.Person.Name}";
    public string SelectedAccountTypeLabel => SelectedPerson is null ? "" : SelectedPerson.BookName;
    public string SelectedAccountNote => SelectedPerson?.Person.Note ?? "";
    public decimal SelectedAccountBalance => SelectedPerson?.NetBalance ?? 0m;
    public decimal SelectedAccountOwedToUs => SelectedPerson?.OwedToUs ?? 0m;
    public decimal SelectedAccountWeOwe => SelectedPerson?.WeOwe ?? 0m;
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
        NewPhone="";
        NewNote="";
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
    private async Task SaveNewAsync()
    {
        if(IsSaving) return;

        var name=NewName.Trim();
        if(name.Length<2){StatusText="أدخل اسم الحساب.";return;}

        var rawOpening=OpeningBalanceText.Trim();
        decimal opening=0;
        if(rawOpening.Length>0 && !NumericNormalizer.TryParseDecimal(rawOpening,out opening))
        {
            StatusText="أدخل مبلغًا صحيحًا.";
            return;
        }

        if(opening<0){StatusText="الرصيد الافتتاحي لا يمكن أن يكون سالبًا.";return;}
        if(opening>0 && !OpeningBalanceForUs && !OpeningBalanceAgainstUs)
        {
            StatusText="اختر اتجاه الرصيد.";
            return;
        }

        var book=SelectedGroupBook;
        if(book is null){StatusText="اختر تصنيف الدفتر أولاً.";return;}
        if(book.Kind==BookKind.All){StatusText="اختر تصنيفًا صالحًا للحساب.";return;}

        try
        {
            IsSaving=true;
            StatusText="جارٍ حفظ الحساب…";
            var direction=OpeningBalanceForUs?OperationType.DebtOnUs:OperationType.DebtToUs;
            await repo.CreatePersonWithOpeningBalanceAsync(book.Id,name,NewPhone.Trim(),NewNote.Trim(),opening,direction);
            IsDialogOpen=false;
            await RefreshAsync();
        }
        catch(Exception ex)
        {
            StatusText=ex.Message;
        }
        finally
        {
            IsSaving=false;
        }
    }

    [RelayCommand]
    private async Task OpenAccountAsync()
    {
        if(SelectedPerson is null) { StatusText="اختر حساباً أولاً."; return; }
        try
        {
            AccountStatementStatus="جاري تحميل كشف الحساب…";
            SelectedAccountOperations=await repo.GetOperationsAsync(SelectedPerson.Person.Id,1000);
            var running=0m;
            var chronological=SelectedAccountOperations.OrderBy(x=>x.OccurredAt).ThenBy(x=>x.Id).ToArray();
            var rows=new List<AccountStatementRow>(chronological.Length);
            foreach(var operation in chronological)
            {
                running+=operation.Type switch
                {
                    OperationType.DebtOnUs or OperationType.Sale or OperationType.Payment or OperationType.Settlement=>operation.Amount,
                    OperationType.Receive or OperationType.Return or OperationType.DebtToUs or OperationType.Purchase=>-operation.Amount,
                    _=>0m
                };
                rows.Add(new AccountStatementRow(
                    operation.Id,
                    operation.Note,
                    operation.TypeLabel,
                    operation.Amount,
                    operation.OccurredAt,
                    decimal.Round(running,2,MidpointRounding.AwayFromZero)));
            }
            SelectedAccountStatementRows=rows.AsEnumerable().Reverse().ToArray();
            IsAccountOpen=true;
            AccountStatementStatus=$"تم تحميل {SelectedAccountOperations.Count} حركة.";
        }
        catch(Exception ex) { AccountStatementStatus=$"تعذر فتح الحساب: {ex.Message}"; }
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
