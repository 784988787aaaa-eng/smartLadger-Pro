using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using SmartLedger.Core.Abstractions;
using SmartLedger.Core.Models;
using SmartLedger.Core.Services;

namespace SmartLedger.App.ViewModels;

public partial class InventoryViewModel(IAppRepository repo) : ObservableObject
{
    [ObservableProperty] private IReadOnlyList<Product> products = Array.Empty<Product>();
    private IReadOnlyList<Product> _allProducts = Array.Empty<Product>();
    [ObservableProperty] private IReadOnlyList<PersonSummary> suppliers = Array.Empty<PersonSummary>();
    [ObservableProperty] private IReadOnlyList<ProductMovementRow> selectedProductMovements = Array.Empty<ProductMovementRow>();
    [ObservableProperty] private IReadOnlyList<ProductBatch> selectedProductBatches = Array.Empty<ProductBatch>();
    [ObservableProperty] private string newBatchNumber = "";
    [ObservableProperty] private string newBatchQuantity = "0";
    [ObservableProperty] private string newBatchCost = "0";
    [ObservableProperty] private string newBatchExpiry = "";

    [ObservableProperty] private string search = "";
    [ObservableProperty] private bool isSearchFocusRequested;
    [ObservableProperty] private bool isDialogOpen;
    [ObservableProperty] private bool isSaving;
    [ObservableProperty] private bool isEditing;
    [ObservableProperty] private Product? selectedProduct;
    [ObservableProperty] private Guid? newSupplierId;
    [ObservableProperty] private string newName = "";
    [ObservableProperty] private string newBarcode = "";
    [ObservableProperty] private string newCategory = "عام";
    [ObservableProperty] private string newLargeUnit = "كرتون";
    [ObservableProperty] private string newSmallUnit = "حبة";
    [ObservableProperty] private string newConversion = "24";
    [ObservableProperty] private string newOpeningQuantity = "0";
    [ObservableProperty] private string newPurchasePrice = "0";
    [ObservableProperty] private string newSalePrice = "0";
    [ObservableProperty] private string newMinimumSellingPrice = "0";
    [ObservableProperty] private string newAlert = "10";
    [ObservableProperty] private string newNote = "";
    [ObservableProperty] private string newShelfLocation = "";
    [ObservableProperty] private string newWarehouse = "المخزن الرئيسي";
    [ObservableProperty] private bool enableMajorUnit = true;
    [ObservableProperty] private string newMajorPurchasePrice = "0";
    [ObservableProperty] private string newMajorSalePrice = "0";
    [ObservableProperty] private string newOpeningLargeQuantity = "0";
    [ObservableProperty] private string selectedWarehouse = "الكل";
    [ObservableProperty] private string stockFilter = "All";
    private bool _syncingUnits;

    public IReadOnlyList<string> Categories => _allProducts.Select(p => p.Category).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.CurrentCultureIgnoreCase).OrderBy(x => x).Prepend("عام").Distinct(StringComparer.CurrentCultureIgnoreCase).ToArray();
    public IReadOnlyList<string> Warehouses => Products.Select(p => p.WarehouseName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x).Prepend("الكل").ToArray();
    public IReadOnlyList<string> ProductWarehouses => _allProducts.Select(p => p.WarehouseName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x).DefaultIfEmpty("المخزن الرئيسي").ToArray();
    public decimal CalculatedUnitCostMinor => EnableMajorUnit ? ParseDecimal(NewMajorPurchasePrice) / Math.Max(1m, ParseDecimal(NewConversion)) : ParseDecimal(NewPurchasePrice);
    public decimal ProfitPerUnit => ParseDecimal(NewSalePrice) - CalculatedUnitCostMinor;
    public decimal ProfitPerMajor => ParseDecimal(NewMajorSalePrice) - ParseDecimal(NewMajorPurchasePrice);
    public decimal ProfitMargin => CalculatedUnitCostMinor > 0m ? decimal.Round((ProfitPerUnit / CalculatedUnitCostMinor) * 100m, 1) : 0m;
    public bool IsSellingBelowCost => ParseDecimal(NewSalePrice) < CalculatedUnitCostMinor;
    public string PackagingSummary => EnableMajorUnit ? $"1 {NewLargeUnit} = {ParseDecimal(NewConversion):0.##} {NewSmallUnit}" : $"الوحدة: {NewSmallUnit}";
    public decimal NetOpeningSmallUnits => EnableMajorUnit ? ParseDecimal(NewOpeningLargeQuantity) * Math.Max(1m, ParseDecimal(NewConversion)) : ParseDecimal(NewOpeningQuantity);
    [ObservableProperty] private string statusText = "";

    public int ProductsCount => Products.Count;
    private IReadOnlyDictionary<Guid,decimal> _productCostValues = new Dictionary<Guid,decimal>();
    public decimal StockValue => Products.Sum(x => _productCostValues.TryGetValue(x.Id,out var value) ? value : x.StockSmallUnits * x.PurchasePrice);
    public decimal RetailStockValue => Products.Sum(x => x.StockSmallUnits * (x.SellingPriceMinor > 0m ? x.SellingPriceMinor : x.SalePrice));
    public string SelectedProductStockBreakdown => SelectedProduct is { } p ? InventoryUnitConverter.BreakDown(p.StockSmallUnits, Math.Max(1m,p.ConversionFactor), p.LargeUnit, p.SmallUnit).Formatted : "—";
    public decimal SelectedProductCostValue => SelectedProduct is { } p ? (_productCostValues.TryGetValue(p.Id,out var value) ? value : p.StockSmallUnits * p.PurchasePrice) : 0m;
    public decimal SelectedProductRetailValue => SelectedProduct is { } p ? p.StockSmallUnits * p.SellingPriceMinor : 0m;
    public decimal SelectedProductIncomingQuantity => SelectedProductMovements.Where(m => m.TypeLabel.Contains("وارد", StringComparison.Ordinal) || m.TypeLabel.Contains("افتتاحي", StringComparison.Ordinal)).Sum(m => m.QuantitySmallUnits);
    public decimal SelectedProductOutgoingQuantity => SelectedProductMovements.Where(m => m.TypeLabel.Contains("منصرف", StringComparison.Ordinal)).Sum(m => m.QuantitySmallUnits);
    public int LowStockCount => Products.Count(x => x.StockSmallUnits <= x.AlertThreshold);
    public string SelectedProductSupplier => SelectedProduct?.DefaultSupplierName ?? "بدون مورد";
    public string SelectedProductLocation => string.IsNullOrWhiteSpace(SelectedProduct?.ShelfLocation) ? "غير محدد" : SelectedProduct!.ShelfLocation;

    public async Task LoadAsync()
    {
        _allProducts = await repo.GetProductsAsync(Search);
        _productCostValues = await repo.GetProductInventoryCostValuesAsync();
        Products = _allProducts;
        Suppliers = await repo.GetSupplierPeopleAsync();
        ApplyFilter();
        OnPropertyChanged(nameof(ProductsCount));
        OnPropertyChanged(nameof(StockValue));
        OnPropertyChanged(nameof(SelectedProductCostValue));
        OnPropertyChanged(nameof(RetailStockValue));
        OnPropertyChanged(nameof(LowStockCount));
        OnPropertyChanged(nameof(Warehouses));
        OnPropertyChanged(nameof(ProductWarehouses));
        OnPropertyChanged(nameof(Categories));
        if (SelectedProduct is not null)
            await LoadMovementsAsync(SelectedProduct.Id);
    }

    partial void OnSearchChanged(string value) => _ = LoadAsync();

    partial void OnNewNameChanged(string value) => OnPropertyChanged(nameof(BarcodePreview));
    partial void OnNewBarcodeChanged(string value) => OnPropertyChanged(nameof(BarcodePreview));
    partial void OnNewSmallUnitChanged(string value) => NotifyPackaging();
    partial void OnNewLargeUnitChanged(string value) => NotifyPackaging();
    partial void OnNewPurchasePriceChanged(string value) { if (!_syncingUnits) SyncMajorFromMinor(); if (!IsEditing && (string.IsNullOrWhiteSpace(NewMinimumSellingPrice) || ParseDecimal(NewMinimumSellingPrice)==0m)) NewMinimumSellingPrice=value; NotifyProfit(); }
    partial void OnNewSalePriceChanged(string value) { if (!_syncingUnits) SyncMajorFromMinor(); NotifyProfit(); }
    partial void OnNewMajorPurchasePriceChanged(string value) { if (!_syncingUnits) SyncMinorFromMajor(); NotifyProfit(); }
    partial void OnNewMajorSalePriceChanged(string value) { if (!_syncingUnits) SyncMinorFromMajor(); NotifyProfit(); }
    partial void OnNewConversionChanged(string value) { if (!_syncingUnits) SyncMajorFromMinor(); OnPropertyChanged(nameof(NetOpeningSmallUnits)); NotifyProfit(); NotifyPackaging(); }
    partial void OnEnableMajorUnitChanged(bool value) { OnPropertyChanged(nameof(NetOpeningSmallUnits)); NotifyProfit(); NotifyPackaging(); if (value) SyncMajorFromMinor(); }
    partial void OnNewOpeningLargeQuantityChanged(string value) => OnPropertyChanged(nameof(NetOpeningSmallUnits));
    partial void OnNewOpeningQuantityChanged(string value) => OnPropertyChanged(nameof(NetOpeningSmallUnits));
    partial void OnSelectedWarehouseChanged(string value) => ApplyFilter();
    partial void OnStockFilterChanged(string value) => ApplyFilter();

    private static decimal ParseDecimal(string value) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out var n) ? n : 0m;
    public string BarcodePreview => string.IsNullOrWhiteSpace(NewBarcode) ? "سيُنشأ عند الطلب" : NewBarcode;
    private void NotifyPackaging() { OnPropertyChanged(nameof(PackagingSummary)); }
    private void NotifyProfit()
    {
        OnPropertyChanged(nameof(CalculatedUnitCostMinor));
        OnPropertyChanged(nameof(ProfitPerUnit));
        OnPropertyChanged(nameof(ProfitPerMajor));
        OnPropertyChanged(nameof(ProfitMargin));
        OnPropertyChanged(nameof(IsSellingBelowCost));
    }
    private void SyncMajorFromMinor()
    {
        if (!EnableMajorUnit) return;
        var factor = Math.Max(1m, ParseDecimal(NewConversion)); _syncingUnits = true;
        NewMajorPurchasePrice = (ParseDecimal(NewPurchasePrice) * factor).ToString("0.##", CultureInfo.CurrentCulture);
        NewMajorSalePrice = (ParseDecimal(NewSalePrice) * factor).ToString("0.##", CultureInfo.CurrentCulture);
        _syncingUnits = false;
    }
    private void SyncMinorFromMajor()
    {
        if (!EnableMajorUnit) return;
        var factor = Math.Max(1m, ParseDecimal(NewConversion)); _syncingUnits = true;
        NewPurchasePrice = (ParseDecimal(NewMajorPurchasePrice) / factor).ToString("0.##", CultureInfo.CurrentCulture);
        NewSalePrice = (ParseDecimal(NewMajorSalePrice) / factor).ToString("0.##", CultureInfo.CurrentCulture);
        _syncingUnits = false;
    }

    [RelayCommand]
    private void FocusSearch() => IsSearchFocusRequested = true;

    [RelayCommand]
    private void SetFilter(string filter) => StockFilter = filter;

    partial void OnSelectedProductChanged(Product? value)
    {
        OnPropertyChanged(nameof(SelectedProductCostValue));
        OnPropertyChanged(nameof(SelectedProductRetailValue));
        OnPropertyChanged(nameof(StockValue));
        OnPropertyChanged(nameof(SelectedProductIncomingQuantity));
        OnPropertyChanged(nameof(SelectedProductOutgoingQuantity));
        OnPropertyChanged(nameof(SelectedProductSupplier));
        OnPropertyChanged(nameof(SelectedProductLocation));
        OnPropertyChanged(nameof(SelectedProductStockBreakdown));
        _ = LoadMovementsAsync(value?.Id);
    }

    private async Task LoadMovementsAsync(Guid? productId)
    {
        if (productId is Guid id)
        {
            SelectedProductMovements = await repo.GetProductMovementsAsync(id, 30);
            OnPropertyChanged(nameof(SelectedProductIncomingQuantity));
            OnPropertyChanged(nameof(SelectedProductOutgoingQuantity));
            SelectedProductBatches = await repo.GetProductBatchesAsync(id);
        }
        else
        {
            SelectedProductMovements = Array.Empty<ProductMovementRow>();
            OnPropertyChanged(nameof(SelectedProductIncomingQuantity));
            OnPropertyChanged(nameof(SelectedProductOutgoingQuantity));
            SelectedProductBatches = Array.Empty<ProductBatch>();
        }
    }

    [RelayCommand]
    private void OpenAdd()
    {
        IsEditing = false;
        SelectedProduct = null;
        NewSupplierId = null;
        NewName = "";
        NewBarcode = "";
        NewCategory = "عام";
        NewLargeUnit = "كرتون";
        NewSmallUnit = "حبة";
        NewConversion = "24";
        NewOpeningQuantity = "0";
        NewPurchasePrice = "0";
        NewSalePrice = "0";
        NewMinimumSellingPrice = "0";
        NewAlert = "10";
        NewNote = "";
        NewShelfLocation = "";
        NewWarehouse = "المخزن الرئيسي";
        EnableMajorUnit = true;
        NewMajorPurchasePrice = "0";
        NewMajorSalePrice = "0";
        NewOpeningLargeQuantity = "0";
        StatusText = "";
        IsDialogOpen = true;
    }

    [RelayCommand]
    private void OpenEdit(Product? product)
    {
        if (product is null) return;
        IsEditing = true;
        SelectedProduct = product;
        NewSupplierId = product.DefaultSupplierId;
        NewName = product.Name;
        NewBarcode = product.Barcode;
        NewCategory = product.Category;
        NewLargeUnit = product.LargeUnit;
        NewSmallUnit = product.SmallUnit;
        NewConversion = product.ConversionFactor.ToString("0.##", CultureInfo.InvariantCulture);
        NewOpeningQuantity = product.StockSmallUnits.ToString("0.##", CultureInfo.InvariantCulture);
        NewPurchasePrice = product.PurchasePrice.ToString("0.##", CultureInfo.InvariantCulture);
        NewSalePrice = product.SalePrice.ToString("0.##", CultureInfo.InvariantCulture);
        NewMinimumSellingPrice = product.MinimumSellingPrice.ToString("0.##", CultureInfo.InvariantCulture);
        NewAlert = product.AlertThreshold.ToString("0.##", CultureInfo.InvariantCulture);
        NewNote = product.Note;
        NewShelfLocation = product.ShelfLocation;
        NewWarehouse = product.WarehouseName;
        EnableMajorUnit = product.ConversionFactor > 1m;
        NewMajorPurchasePrice = (product.PurchasePrice * product.ConversionFactor).ToString("0.##", CultureInfo.CurrentCulture);
        NewMajorSalePrice = (product.SalePrice * product.ConversionFactor).ToString("0.##", CultureInfo.CurrentCulture);
        NewOpeningLargeQuantity = (product.StockSmallUnits / Math.Max(1m, product.ConversionFactor)).ToString("0.##", CultureInfo.CurrentCulture);
        StatusText = "تعديل البيانات — الرصيد الحالي محفوظ ولا يُعاد احتسابه.";
        IsDialogOpen = true;
    }

    [RelayCommand]
    private void CloseDialog()
    {
        if (!IsSaving) IsDialogOpen = false;
    }

    [RelayCommand]
    private async Task SaveNewAsync()
    {
        if (IsSaving) return;
        if (string.IsNullOrWhiteSpace(NewName)) { StatusText = "أدخل اسم الصنف."; return; }
        if (!decimal.TryParse(NewConversion, NumberStyles.Number, CultureInfo.CurrentCulture, out var conversion) || conversion <= 0) { StatusText = "أدخل معامل تحويل صحيحًا."; return; }
        if (!decimal.TryParse(NewPurchasePrice, NumberStyles.Number, CultureInfo.CurrentCulture, out var purchase) || purchase < 0) { StatusText = "أدخل سعر شراء صحيحًا."; return; }
        if (!decimal.TryParse(NewSalePrice, NumberStyles.Number, CultureInfo.CurrentCulture, out var sale) || sale < 0) { StatusText = "أدخل سعر بيع صحيحًا."; return; }
        if (!decimal.TryParse(NewMinimumSellingPrice, NumberStyles.Number, CultureInfo.CurrentCulture, out var minimumSale) || minimumSale < 0) { StatusText = "أدخل حدًا أدنى صحيحًا لسعر البيع."; return; }
        if (!decimal.TryParse(NewAlert, NumberStyles.Number, CultureInfo.CurrentCulture, out var alert) || alert < 0) { StatusText = "أدخل حد تنبيه صحيحًا."; return; }
        if (!IsEditing && ((!EnableMajorUnit && (!decimal.TryParse(NewOpeningQuantity, NumberStyles.Number, CultureInfo.CurrentCulture, out var opening) || opening < 0)) || (EnableMajorUnit && (!decimal.TryParse(NewOpeningLargeQuantity, NumberStyles.Number, CultureInfo.CurrentCulture, out var boxes) || boxes < 0)))) { StatusText = "أدخل الرصيد الافتتاحي الصحيح."; return; }
        var openingQuantity = IsEditing ? SelectedProduct?.StockSmallUnits ?? 0m : NetOpeningSmallUnits;

        IsSaving = true;
        StatusText = IsEditing ? "جارٍ حفظ التعديل…" : "جارٍ حفظ الصنف…";
        try
        {
            Product saved;
            if (IsEditing && SelectedProduct is not null)
            {
                saved = await repo.UpdateProductAsync(SelectedProduct with
                {
                    Name = NewName.Trim(),
                    Barcode = NewBarcode.Trim(),
                    Category = NewCategory.Trim(),
                    LargeUnit = NewLargeUnit.Trim(),
                    SmallUnit = NewSmallUnit.Trim(),
                    ConversionFactor = conversion,
                    PurchasePrice = purchase,
                    SalePrice = sale,
                    AlertThreshold = alert,
                    DefaultSupplierId = NewSupplierId,
                    Note = NewNote.Trim(),
                    ShelfLocation = NewShelfLocation.Trim(),
                    WarehouseName = NewWarehouse.Trim(),
                    SellingPriceMajor = ParseDecimal(NewMajorSalePrice),
                    SellingPriceMinor = sale,
                    MinimumSellingPrice = minimumSale,
                    ReorderPoint = alert,
                    LowStockThreshold = alert,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }
            else
            {
                var effectiveSmallUnit = NewSmallUnit.Trim();
                var effectiveLargeUnit = EnableMajorUnit ? NewLargeUnit.Trim() : effectiveSmallUnit;
                var effectiveConversion = EnableMajorUnit ? conversion : 1m;
                saved = await repo.CreateProductAsync(new Product(
                    Guid.NewGuid(), NewName.Trim(), NewBarcode.Trim(), NewCategory.Trim(),
                    effectiveLargeUnit, effectiveSmallUnit, effectiveConversion, purchase, sale,
                    openingQuantity, alert, DateTimeOffset.Now)
                {
                    DefaultSupplierId = NewSupplierId,
                    Note = NewNote.Trim(),
                    ShelfLocation = NewShelfLocation.Trim(),
                    WarehouseName = NewWarehouse.Trim(),
                    SellingPriceMajor = ParseDecimal(NewMajorSalePrice),
                    SellingPriceMinor = sale,
                    MinimumSellingPrice = minimumSale,
                    ReorderPoint = alert,
                    LowStockThreshold = alert,
                    UpdatedAt = DateTimeOffset.UtcNow
                });
            }

            StatusText = IsEditing ? "تم تحديث الصنف بنجاح." : "تم حفظ الصنف بنجاح.";
            IsDialogOpen = false;
            SelectedProduct = saved;
            await LoadAsync();
        }
        catch (Exception ex) { StatusText = ex.Message; }
        finally { IsSaving = false; }
    }


    [RelayCommand]
    private async Task AddStockBatchAsync()
    {
        if (IsSaving) return;
        if (SelectedProduct is not { } product)
        {
            StatusText = "اختر صنفًا من القائمة قبل إضافة دفعة.";
            return;
        }
        if (!decimal.TryParse(NewBatchQuantity, NumberStyles.Number, CultureInfo.CurrentCulture, out var quantity) || quantity <= 0m)
        {
            StatusText = "أدخل كمية دفعة أكبر من صفر.";
            return;
        }
        if (!decimal.TryParse(NewBatchCost, NumberStyles.Number, CultureInfo.CurrentCulture, out var cost) || cost < 0m)
        {
            StatusText = "أدخل تكلفة صحيحة غير سالبة.";
            return;
        }
        DateTimeOffset? expiry = null;
        if (!string.IsNullOrWhiteSpace(NewBatchExpiry))
        {
            if (!DateTimeOffset.TryParse(NewBatchExpiry, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var parsedExpiry))
            {
                StatusText = "أدخل تاريخ انتهاء صحيحًا أو اترك الحقل فارغًا.";
                return;
            }
            expiry = parsedExpiry;
        }

        IsSaving = true;
        try
        {
            await repo.AddStockBatchAsync(new ProductBatch(
                Guid.NewGuid(), product.Id, NewBatchNumber.Trim(), expiry, quantity, cost,
                product.DefaultSupplierId, null, DateTimeOffset.UtcNow));
            NewBatchNumber = "";
            NewBatchQuantity = "0";
            NewBatchCost = "0";
            NewBatchExpiry = "";
            StatusText = "تمت إضافة الدفعة وتحديث رصيد الصنف بنجاح.";
            await LoadAsync();
            SelectedProduct = _allProducts.FirstOrDefault(p => p.Id == product.Id);
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task SaveAndNewProductAsync()
    {
        await SaveNewAsync();
        if (!IsDialogOpen && StatusText.Contains("بنجاح", StringComparison.Ordinal))
            OpenAdd();
    }

    [RelayCommand]
    private async Task SaveAndCloseProductAsync() => await SaveNewAsync();

    [RelayCommand]
    private void GenerateBarcode()
    {
        if (string.IsNullOrWhiteSpace(NewBarcode))
            NewBarcode = "SL" + DateTimeOffset.UtcNow.ToString("yyMMddHHmmssfff", CultureInfo.InvariantCulture);
    }

    private void ApplyFilter()
    {
        IEnumerable<Product> source = _allProducts;
        if (SelectedWarehouse != "الكل") source = source.Where(p => p.WarehouseName == SelectedWarehouse);
        source = StockFilter switch { "InStock" => source.Where(p => p.StockSmallUnits > p.AlertThreshold), "LowStock" => source.Where(p => p.StockSmallUnits > 0 && p.StockSmallUnits <= p.AlertThreshold), "OutOfStock" => source.Where(p => p.StockSmallUnits <= 0), _ => source };
        Products = source.ToArray();
        OnPropertyChanged(nameof(ProductsCount));
        OnPropertyChanged(nameof(StockValue));
        OnPropertyChanged(nameof(LowStockCount));
    }
    public string GetAvailability(Product p) => p.StockSmallUnits <= 0 ? "نفد" : p.StockSmallUnits <= p.AlertThreshold ? "منخفض" : "متوفر";
}
