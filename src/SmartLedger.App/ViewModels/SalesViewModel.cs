using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;
using SmartLedger.Infrastructure.Integrations;

namespace SmartLedger.App.ViewModels;

public partial class SalesViewModel(IAppRepository repo,IAppSession session):ObservableObject
{
    [ObservableProperty] private IReadOnlyList<PersonSummary> people=Array.Empty<PersonSummary>();
    [ObservableProperty] private IReadOnlyList<Product> products=Array.Empty<Product>();
    private IReadOnlyList<PersonSummary> _allPeople=Array.Empty<PersonSummary>();
    private IReadOnlyList<Product> _allProducts=Array.Empty<Product>();
    private IReadOnlyList<Book> _books=Array.Empty<Book>();
    private bool _settingProductSelection;
    private decimal _lastUnitFactor=1m;
    [ObservableProperty] private string customerSearch="";
    [ObservableProperty] private string productSearch="";
    [ObservableProperty] private string barcodeInput="";
    [ObservableProperty] private string invoiceDiscountText="0";
    [ObservableProperty] private Invoice? lastSavedInvoice;
    [ObservableProperty] private string lastSavedCustomerPhone="";
    [ObservableProperty] private string lastSavedCustomerName="";
    [ObservableProperty] private bool isCustomerSuggestionsOpen;
    [ObservableProperty] private bool isProductSuggestionsOpen;
    [ObservableProperty] private Guid? personId;
    [ObservableProperty] private Guid? productId;
    [ObservableProperty] private string selectedQuantityUnit="";
    [ObservableProperty] private string quantityText="1";
    [ObservableProperty] private string priceText="";
    [ObservableProperty] private string paidText="0";
    [ObservableProperty] private string note="";
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
    public string BalanceLabel=>Remaining>0?"متبقي لنا":"مسدد بالكامل";
    public IReadOnlyList<PaymentModeOption> PaymentModes {get;}=new[]{new PaymentModeOption(PaymentMode.Cash,"نقدي"),new PaymentModeOption(PaymentMode.Credit,"آجل — له"),new PaymentModeOption(PaymentMode.Partial,"جزئي — المتبقي له")};

    public async Task LoadAsync(){_books=await repo.GetBooksAsync();var customerBook=_books.FirstOrDefault(x=>x.Kind==BookKind.Customers);_allPeople=customerBook is null?Array.Empty<PersonSummary>():await repo.GetPeopleAsync(customerBook.Id);_allProducts=await repo.GetProductsAsync();People=_allPeople;Products=_allProducts;}
    partial void OnCustomerSearchChanged(string value){if(PersonId is Guid id&&_allPeople.FirstOrDefault(x=>x.PersonId==id) is { } selected&&!Normalize(value).Equals(Normalize(selected.PersonName),StringComparison.OrdinalIgnoreCase))PersonId=null;People=FilterPeople(value);IsCustomerSuggestionsOpen=!string.IsNullOrWhiteSpace(value)&&People.Count>0;}
    partial void OnProductSearchChanged(string value){if(_settingProductSelection)return;if(ProductId is Guid id&&_allProducts.FirstOrDefault(x=>x.Id==id) is { } selected&&!Normalize(value).Equals(Normalize(selected.Name),StringComparison.OrdinalIgnoreCase))ProductId=null;Products=FilterProducts(value);IsProductSuggestionsOpen=!string.IsNullOrWhiteSpace(value)&&Products.Count>0;}
    partial void OnPersonIdChanged(Guid? value){var p=_allPeople.FirstOrDefault(x=>x.PersonId==value);if(p is not null){CustomerSearch=p.PersonName;IsCustomerSuggestionsOpen=false;}}
    partial void OnProductIdChanged(Guid? value){var p=_allProducts.FirstOrDefault(x=>x.Id==value);_settingProductSelection=true;_lastUnitFactor=1m;if(p is not null){ProductSearch=p.Name;SelectedQuantityUnit=p.SmallUnit;PriceText=p.SalePrice>0?p.SalePrice.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture):"";IsProductSuggestionsOpen=false;}else{SelectedQuantityUnit="";PriceText="";}_settingProductSelection=false;OnPropertyChanged(nameof(QuantityUnits));Recalc();}
    partial void OnSelectedQuantityUnitChanged(string value){OnPropertyChanged(nameof(QuantityUnits));if(_settingProductSelection)return;var product=_allProducts.FirstOrDefault(x=>x.Id==ProductId);if(product is null)return;var nextFactor=QuantityFactor(product);if(NumericNormalizer.TryParseDecimal(PriceText,out var currentPrice)&&_lastUnitFactor>0)PriceText=(currentPrice*nextFactor/_lastUnitFactor).ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);_lastUnitFactor=nextFactor;}
    [RelayCommand] private void OpenNew(){Lines.Clear();OnPropertyChanged(nameof(LinesTotal));OnPropertyChanged(nameof(IsCartEmpty));BarcodeInput="";InvoiceDiscountText="0";LastSavedInvoice=null;PersonId=null;ProductId=null;CustomerSearch="";ProductSearch="";People=_allPeople;Products=_allProducts;IsCustomerSuggestionsOpen=false;IsProductSuggestionsOpen=false;QuantityText="1";PriceText="";PaidText="0";Note="";PaymentMode=PaymentMode.Cash;SyncPaymentChoices();StatusText="";Recalc();IsDialogOpen=true;}
    [RelayCommand] private void OpenQuickPerson(){QuickPersonName=CustomerSearch.Trim();QuickPersonPhone="";QuickPersonNote="";IsQuickPersonDialogOpen=true;}
    [RelayCommand] private void CloseQuickPerson(){IsQuickPersonDialogOpen=false;}
    [RelayCommand] private async Task SaveQuickPersonAsync(){
        if(IsSaving)return;
        if(string.IsNullOrWhiteSpace(QuickPersonName)){StatusText="أدخل اسم الحساب.";return;}
        var book=_books.FirstOrDefault(x=>x.Kind==BookKind.Customers);
        if(book is null){StatusText="لم يُعثر على دفتر العملاء؛ تعذر إنشاء الحساب.";return;}
        try{
            var candidates=await repo.GetPeopleAsync(book.Id,null,null);
            var duplicate=candidates.FirstOrDefault(x=>Normalize(x.PersonName).Equals(Normalize(QuickPersonName),StringComparison.OrdinalIgnoreCase));
            if(duplicate is not null){
                _allPeople=await repo.GetPeopleAsync(book.Id);People=FilterPeople(QuickPersonName);PersonId=duplicate.PersonId;IsQuickPersonDialogOpen=false;StatusText="الحساب موجود بالفعل؛ تم اختياره بدل إنشاء نسخة مكررة.";return;
            }
            IsSaving=true;
            var saved=await repo.CreatePersonAsync(book.Id,QuickPersonName.Trim(),QuickPersonPhone.Trim(),QuickPersonNote.Trim());
            _allPeople=await repo.GetPeopleAsync(book.Id);People=FilterPeople(saved.Name);CustomerSearch=saved.Name;PersonId=saved.Id;IsQuickPersonDialogOpen=false;StatusText="تم إنشاء الحساب وربطه بالفاتورة دون فقد بياناتها.";
        }catch(Exception ex){StatusText=ex.Message;}
        finally{IsSaving=false;}
    }
    [RelayCommand] private void OpenQuickProduct(){QuickProductName=ProductSearch.Trim();QuickProductSmallUnit="";QuickProductLargeUnit="";QuickProductConversion="1";QuickProductPurchasePrice="";QuickProductSalePrice=PriceText;IsQuickProductDialogOpen=true;}
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
            else PriceText=saved.SalePrice>0?saved.SalePrice.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture):"";
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
    partial void OnInvoiceDiscountTextChanged(string value)=>Recalc();
    private decimal CurrentDiscount(decimal gross)
    {
        if(!NumericNormalizer.TryParseDecimal(InvoiceDiscountText,out var discount)||discount<0)return 0m;
        return Math.Min(decimal.Round(gross,2),decimal.Round(discount,2));
    }
    private void Recalc(){var current=ProductId is not null&&NumericNormalizer.TryParseDecimal(QuantityText,out var q)&&q>0&&NumericNormalizer.TryParseDecimal(PriceText,out var p)&&p>0?decimal.Round(q*p,2):0m;var gross=decimal.Round(LinesTotal+current,2);Total=Math.Max(0m,decimal.Round(gross-CurrentDiscount(gross),2));}
    [RelayCommand] private void AddLine(){if(AddCurrentLine())StatusText="تمت إضافة الصنف إلى بنود الفاتورة.";}
    [RelayCommand] private void AddScannedBarcode()
    {
        var barcode=BarcodeInput.Trim();
        if(string.IsNullOrWhiteSpace(barcode)){StatusText="امسح الباركود أو أدخله أولًا.";return;}
        var product=_allProducts.FirstOrDefault(x=>string.Equals(x.Barcode.Trim(),barcode,StringComparison.OrdinalIgnoreCase));
        if(product is null){StatusText="لم يُعثر على صنف بهذا الباركود.";return;}
        ProductId=product.Id;SelectedQuantityUnit=product.SmallUnit;QuantityText="1";PriceText=(product.SellingPriceMinor>0?product.SellingPriceMinor:product.SalePrice).ToString("0.##",System.Globalization.CultureInfo.InvariantCulture);
        if(AddCurrentLine()){BarcodeInput="";StatusText=$"أُضيف {product.Name} من الباركود.";}
    }
    [RelayCommand] private void ApplyDiscount(){StatusText="أدخل قيمة الخصم الإجمالية في حقل الخصم ثم أكمل الفاتورة.";}
    [RelayCommand] private void ClearInvoice(){if(IsSaving)return;Lines.Clear();OnPropertyChanged(nameof(LinesTotal));OnPropertyChanged(nameof(IsCartEmpty));ProductId=null;ProductSearch="";BarcodeInput="";QuantityText="1";PriceText="";InvoiceDiscountText="0";Note="";Recalc();StatusText="تم تفريغ الفاتورة الحالية.";}
    [RelayCommand] private void HoldInvoice(){StatusText="ميزة تعليق الفاتورة ستُفعّل بعد إضافة تخزين محلي للمسودات.";}
    [RelayCommand] private void SendWhatsApp()
    {
        if(LastSavedInvoice is not { } invoice){StatusText="احفظ الفاتورة أولًا قبل إرسالها عبر واتساب.";return;}
        if(string.IsNullOrWhiteSpace(LastSavedCustomerPhone)){StatusText="لا يوجد رقم هاتف للعميل المرتبط بالفاتورة الأخيرة.";return;}
        try
        {
            var identity=repo.GetIdentityAsync().GetAwaiter().GetResult();
            var message=$"مرحباً {LastSavedCustomerName}،\\nفاتورة مبيعات رقم: #{invoice.InvoiceNumber}\\nالمحل: {identity.BusinessName}\\nإجمالي الفاتورة: {invoice.Total:N2} {identity.Currency}\\nالمدفوع: {invoice.Paid:N2} {identity.Currency}\\nالمتبقي: {invoice.RemainingAmount:N2} {identity.Currency}\\nالتاريخ: {invoice.IssuedAt:yyyy/MM/dd HH:mm}\\nشكراً لزيارتكم!";
            new WhatsAppService().OpenWhatsApp(LastSavedCustomerPhone,message);StatusText="تم فتح واتساب برسالة الفاتورة؛ راجعها قبل الإرسال.";
        }
        catch(Exception ex){StatusText=ex.Message;}
    }
    [RelayCommand] private void RemoveLine(InvoiceDraftLine? line){if(line is not null&&Lines.Remove(line)){OnPropertyChanged(nameof(LinesTotal));OnPropertyChanged(nameof(IsCartEmpty));Recalc();}}
    private bool AddCurrentLine(){
        if(ProductId is null){StatusText="اختر صنفًا مسجلًا أو أنشئ صنفًا جديدًا أولًا.";return false;}
        var product=_allProducts.FirstOrDefault(x=>x.Id==ProductId);if(product is null){StatusText="الصنف المحدد غير متاح؛ أعد اختياره.";return false;}
        if(!NumericNormalizer.TryParseDecimal(QuantityText,out var q)||q<=0){StatusText="أدخل كمية صحيحة.";return false;}
        if(!NumericNormalizer.TryParseDecimal(PriceText,out var price)||price<=0){StatusText="أدخل سعرًا صحيحًا أكبر من صفر.";return false;}
        var smallQuantity=q*QuantityFactor(product);if(smallQuantity<=0){StatusText="تحقق من الوحدة ومعامل التحويل.";return false;}
        var alreadyInCart=Lines.Where(x=>x.ProductId==product.Id).Sum(x=>x.QuantitySmallUnits);if(alreadyInCart+smallQuantity>product.StockSmallUnits){StatusText=$"الكمية المطلوبة تتجاوز الرصيد المتاح ({product.StockSmallUnits:0.##} {product.SmallUnit}).";return false;}
        Lines.Add(new InvoiceDraftLine(product.Id,product.Name,q,SelectedQuantityUnit,price,decimal.Round(q*price,2),smallQuantity){CostPriceSnapshot=product.PurchasePrice,Barcode=product.Barcode,ConversionFactorApplied=QuantityFactor(product)});OnPropertyChanged(nameof(LinesTotal));OnPropertyChanged(nameof(IsCartEmpty));ProductId=null;ProductSearch="";QuantityText="1";PriceText="";IsProductSuggestionsOpen=false;Recalc();return true;
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
        if(PaymentMode!=PaymentMode.Cash&&PersonId is null){StatusText="اختر العميل للفواتير الآجلة أو الجزئية.";return;}
        IsSaving=true;StatusText="جارٍ حفظ الفاتورة…";
        try{
            var invoice=new Invoice(Guid.NewGuid(),InvoiceType.Sale,PersonId,DateTimeOffset.Now,total,0,total,paid,PaymentMode,Note.Trim(),session.CurrentUser!.Id);
            var invoiceLines=Lines.Select(x=>new InvoiceLine(Guid.NewGuid(),invoice.Id,x.ProductId,x.ProductName,x.Quantity,x.Unit,x.UnitPrice,0,x.LineTotal)).ToArray();
            var movements=Lines.Select(x=>new StockMovement(Guid.NewGuid(),x.ProductId,StockMovementType.Sale,x.QuantitySmallUnits,decimal.Round(x.UnitPrice*x.Quantity/x.QuantitySmallUnits,4),Note.Trim(),DateTimeOffset.Now,session.CurrentUser!.Id)).ToArray();
            LedgerOperation? operation=PersonId is Guid person&&invoice.Total-invoice.Paid>0?new LedgerOperation(Guid.NewGuid(),person,OperationType.DebtOnUs,invoice.Total-invoice.Paid,Note.Trim(),DateTimeOffset.Now,session.CurrentUser!.Id):null;
            await repo.CreateInvoiceTransactionWithMovementsAsync(invoice,invoiceLines,movements,operation);
            StatusText="تم حفظ الفاتورة بنجاح.";IsDialogOpen=false;Lines.Clear();OnPropertyChanged(nameof(LinesTotal));ProductId=null;ProductSearch="";QuantityText="1";PriceText="";PaidText="0";Note="";_allProducts=await repo.GetProductsAsync();Products=_allProducts;Recalc();
        }catch(Exception ex){StatusText=ex.Message;}
        finally{IsSaving=false;}
    }
}