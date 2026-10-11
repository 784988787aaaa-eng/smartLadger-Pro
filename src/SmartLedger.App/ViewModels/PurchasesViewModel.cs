using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;

namespace SmartLedger.App.ViewModels;

public partial class PurchasesViewModel(IAppRepository repo,IAppSession session):ObservableObject
{
    [ObservableProperty] private IReadOnlyList<PersonSummary> people=Array.Empty<PersonSummary>();
    [ObservableProperty] private IReadOnlyList<Product> products=Array.Empty<Product>();
    [ObservableProperty] private IReadOnlyList<InvoiceSummary> invoiceHistory=Array.Empty<InvoiceSummary>();
    [ObservableProperty] private string invoiceSearch="";
    private IReadOnlyList<PersonSummary> _allPeople=Array.Empty<PersonSummary>();
    private IReadOnlyList<Product> _allProducts=Array.Empty<Product>();
    private IReadOnlyList<Book> _books=Array.Empty<Book>();
    private bool _settingProductSelection;
    private decimal _lastUnitFactor=1m;
    [ObservableProperty] private string supplierSearch="";
    [ObservableProperty] private string productSearch="";
    [ObservableProperty] private bool isSupplierSuggestionsOpen;
    [ObservableProperty] private bool isProductSuggestionsOpen;
    [ObservableProperty] private Guid? personId;
    [ObservableProperty] private Guid? productId;
    [ObservableProperty] private string selectedQuantityUnit="";
    [ObservableProperty] private string quantityText="1";
    [ObservableProperty] private string priceText="";
    [ObservableProperty] private string paidText="0";
    [ObservableProperty] private string note="";
    [ObservableProperty] private string batchNumber="";
    [ObservableProperty] private DateTime? expiryDate;
    [ObservableProperty] private PaymentMode paymentMode=PaymentMode.Cash;
    [ObservableProperty] private bool isCashSelected=true;
    [ObservableProperty] private bool isCreditSelected;
    [ObservableProperty] private bool isPartialSelected;
    [ObservableProperty] private decimal total;
    [ObservableProperty] private bool isDialogOpen;
    [ObservableProperty] private bool isQuickProductDialogOpen;
    [ObservableProperty] private bool isQuickPersonDialogOpen;
    [ObservableProperty] private string quickPersonName="";
    [ObservableProperty] private string quickPersonPhone="";
    [ObservableProperty] private string quickPersonNote="";
    [ObservableProperty] private bool isSaving;
    [ObservableProperty] private string quickProductName="";
    [ObservableProperty] private string quickProductSmallUnit="";
    [ObservableProperty] private string quickProductLargeUnit="";
    [ObservableProperty] private string quickProductConversion="1";
    [ObservableProperty] private string quickProductPurchasePrice="";
    [ObservableProperty] private string quickProductSalePrice="";
    [ObservableProperty] private string statusText="";
    public ObservableCollection<InvoiceDraftLine> Lines { get; } = new();
    public decimal LinesTotal => Lines.Sum(x=>x.LineTotal);
    public bool IsCartEmpty => Lines.Count == 0;

    public IReadOnlyList<string> QuantityUnits { get { var p=_allProducts.FirstOrDefault(x=>x.Id==ProductId); return p is null?Array.Empty<string>():new[]{p.SmallUnit,p.LargeUnit}.Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); } }
    private decimal QuantityFactor(Product p)=>!string.IsNullOrWhiteSpace(p.LargeUnit)&&!string.Equals(p.LargeUnit,p.SmallUnit,StringComparison.OrdinalIgnoreCase)&&string.Equals(SelectedQuantityUnit,p.LargeUnit,StringComparison.OrdinalIgnoreCase)?p.ConversionFactor:1m;
    public decimal Remaining=>Math.Max(0,Total-(NumericNormalizer.TryParseDecimal(PaidText,out var paid)?paid:0));
    public string BalanceLabel=>Remaining>0?"متبقي علينا":"مسدد بالكامل";
    public IReadOnlyList<PaymentModeOption> PaymentModes {get;}=new[]{new PaymentModeOption(PaymentMode.Cash,"نقدي"),new PaymentModeOption(PaymentMode.Credit,"آجل — عليه"),new PaymentModeOption(PaymentMode.Partial,"جزئي — المتبقي عليه")};

    public async Task LoadAsync(){_books=await repo.GetBooksAsync();_allPeople=await repo.GetSupplierPeopleAsync();_allProducts=await repo.GetProductsAsync();People=_allPeople;Products=_allProducts;await LoadInvoiceHistoryAsync();}
    private int _invoiceSearchRequest;
    partial void OnInvoiceSearchChanged(string value)=>_ = LoadInvoiceHistoryAsync();
    private async Task LoadInvoiceHistoryAsync()
    {
        var request = System.Threading.Interlocked.Increment(ref _invoiceSearchRequest);
        var search = InvoiceSearch;
        var result = await repo.GetInvoiceSummariesAsync(InvoiceType.Purchase, search, 100);
        if (request == System.Threading.Volatile.Read(ref _invoiceSearchRequest)) InvoiceHistory = result;
    }
    [RelayCommand] private async Task ReverseInvoiceAsync(string invoiceNumber)
    {
        if(IsSaving||string.IsNullOrWhiteSpace(invoiceNumber))return;
        var confirmation=System.Windows.MessageBox.Show($"سيتم عكس فاتورة الشراء رقم {invoiceNumber} مع تحديث المخزون وحساب المورد وحركة النقد. قد يُرفض العكس إذا استُهلكت دفعات الفاتورة. هل تريد المتابعة؟","تأكيد عكس فاتورة الشراء",System.Windows.MessageBoxButton.YesNo,System.Windows.MessageBoxImage.Warning);
        if(confirmation!=System.Windows.MessageBoxResult.Yes)return;
        IsSaving=true;
        try
        {
            var reversed=await repo.ReverseInvoiceAsync(invoiceNumber,"طلب المستخدم عكس فاتورة الشراء",session.CurrentUser!.Id.ToString());
            if(!reversed){System.Windows.MessageBox.Show("لم تُعثر على الفاتورة أو سبق عكسها.","نتيجة العكس",System.Windows.MessageBoxButton.OK,System.Windows.MessageBoxImage.Information);return;}
            _allProducts=await repo.GetProductsAsync();Products=_allProducts;
            await LoadInvoiceHistoryAsync();
            System.Windows.MessageBox.Show("تم عكس الفاتورة وتسجيل الحركات العكسية بنجاح.","تم العكس",System.Windows.MessageBoxButton.OK,System.Windows.MessageBoxImage.Information);
        }
        catch(Exception ex){System.Windows.MessageBox.Show(ex.Message,"تعذر عكس الفاتورة",System.Windows.MessageBoxButton.OK,System.Windows.MessageBoxImage.Warning);}
        finally{IsSaving=false;}
    }
    partial void OnSupplierSearchChanged(string value){if(PersonId is Guid id&&_allPeople.FirstOrDefault(x=>x.PersonId==id) is { } selected&&!Normalize(value).Equals(Normalize(selected.PersonName),StringComparison.OrdinalIgnoreCase))PersonId=null;People=FilterPeople(value);IsSupplierSuggestionsOpen=!string.IsNullOrWhiteSpace(value)&&People.Count>0;}
    partial void OnProductSearchChanged(string value){if(_settingProductSelection)return;if(ProductId is Guid id&&_allProducts.FirstOrDefault(x=>x.Id==id) is { } selected&&!Normalize(value).Equals(Normalize(selected.Name),StringComparison.OrdinalIgnoreCase))ProductId=null;Products=FilterProducts(value);IsProductSuggestionsOpen=!string.IsNullOrWhiteSpace(value)&&Products.Count>0;}
    partial void OnPersonIdChanged(Guid? value){var p=_allPeople.FirstOrDefault(x=>x.PersonId==value);if(p is not null){SupplierSearch=p.PersonName;IsSupplierSuggestionsOpen=false;}}
    partial void OnProductIdChanged(Guid? value){var p=_allProducts.FirstOrDefault(x=>x.Id==value);_settingProductSelection=true;_lastUnitFactor=1m;if(p is not null){ProductSearch=p.Name;SelectedQuantityUnit=p.SmallUnit;PriceText=p.PurchasePrice>0?p.PurchasePrice.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture):"";IsProductSuggestionsOpen=false;}else{SelectedQuantityUnit="";PriceText="";}_settingProductSelection=false;OnPropertyChanged(nameof(QuantityUnits));Recalc();}
    partial void OnSelectedQuantityUnitChanged(string value){OnPropertyChanged(nameof(QuantityUnits));if(_settingProductSelection)return;var product=_allProducts.FirstOrDefault(x=>x.Id==ProductId);if(product is null)return;var nextFactor=QuantityFactor(product);if(NumericNormalizer.TryParseDecimal(PriceText,out var currentPrice)&&_lastUnitFactor>0)PriceText=(currentPrice*nextFactor/_lastUnitFactor).ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);_lastUnitFactor=nextFactor;}
    [RelayCommand] private void OpenNew(){Lines.Clear();OnPropertyChanged(nameof(LinesTotal));OnPropertyChanged(nameof(IsCartEmpty));PersonId=null;ProductId=null;SupplierSearch="";ProductSearch="";People=_allPeople;Products=_allProducts;IsSupplierSuggestionsOpen=false;IsProductSuggestionsOpen=false;QuantityText="1";PriceText="";PaidText="0";Note="";PaymentMode=PaymentMode.Cash;SyncPaymentChoices();StatusText="";Recalc();IsDialogOpen=true;}
    [RelayCommand] private void OpenQuickPerson(){QuickPersonName=SupplierSearch.Trim();QuickPersonPhone="";QuickPersonNote="";IsQuickPersonDialogOpen=true;}
    [RelayCommand] private void CloseQuickPerson(){IsQuickPersonDialogOpen=false;}
    [RelayCommand] private async Task SaveQuickPersonAsync(){
        if(IsSaving)return;
        if(string.IsNullOrWhiteSpace(QuickPersonName)){StatusText="أدخل اسم الحساب.";return;}
        var book=_books.FirstOrDefault(x=>x.Kind==BookKind.Suppliers);
        if(book is null){StatusText="لم يُعثر على دفتر الموردين؛ تعذر إنشاء الحساب.";return;}
        try{
            var candidates=await repo.GetPeopleAsync(book.Id,null,null);
            var duplicate=candidates.FirstOrDefault(x=>Normalize(x.PersonName).Equals(Normalize(QuickPersonName),StringComparison.OrdinalIgnoreCase));
            if(duplicate is not null){
                _allPeople=await repo.GetSupplierPeopleAsync();People=FilterPeople(QuickPersonName);PersonId=duplicate.PersonId;IsQuickPersonDialogOpen=false;StatusText="الحساب موجود بالفعل؛ تم اختياره بدل إنشاء نسخة مكررة.";return;
            }
            IsSaving=true;
            var saved=await repo.CreatePersonAsync(book.Id,QuickPersonName.Trim(),QuickPersonPhone.Trim(),QuickPersonNote.Trim());
            _allPeople=await repo.GetSupplierPeopleAsync();People=FilterPeople(saved.Name);SupplierSearch=saved.Name;PersonId=saved.Id;IsQuickPersonDialogOpen=false;StatusText="تم إنشاء الحساب وربطه بالفاتورة دون فقد بياناتها.";
        }catch(Exception ex){StatusText=ex.Message;}
        finally{IsSaving=false;}
    }
    [RelayCommand] private void OpenQuickProduct(){QuickProductName=ProductSearch.Trim();QuickProductSmallUnit="";QuickProductLargeUnit="";QuickProductConversion="1";QuickProductPurchasePrice="";QuickProductSalePrice="";IsQuickProductDialogOpen=true;}
    [RelayCommand] private void CloseQuickProduct(){IsQuickProductDialogOpen=false;}
    [RelayCommand] private async Task SaveQuickProductAsync(){
        if(IsSaving)return;
        if(string.IsNullOrWhiteSpace(QuickProductName)){StatusText="أدخل اسم الصنف الجديد.";return;}
        if(string.IsNullOrWhiteSpace(QuickProductSmallUnit)||string.IsNullOrWhiteSpace(QuickProductLargeUnit)){StatusText="أدخل اسمي الوحدتين.";return;}
        if(!NumericNormalizer.TryParseDecimal(QuickProductConversion,out var factor)||factor<=0){StatusText="أدخل معامل تحويل صحيحًا.";return;}
        var purchase=0m;if(!string.IsNullOrWhiteSpace(QuickProductPurchasePrice)&&(!NumericNormalizer.TryParseDecimal(QuickProductPurchasePrice,out purchase)||purchase<0)){StatusText="أدخل سعر شراء مرجعيًا صحيحًا أو اتركه فارغًا.";return;}
        var sale=0m;if(!string.IsNullOrWhiteSpace(QuickProductSalePrice)&&(!NumericNormalizer.TryParseDecimal(QuickProductSalePrice,out sale)||sale<0)){StatusText="أدخل سعر بيع صحيحًا أو اتركه فارغًا.";return;}
        IsSaving=true;
        try{
            var saved=await repo.CreateProductAsync(new Product(Guid.NewGuid(),QuickProductName.Trim(),"","عام",QuickProductLargeUnit.Trim(),QuickProductSmallUnit.Trim(),factor,purchase,sale,0m,10m,DateTimeOffset.Now));
            var actualPrice=PriceText;
            _allProducts=await repo.GetProductsAsync();Products=FilterProducts(ProductSearch);
            ProductId=saved.Id;ProductSearch=saved.Name;
            if(NumericNormalizer.TryParseDecimal(actualPrice,out var entered)&&entered>0)PriceText=actualPrice;
            else PriceText=saved.PurchasePrice>0?saved.PurchasePrice.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture):"";
            IsQuickProductDialogOpen=false;StatusText="تم إنشاء الصنف وربطه بالفاتورة. أكمل بيانات العملية.";
        }catch(Exception ex){StatusText=ex.Message;}
        finally{IsSaving=false;}
    }
    [RelayCommand] private void CloseDialog(){if(!IsSaving)IsDialogOpen=false;}
    [RelayCommand] private void SelectCash(){PaymentMode=PaymentMode.Cash;}
    [RelayCommand] private void SelectCredit(){PaymentMode=PaymentMode.Credit;}
    [RelayCommand] private void SelectPartial(){PaymentMode=PaymentMode.Partial;}
    partial void OnPaymentModeChanged(PaymentMode value){SyncPaymentChoices();if(value==PaymentMode.Cash)PaidText=Total.ToString("0.##");else if(value==PaymentMode.Credit)PaidText="0";Recalc();OnPropertyChanged(nameof(Remaining));OnPropertyChanged(nameof(BalanceLabel));}
    private void SyncPaymentChoices(){IsCashSelected=PaymentMode==PaymentMode.Cash;IsCreditSelected=PaymentMode==PaymentMode.Credit;IsPartialSelected=PaymentMode==PaymentMode.Partial;}
    partial void OnQuantityTextChanged(string value)=>Recalc();
    partial void OnPriceTextChanged(string value)=>Recalc();
    partial void OnPaidTextChanged(string value){OnPropertyChanged(nameof(Remaining));OnPropertyChanged(nameof(BalanceLabel));}
    partial void OnTotalChanged(decimal value){if(PaymentMode==PaymentMode.Cash)PaidText=value.ToString("0.##");OnPropertyChanged(nameof(Remaining));OnPropertyChanged(nameof(BalanceLabel));}
    private void Recalc(){var current=ProductId is not null&&NumericNormalizer.TryParseDecimal(QuantityText,out var q)&&q>0&&NumericNormalizer.TryParseDecimal(PriceText,out var p)&&p>0?decimal.Round(q*p,2):0m;Total=decimal.Round(LinesTotal+current,2);}
    [RelayCommand] private void AddLine(){if(AddCurrentLine())StatusText="تمت إضافة الصنف إلى بنود الفاتورة.";}
    [RelayCommand] private void RemoveLine(InvoiceDraftLine? line){if(line is not null&&Lines.Remove(line)){OnPropertyChanged(nameof(LinesTotal));OnPropertyChanged(nameof(IsCartEmpty));Recalc();}}
    private bool AddCurrentLine(){
        if(ProductId is null){StatusText="اختر صنفًا مسجلًا أو أنشئ صنفًا جديدًا أولًا.";return false;}
        var product=_allProducts.FirstOrDefault(x=>x.Id==ProductId);if(product is null){StatusText="الصنف المحدد غير متاح؛ أعد اختياره.";return false;}
        if(!NumericNormalizer.TryParseDecimal(QuantityText,out var q)||q<=0){StatusText="أدخل كمية صحيحة.";return false;}
        if(!NumericNormalizer.TryParseDecimal(PriceText,out var price)||price<=0){StatusText="أدخل سعرًا صحيحًا أكبر من صفر.";return false;}
        var smallQuantity=q*QuantityFactor(product);if(smallQuantity<=0){StatusText="تحقق من الوحدة ومعامل التحويل.";return false;}
        Lines.Add(new InvoiceDraftLine(product.Id,product.Name,q,SelectedQuantityUnit,price,decimal.Round(q*price,2),smallQuantity){BatchNumber=BatchNumber.Trim(),ExpiryDate=ExpiryDate is DateTime expiry?new DateTimeOffset(expiry.Date):null,CostPriceSnapshot=decimal.Round(price/QuantityFactor(product),4),ConversionFactorApplied=QuantityFactor(product)});OnPropertyChanged(nameof(LinesTotal));OnPropertyChanged(nameof(IsCartEmpty));ProductId=null;ProductSearch="";QuantityText="1";PriceText="";BatchNumber="";ExpiryDate=null;IsProductSuggestionsOpen=false;Recalc();return true;
    }
    private IReadOnlyList<PersonSummary> FilterPeople(string value)=>string.IsNullOrWhiteSpace(value)?_allPeople:_allPeople.Where(x=>ContainsArabic(x.PersonName,value)||ContainsArabic(x.Person.Phone,value)).Take(25).ToArray();
    private IReadOnlyList<Product> FilterProducts(string value)=>string.IsNullOrWhiteSpace(value)?_allProducts:_allProducts.Where(x=>ContainsArabic(x.Name,value)||ContainsArabic(x.Barcode,value)).Take(25).ToArray();
    private static bool ContainsArabic(string source,string query)=>Normalize(source).Contains(Normalize(query),StringComparison.OrdinalIgnoreCase);
    private static string Normalize(string value)=>string.Join(" ",value.Trim().ToLowerInvariant().Replace("أ","ا").Replace("إ","ا").Replace("آ","ا").Replace("ة","ه").Replace("ى","ي").Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));
    [RelayCommand] private async Task SaveAsync()
    {
        if(IsSaving)return;
        if(ProductId is not null){if(!AddCurrentLine())return;}
        else if(!string.IsNullOrWhiteSpace(ProductSearch)){StatusText="اختر الصنف من الاقتراحات أو أنشئه أولًا.";return;}
        if(Lines.Count==0){StatusText="أضف صنفًا واحدًا على الأقل إلى الفاتورة.";return;}
        var total=decimal.Round(LinesTotal,2);
        var paid=PaymentMode==PaymentMode.Cash?total:PaymentMode==PaymentMode.Credit?0:(NumericNormalizer.TryParseDecimal(PaidText,out var partial)?decimal.Round(partial,2):-1);
        if(total<=0||paid<0||paid>total||(PaymentMode==PaymentMode.Partial&&(paid<=0||paid>=total))){StatusText="تحقق من الإجمالي وقيمة المدفوع.";return;}
        if(PaymentMode!=PaymentMode.Cash&&PersonId is null){StatusText="اختر المورد للفواتير الآجلة أو الجزئية.";return;}
        IsSaving=true;StatusText="جارٍ حفظ الفاتورة…";
        try{
            var invoice=new Invoice(Guid.NewGuid(),InvoiceType.Purchase,PersonId,DateTimeOffset.Now,total,0,total,paid,PaymentMode,Note.Trim(),session.CurrentUser!.Id);
            var invoiceLines=Lines.Select(x=>new InvoiceLine(Guid.NewGuid(),invoice.Id,x.ProductId,x.ProductName,x.Quantity,x.Unit,x.UnitPrice,0,x.LineTotal){BatchNumber=x.BatchNumber,ExpiryDate=x.ExpiryDate,CostPriceSnapshot=x.CostPriceSnapshot,QuantityMinor=x.QuantitySmallUnits,ConversionFactorApplied=x.ConversionFactorApplied,UnitSelected=x.Unit}).ToArray();
            var movements=Lines.Select(x=>new StockMovement(Guid.NewGuid(),x.ProductId,StockMovementType.Purchase,x.QuantitySmallUnits,decimal.Round(x.UnitPrice*x.Quantity/x.QuantitySmallUnits,4),Note.Trim(),DateTimeOffset.Now,session.CurrentUser!.Id)).ToArray();
            LedgerOperation? operation=PersonId is Guid person&&invoice.Total-invoice.Paid>0?new LedgerOperation(Guid.NewGuid(),person,OperationType.DebtToUs,invoice.Total-invoice.Paid,Note.Trim(),DateTimeOffset.Now,session.CurrentUser!.Id):null;
            await repo.CreateInvoiceTransactionWithMovementsAsync(invoice,invoiceLines,movements,operation);
            StatusText="تم حفظ الفاتورة بنجاح.";IsDialogOpen=false;Lines.Clear();OnPropertyChanged(nameof(LinesTotal));OnPropertyChanged(nameof(IsCartEmpty));ProductId=null;ProductSearch="";QuantityText="1";PriceText="";PaidText="0";Note="";_allProducts=await repo.GetProductsAsync();Products=_allProducts;await LoadInvoiceHistoryAsync();Recalc();
        }catch(Exception ex){StatusText=ex.Message;}
        finally{IsSaving=false;}
    }
}