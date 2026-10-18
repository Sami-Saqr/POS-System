using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using CarPartManager.Data;
using CarPartManager.Models;
using CarPartManager.MVVM;
using CarPartManager.Services;

namespace CarPartManager.ViewModels
{
    /// <summary>
    /// صنف وسيط لعرض بنود فاتورة المبيعات وتحديد الكمية المرتجعة وحالة القطعة
    /// </summary>
    public class SalesReturnDraftItem : ViewModelBase
    {
        public int SalesInvoiceItemId { get; set; }
        public int ProductId { get; set; }
        public string PartNumber { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int OriginalQuantity { get; set; }
        public int AlreadyReturnedQuantity { get; set; }
        public int MaxReturnableQuantity => Math.Max(0, OriginalQuantity - AlreadyReturnedQuantity);
        public decimal UnitPrice { get; set; }

        private int _returnQuantity;
        public int ReturnQuantity
        {
            get => _returnQuantity;
            set
            {
                if (value < 0) value = 0;
                if (value > MaxReturnableQuantity) value = MaxReturnableQuantity;
                if (SetProperty(ref _returnQuantity, value))
                {
                    OnPropertyChanged(nameof(LineTotal));
                    OnPropertyChanged(nameof(IsSelected));
                    ValueChangedCallback?.Invoke();
                }
            }
        }

        public bool IsSelected => ReturnQuantity > 0;
        public decimal LineTotal => ReturnQuantity * UnitPrice;

        private string _condition = "إرجاع إلى المخزون المتاح للبيع";
        public string Condition
        {
            get => _condition;
            set => SetProperty(ref _condition, value);
        }

        public Action? ValueChangedCallback { get; set; }
    }

    /// <summary>
    /// صنف وسيط لعرض بنود فاتورة المشتريات وتحديد الكمية المرتجعة وفحص رصيد المخزون
    /// </summary>
    public class PurchaseReturnDraftItem : ViewModelBase
    {
        public int PurchaseInvoiceItemId { get; set; }
        public int ProductId { get; set; }
        public string PartNumber { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int OriginalQuantity { get; set; }
        public int AlreadyReturnedQuantity { get; set; }
        public int CurrentStockQuantity { get; set; }
        public int MaxReturnableQuantity => Math.Max(0, OriginalQuantity - AlreadyReturnedQuantity);
        public decimal UnitPrice { get; set; }

        private int _returnQuantity;
        public int ReturnQuantity
        {
            get => _returnQuantity;
            set
            {
                if (value < 0) value = 0;
                if (value > MaxReturnableQuantity) value = MaxReturnableQuantity;
                if (SetProperty(ref _returnQuantity, value))
                {
                    OnPropertyChanged(nameof(LineTotal));
                    OnPropertyChanged(nameof(IsSelected));
                    ValueChangedCallback?.Invoke();
                }
            }
        }

        public bool IsSelected => ReturnQuantity > 0;
        public decimal LineTotal => ReturnQuantity * UnitPrice;

        public Action? ValueChangedCallback { get; set; }
    }

    /// <summary>
    /// نموذج عرض شاشة المرتجعات الشاملة (مرتجعات المبيعات ومرتجعات المشتريات)
    /// </summary>
    public class ReturnsViewModel : ViewModelBase
    {
        private readonly IReturnsService _returnsService;

        // التبويب النشط: 0 = مرتجعات مبيعات، 1 = مرتجعات مشتريات
        private int _selectedTabIndex = 0;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (SetProperty(ref _selectedTabIndex, value))
                {
                    OnPropertyChanged(nameof(IsSalesTab));
                    OnPropertyChanged(nameof(IsPurchasesTab));
                }
            }
        }

        public bool IsSalesTab => SelectedTabIndex == 0;
        public bool IsPurchasesTab => SelectedTabIndex == 1;

        // ==================== تبويب مرتجعات المبيعات ====================
        public ObservableCollection<SalesInvoiceModel> EligibleSalesInvoices { get; } = new();
        public ObservableCollection<SalesReturnDraftItem> SalesDraftItems { get; } = new();
        public ObservableCollection<SalesReturnModel> SalesReturnsHistory { get; } = new();

        private SalesInvoiceModel? _selectedSalesInvoice;
        public SalesInvoiceModel? SelectedSalesInvoice
        {
            get => _selectedSalesInvoice;
            set
            {
                if (SetProperty(ref _selectedSalesInvoice, value))
                {
                    _ = OnSalesInvoiceSelectedAsync(value);
                }
            }
        }

        private string _salesReturnNumber = string.Empty;
        public string SalesReturnNumber
        {
            get => _salesReturnNumber;
            set => SetProperty(ref _salesReturnNumber, value);
        }

        private string _salesReturnReason = "عيب في القطعة";
        public string SalesReturnReason
        {
            get => _salesReturnReason;
            set => SetProperty(ref _salesReturnReason, value);
        }

        private string _salesReturnNotes = string.Empty;
        public string SalesReturnNotes
        {
            get => _salesReturnNotes;
            set => SetProperty(ref _salesReturnNotes, value);
        }

        private decimal _salesReturnTotalAmount;
        public decimal SalesReturnTotalAmount
        {
            get => _salesReturnTotalAmount;
            set => SetProperty(ref _salesReturnTotalAmount, value);
        }

        private string _salesStatusMessage = string.Empty;
        public string SalesStatusMessage
        {
            get => _salesStatusMessage;
            set => SetProperty(ref _salesStatusMessage, value);
        }

        // ==================== تبويب مرتجعات المشتريات ====================
        public ObservableCollection<PurchaseInvoiceModel> EligiblePurchaseInvoices { get; } = new();
        public ObservableCollection<PurchaseReturnDraftItem> PurchaseDraftItems { get; } = new();
        public ObservableCollection<PurchaseReturnModel> PurchaseReturnsHistory { get; } = new();

        private PurchaseInvoiceModel? _selectedPurchaseInvoice;
        public PurchaseInvoiceModel? SelectedPurchaseInvoice
        {
            get => _selectedPurchaseInvoice;
            set
            {
                if (SetProperty(ref _selectedPurchaseInvoice, value))
                {
                    _ = OnPurchaseInvoiceSelectedAsync(value);
                }
            }
        }

        private string _purchaseReturnNumber = string.Empty;
        public string PurchaseReturnNumber
        {
            get => _purchaseReturnNumber;
            set => SetProperty(ref _purchaseReturnNumber, value);
        }

        private string _purchaseReturnReason = "غير مطابق للمواصفات";
        public string PurchaseReturnReason
        {
            get => _purchaseReturnReason;
            set => SetProperty(ref _purchaseReturnReason, value);
        }

        private string _purchaseReturnNotes = string.Empty;
        public string PurchaseReturnNotes
        {
            get => _purchaseReturnNotes;
            set => SetProperty(ref _purchaseReturnNotes, value);
        }

        private decimal _purchaseReturnTotalAmount;
        public decimal PurchaseReturnTotalAmount
        {
            get => _purchaseReturnTotalAmount;
            set => SetProperty(ref _purchaseReturnTotalAmount, value);
        }

        private string _purchaseStatusMessage = string.Empty;
        public string PurchaseStatusMessage
        {
            get => _purchaseStatusMessage;
            set => SetProperty(ref _purchaseStatusMessage, value);
        }

        // ==================== أوامر التنفيذ ====================
        public RelayCommand PostSalesReturnCommand { get; }
        public RelayCommand ClearSalesReturnCommand { get; }
        public RelayCommand PostPurchaseReturnCommand { get; }
        public RelayCommand ClearPurchaseReturnCommand { get; }
        public RelayCommand RefreshAllCommand { get; }
        public RelayCommand ShowSalesTabCommand { get; }
        public RelayCommand ShowPurchasesTabCommand { get; }

        public ReturnsViewModel(IReturnsService? returnsService = null)
        {
            _returnsService = returnsService ?? new SqliteReturnsService();

            ShowSalesTabCommand = new RelayCommand(() => SelectedTabIndex = 0);
            ShowPurchasesTabCommand = new RelayCommand(() => SelectedTabIndex = 1);

            PostSalesReturnCommand = new RelayCommand(async () => await PostSalesReturnAsync(), () => CanPostSalesReturn);
            ClearSalesReturnCommand = new RelayCommand(ClearSalesReturnForm);
            PostPurchaseReturnCommand = new RelayCommand(async () => await PostPurchaseReturnAsync(), () => CanPostPurchaseReturn);
            ClearPurchaseReturnCommand = new RelayCommand(ClearPurchaseReturnForm);
            RefreshAllCommand = new RelayCommand(async () => await LoadAllDataAsync());

            _ = LoadAllDataAsync();
        }

        public bool CanPostSalesReturn =>
            SelectedSalesInvoice != null &&
            SalesDraftItems.Any(i => i.ReturnQuantity > 0) &&
            !string.IsNullOrWhiteSpace(SalesReturnReason);

        public bool CanPostPurchaseReturn =>
            SelectedPurchaseInvoice != null &&
            PurchaseDraftItems.Any(i => i.ReturnQuantity > 0) &&
            !string.IsNullOrWhiteSpace(PurchaseReturnReason);

        public async Task LoadAllDataAsync()
        {
            try
            {
                var salesReturnNum = await _returnsService.GenerateSalesReturnNumberAsync();
                var eligibleSales = await _returnsService.GetEligibleSalesInvoicesAsync();
                var salesHistory = await _returnsService.GetAllSalesReturnsAsync();

                var purchaseReturnNum = await _returnsService.GeneratePurchaseReturnNumberAsync();
                var eligiblePurchases = await _returnsService.GetEligiblePurchaseInvoicesAsync();
                var purchaseHistory = await _returnsService.GetAllPurchaseReturnsAsync();

                void UpdateUi()
                {
                    SalesReturnNumber = salesReturnNum;
                    EligibleSalesInvoices.Clear();
                    foreach (var inv in eligibleSales) EligibleSalesInvoices.Add(inv);

                    SalesReturnsHistory.Clear();
                    foreach (var ret in salesHistory) SalesReturnsHistory.Add(ret);

                    PurchaseReturnNumber = purchaseReturnNum;
                    EligiblePurchaseInvoices.Clear();
                    foreach (var inv in eligiblePurchases) EligiblePurchaseInvoices.Add(inv);

                    PurchaseReturnsHistory.Clear();
                    foreach (var ret in purchaseHistory) PurchaseReturnsHistory.Add(ret);
                }

                if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
                {
                    Application.Current.Dispatcher.Invoke(UpdateUi);
                }
                else
                {
                    UpdateUi();
                }
            }
            catch (Exception ex)
            {
                SalesStatusMessage = $"تنبيه: {ex.Message}";
                PurchaseStatusMessage = $"تنبيه: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error loading returns: {ex.Message}");
            }
        }

        public async Task LoadSalesDataAsync() => await LoadAllDataAsync();
        public async Task LoadPurchaseDataAsync() => await LoadAllDataAsync();

        private async Task OnSalesInvoiceSelectedAsync(SalesInvoiceModel? invoice)
        {
            SalesDraftItems.Clear();
            SalesReturnTotalAmount = 0;

            if (invoice == null)
            {
                SalesStatusMessage = "يرجى اختيار فاتورة بيع معتمدة لعرض بنودها المتاحة للإرجاع.";
                return;
            }

            var returnedQuantities = await _returnsService.GetSalesInvoiceReturnedQuantitiesAsync(invoice.Id);

            foreach (var item in invoice.Items)
            {
                int returned = returnedQuantities.TryGetValue(item.Id, out int q) ? q : 0;
                var draftItem = new SalesReturnDraftItem
                {
                    SalesInvoiceItemId = item.Id,
                    ProductId = item.ProductId,
                    PartNumber = item.PartNumber,
                    ProductName = item.ProductName,
                    OriginalQuantity = item.Quantity,
                    AlreadyReturnedQuantity = returned,
                    UnitPrice = item.UnitPrice,
                    ReturnQuantity = 0,
                    Condition = "صالح للبيع",
                    ValueChangedCallback = RecalculateSalesReturnTotal
                };

                SalesDraftItems.Add(draftItem);
            }

            RecalculateSalesReturnTotal();
            SalesStatusMessage = $"تم تحميل بنود الفاتورة رقم '{invoice.InvoiceNumber}'. حدد الكميات المراد إرجاعها.";
        }

        private void RecalculateSalesReturnTotal()
        {
            SalesReturnTotalAmount = SalesDraftItems.Sum(i => i.LineTotal);
            PostSalesReturnCommand.RaiseCanExecuteChanged();
        }

        private void ClearSalesReturnForm()
        {
            SelectedSalesInvoice = null;
            SalesDraftItems.Clear();
            SalesReturnTotalAmount = 0;
            SalesReturnReason = "عيب في القطعة";
            SalesReturnNotes = string.Empty;
            SalesStatusMessage = "تم تفريغ نموذج مرتجع المبيعات.";
            _ = LoadSalesDataAsync();
        }

        private async Task PostSalesReturnAsync()
        {
            if (SelectedSalesInvoice == null) return;

            var itemsToReturn = SalesDraftItems.Where(i => i.ReturnQuantity > 0).ToList();
            if (itemsToReturn.Count == 0)
            {
                MessageBox.Show("يرجى تحديد كمية إرجاع لصنف واحد على الأقل.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // التحقق من وجود أصناف مستبعدة نهائياً وطلب تأكيد صريح من المستخدم
            var disposedItems = itemsToReturn.Where(i => i.Condition == "استبعاد نهائي من المخزون").ToList();
            if (disposedItems.Count > 0)
            {
                var disposedNames = string.Join("\n", disposedItems.Select(x => $"• {x.ProductName} (الكمية: {x.ReturnQuantity})"));
                var disposalConfirm = MessageBox.Show(
                    $"تنبيه استبعاد نهائي من المخزون:\n\n" +
                    $"تم تحديد مصير (استبعاد نهائي من المخزون) للأصناف التالية:\n{disposedNames}\n\n" +
                    "هذه القطع لن يتم إرجاعها إلى رصيد البيع ولا إلى مخزون التالف (تخريد/إتلاف نهائي)، مع توثيق السجل التاريخي وحركات المخزون بدقة.\n\n" +
                    "هل تؤكد رغبتك في الاستبعاد النهائي لهذه الأصناف؟",
                    "تأكيد الاستبعاد النهائي من المخزون",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (disposalConfirm != MessageBoxResult.Yes) return;
            }

            var confirmResult = MessageBox.Show(
                $"هل أنت متأكد من اعتماد مرتجع المبيعات رقم '{SalesReturnNumber}'؟\n" +
                $"الفاتورة الأصلية: {SelectedSalesInvoice.InvoiceNumber}\n" +
                $"إجمالي المرتجع: {SalesReturnTotalAmount:N2} ج.م\n" +
                $"عدد الأصناف: {itemsToReturn.Count} صنف\n" +
                "سيتم تحديث المخزون وحركات الصنف بحسب مصير كل قطعة وتسجيل الحركات أوتوماتيكياً.",
                "تأكيد اعتماد مرتجع المبيعات",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes) return;

            try
            {
                var returnModel = new SalesReturnModel
                {
                    ReturnNumber = SalesReturnNumber,
                    SalesInvoiceId = SelectedSalesInvoice.Id,
                    ReturnDate = DateTime.Now,
                    Reason = SalesReturnReason,
                    Notes = SalesReturnNotes,
                    Status = "معتمدة",
                    Items = itemsToReturn.Select(i => new SalesReturnItemModel
                    {
                        SalesInvoiceItemId = i.SalesInvoiceItemId,
                        ProductId = i.ProductId,
                        ProductName = i.ProductName,
                        Quantity = i.ReturnQuantity,
                        UnitPrice = i.UnitPrice,
                        TotalPrice = i.LineTotal,
                        Condition = i.Condition
                    }).ToList()
                };

                await _returnsService.ProcessSalesReturnAsync(returnModel);

                DataEvents.RaiseInventoryOrInvoicesChanged();

                MessageBox.Show(
                    $"تم اعتماد مرتجع المبيعات رقم '{returnModel.ReturnNumber}' بنجاح!\n" +
                    $"تم تحديث رصيد المخزون للقطع الصالحة للبيع وحفظ الحركة بدقة.",
                    "نجاح العملية",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                ClearSalesReturnForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل اعتماد مرتجع المبيعات:\n{ex.Message}", "خطأ في المعالجة", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task OnPurchaseInvoiceSelectedAsync(PurchaseInvoiceModel? invoice)
        {
            PurchaseDraftItems.Clear();
            PurchaseReturnTotalAmount = 0;

            if (invoice == null)
            {
                PurchaseStatusMessage = "يرجى اختيار فاتورة مشتريات معتمدة لعرض بنودها المتاحة للإرجاع.";
                return;
            }

            var returnedQuantities = await _returnsService.GetPurchaseInvoiceReturnedQuantitiesAsync(invoice.Id);

            // جلب الأرصدة الحالية في المخزون
            using var context = new CarPartsDbContext();
            var productIds = invoice.Items.Select(i => i.ProductId).Distinct().ToList();
            var stockMap = await context.Products
                                        .Where(p => productIds.Contains(p.Id))
                                        .ToDictionaryAsync(p => p.Id, p => p.Quantity);

            foreach (var item in invoice.Items)
            {
                int returned = returnedQuantities.TryGetValue(item.Id, out int q) ? q : 0;
                int currentStock = stockMap.TryGetValue(item.ProductId, out int cs) ? cs : 0;

                var draftItem = new PurchaseReturnDraftItem
                {
                    PurchaseInvoiceItemId = item.Id,
                    ProductId = item.ProductId,
                    PartNumber = item.PartNumber,
                    ProductName = item.ProductName,
                    OriginalQuantity = item.Quantity,
                    AlreadyReturnedQuantity = returned,
                    CurrentStockQuantity = currentStock,
                    UnitPrice = item.UnitPrice,
                    ReturnQuantity = 0,
                    ValueChangedCallback = RecalculatePurchaseReturnTotal
                };

                PurchaseDraftItems.Add(draftItem);
            }

            RecalculatePurchaseReturnTotal();
            PurchaseStatusMessage = $"تم تحميل بنود الفاتورة رقم '{invoice.InvoiceNumber}'. حدد الكميات المراد إرجاعها للمورد.";
        }

        private void RecalculatePurchaseReturnTotal()
        {
            PurchaseReturnTotalAmount = PurchaseDraftItems.Sum(i => i.LineTotal);
            PostPurchaseReturnCommand.RaiseCanExecuteChanged();
        }

        private void ClearPurchaseReturnForm()
        {
            SelectedPurchaseInvoice = null;
            PurchaseDraftItems.Clear();
            PurchaseReturnTotalAmount = 0;
            PurchaseReturnReason = "غير مطابق للمواصفات";
            PurchaseReturnNotes = string.Empty;
            PurchaseStatusMessage = "تم تفريغ نموذج مرتجع المشتريات.";
            _ = LoadPurchaseDataAsync();
        }

        private async Task PostPurchaseReturnAsync()
        {
            if (SelectedPurchaseInvoice == null) return;

            var itemsToReturn = PurchaseDraftItems.Where(i => i.ReturnQuantity > 0).ToList();
            if (itemsToReturn.Count == 0)
            {
                MessageBox.Show("يرجى تحديد كمية إرجاع لصنف واحد على الأقل.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // فحص مبكر لكفاية المخزون
            foreach (var item in itemsToReturn)
            {
                if (item.CurrentStockQuantity < item.ReturnQuantity)
                {
                    MessageBox.Show(
                        $"الكمية المتوفرة بالمخزون من الصنف '{item.ProductName}' ({item.CurrentStockQuantity} قطعة) لا تكفي لإرجاع ({item.ReturnQuantity} قطعة) للمورد!\n" +
                        $"يرجى تعديل الكمية المرتجعة أو مراجعة حركة الصنف لمنع حدوث رصيد سالب.",
                        "رصيد المخزون غير كافٍ",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }

            var confirmResult = MessageBox.Show(
                $"هل أنت متأكد من اعتماد مرتجع المشتريات رقم '{PurchaseReturnNumber}' للمورد؟\n" +
                $"الفاتورة الأصلية: {SelectedPurchaseInvoice.InvoiceNumber}\n" +
                $"المورد: {SelectedPurchaseInvoice.SupplierName}\n" +
                $"إجمالي المرتجع: {PurchaseReturnTotalAmount:N2} ج.م\n" +
                $"عدد الأصناف: {itemsToReturn.Count} صنف\n" +
                "سيتم خصم الكميات من المخزون بحركة 'مرتجع شراء صادر'.",
                "تأكيد اعتماد مرتجع المشتريات",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes) return;

            try
            {
                var returnModel = new PurchaseReturnModel
                {
                    ReturnNumber = PurchaseReturnNumber,
                    PurchaseInvoiceId = SelectedPurchaseInvoice.Id,
                    ReturnDate = DateTime.Now,
                    Reason = PurchaseReturnReason,
                    Notes = PurchaseReturnNotes,
                    Status = "معتمدة",
                    Items = itemsToReturn.Select(i => new PurchaseReturnItemModel
                    {
                        PurchaseInvoiceItemId = i.PurchaseInvoiceItemId,
                        ProductId = i.ProductId,
                        ProductName = i.ProductName,
                        Quantity = i.ReturnQuantity,
                        UnitPrice = i.UnitPrice,
                        TotalPrice = i.LineTotal
                    }).ToList()
                };

                await _returnsService.ProcessPurchaseReturnAsync(returnModel);

                DataEvents.RaiseInventoryOrInvoicesChanged();

                MessageBox.Show(
                    $"تم اعتماد مرتجع المشتريات رقم '{returnModel.ReturnNumber}' بنجاح!\n" +
                    $"تم خصم الكميات من المخزون وحفظ الحركة بأمان.",
                    "نجاح العملية",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                ClearPurchaseReturnForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل اعتماد مرتجع المشتريات:\n{ex.Message}", "خطأ في المعالجة", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
