using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;

namespace SmartLedger.App.ViewModels;

public partial class OperationsViewModel(IAppRepository repo,IAppSession session):ObservableObject
{
    [ObservableProperty] private IReadOnlyList<PersonSummary> people=Array.Empty<PersonSummary>();
    [ObservableProperty] private string personSearch="";
    [ObservableProperty] private bool isPersonSuggestionsOpen;
    [ObservableProperty] private bool isMainPersonSuggestionsOpen;
    [ObservableProperty] private bool isDialogPersonSuggestionsOpen;
    [ObservableProperty] private IReadOnlyList<LedgerOperation> operations=Array.Empty<LedgerOperation>();
    [ObservableProperty] private IReadOnlyList<OperationRow> rows=Array.Empty<OperationRow>();
    [ObservableProperty] private Guid? selectedPersonId;
    [ObservableProperty] private OperationType selectedType=OperationType.Receive;
    [ObservableProperty] private bool isReceiveSelected=true;
    [ObservableProperty] private bool isPaymentSelected;
    [ObservableProperty] private bool isSaleSelected;
    [ObservableProperty] private bool isPurchaseSelected;
    [ObservableProperty] private bool isDebtSelected;
    [ObservableProperty] private string amountText="";
    [ObservableProperty] private string note="";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool isDialogOpen;
    [ObservableProperty] private string statusText="";
    private CancellationTokenSource? _personSearchCts;
    private IReadOnlyDictionary<Guid,string> _personNames=new Dictionary<Guid,string>();

    public IReadOnlyList<OperationTypeOption> Types { get; } = new[]
    {
        new OperationTypeOption(OperationType.Receive,"استلام"),
        new OperationTypeOption(OperationType.Sale,"بيع"),
        new OperationTypeOption(OperationType.Payment,"سداد"),
        new OperationTypeOption(OperationType.Purchase,"شراء")
    };

    public string SelectedTypeLabel=>Types.First(x=>x.Value==SelectedType).Label;
    partial void OnSelectedTypeChanged(OperationType value)
    {
        IsReceiveSelected=value==OperationType.Receive;
        IsPaymentSelected=value==OperationType.Payment;
        IsSaleSelected=value==OperationType.Sale;
        IsPurchaseSelected=value==OperationType.Purchase;
        IsDebtSelected=value==OperationType.DebtOnUs || value==OperationType.DebtToUs;
        OnPropertyChanged(nameof(SelectedTypeLabel));
    }
    partial void OnSelectedPersonIdChanged(Guid? value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        var person=People.FirstOrDefault(x=>x.PersonId==value);
        if(person is not null)
        {
            PersonSearch=person.PersonName;
            IsPersonSuggestionsOpen=false;
            IsMainPersonSuggestionsOpen=false;
            IsDialogPersonSuggestionsOpen=false;
            ApplySmartType(person);
        }
    }
    partial void OnIsDialogOpenChanged(bool value)
    {
        if(value) IsMainPersonSuggestionsOpen=false;
        else IsDialogPersonSuggestionsOpen=false;
    }
    partial void OnPersonSearchChanged(string value)
    {
        IsPersonSuggestionsOpen=!string.IsNullOrWhiteSpace(value);
        if(IsDialogOpen) IsDialogPersonSuggestionsOpen=IsPersonSuggestionsOpen;
        else IsMainPersonSuggestionsOpen=IsPersonSuggestionsOpen;
        _personSearchCts?.Cancel();
        _personSearchCts?.Dispose();
        _personSearchCts=new CancellationTokenSource();
        _=RefreshPeopleSuggestionsAsync(value,_personSearchCts.Token);
    }

    private async Task RefreshPeopleSuggestionsAsync(string value,CancellationToken ct)
    {
        try
        {
            await Task.Delay(180,ct);
            People=await repo.GetPeopleAsync(search:value.Trim(),take:25,ct:ct);
            if(SelectedPersonId is not null && People.All(x=>x.PersonId!=SelectedPersonId))
                SelectedPersonId=null;
        }
        catch(OperationCanceledException) { }
    }
    [RelayCommand] private void SelectReceive(){SelectedType=OperationType.Receive;}
    [RelayCommand] private void SelectPayment(){SelectedType=OperationType.Payment;}
    [RelayCommand] private void SelectSale(){SelectedType=OperationType.Sale;}
    [RelayCommand] private void SelectPurchase(){SelectedType=OperationType.Purchase;}
    [RelayCommand] private void SelectDebt(){ var person=People.FirstOrDefault(x=>x.PersonId==SelectedPersonId); SelectedType=person is not null && person.NetBalance<0 ? OperationType.DebtToUs : OperationType.DebtOnUs; }
    [RelayCommand] private void OpenNew(){SelectedPersonId=null;PersonSearch="";SelectedType=OperationType.Receive;IsPersonSuggestionsOpen=false;IsMainPersonSuggestionsOpen=false;IsDialogPersonSuggestionsOpen=false;AmountText="";Note="";StatusText="";IsDialogOpen=true;}
    [RelayCommand] private void CloseDialog(){if(!IsBusy)IsDialogOpen=false;}

    public async Task LoadAsync()
    {
        _personNames=await repo.GetPersonNamesAsync();
        People=await repo.GetPeopleAsync(take:25);
        Operations=await repo.GetOperationsAsync(take:100);
        RefreshRows();
    }

    partial void OnAmountTextChanged(string value)=>SaveCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute=nameof(CanSave))]
    private async Task SaveAsync()
    {
        if(!NumericNormalizer.TryParseDecimal(AmountText,out var amount)||amount<=0||SelectedPersonId is null)return;
        IsBusy=true; StatusText="جارٍ حفظ الحركة…";
        try
        {
            await repo.CreateOperationAsync(new LedgerOperation(Guid.NewGuid(),SelectedPersonId.Value,SelectedType,amount,Note.Trim(),DateTimeOffset.Now,session.CurrentUser!.Id));
            AmountText=""; Note="";
            Operations=await repo.GetOperationsAsync(take:100);
            RefreshRows();
            StatusText="تم حفظ الحركة بنجاح.";
            IsDialogOpen=false;
        }
        catch(Exception ex){StatusText=ex.Message;}
        finally{IsBusy=false;SaveCommand.NotifyCanExecuteChanged();}
    }

    private bool CanSave()=>!IsBusy&&SelectedPersonId is not null&&NumericNormalizer.TryParseDecimal(AmountText,out var a)&&a>0;

    private void RefreshRows()
    {
        Rows=Operations.Select(x=>new OperationRow(x.Id,_personNames.TryGetValue(x.PersonId,out var name)?name:"حساب غير معروف",DirectionLabel(x.Type),DirectionPositive(x.Type),x.TypeLabel,x.Amount,x.OccurredAt,x.Note)).ToArray();
    }

    private void ApplySmartType(PersonSummary person)
    {
        var preferred=person.BookName.Contains("عميل",StringComparison.OrdinalIgnoreCase)
            ? OperationType.Receive
            : person.BookName.Contains("مورد",StringComparison.OrdinalIgnoreCase) || person.BookName.Contains("عامل",StringComparison.OrdinalIgnoreCase)
                ? OperationType.Payment
                : person.NetBalance switch
                {
                    > 0 => OperationType.Receive,
                    < 0 => OperationType.Payment,
                    _ => OperationType.Receive
                };
        SelectedType=preferred;
    }

    private static string DirectionLabel(OperationType type)=>type switch
    {
        OperationType.DebtToUs or OperationType.Purchase or OperationType.Payment=>"له",
        OperationType.DebtOnUs or OperationType.Sale or OperationType.Receive=>"عليه",
        OperationType.Return or OperationType.Settlement=>"تسوية",
        _=>"أخرى"
    };

private static bool? DirectionPositive(OperationType type)=>type switch
{
    OperationType.DebtOnUs or OperationType.Sale or OperationType.Receive=>true,
    OperationType.DebtToUs or OperationType.Purchase or OperationType.Payment=>false,
    _=>null
};
}

public sealed record OperationRow(Guid Id,string PersonName,string DirectionLabel,bool? DirectionPositive,string TypeLabel,decimal Amount,DateTimeOffset OccurredAt,string Note);