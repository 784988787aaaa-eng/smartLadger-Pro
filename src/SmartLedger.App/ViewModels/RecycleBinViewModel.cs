using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;

namespace SmartLedger.App.ViewModels;

public partial class RecycleBinViewModel(IAppRepository repo):ObservableObject
{
    [ObservableProperty] private IReadOnlyList<DeletedItem> items=Array.Empty<DeletedItem>();
    [ObservableProperty] private DeletedItem? selected;
    [ObservableProperty] private string statusText="";
    [ObservableProperty] private bool isBusy;
    public async Task LoadAsync()=>Items=await repo.GetDeletedItemsAsync();

    [RelayCommand]
    private async Task Restore()
    {
        if(Selected is null||IsBusy)return;
        IsBusy=true;StatusText="";
        try{await repo.RestoreDeletedItemAsync(Selected.Id);StatusText="تمت الاستعادة بنجاح.";await LoadAsync();}
        catch(Exception ex){StatusText=ex.Message;}
        finally{IsBusy=false;}
    }

    [RelayCommand]
    private async Task HardDelete()
    {
        if(Selected is null||IsBusy)return;
        IsBusy=true;StatusText="";
        try{await repo.HardDeleteDeletedItemAsync(Selected.Id);StatusText="تم الحذف النهائي من السلة.";Selected=null;await LoadAsync();}
        catch(Exception ex){StatusText=ex.Message;}
        finally{IsBusy=false;}
    }
}
