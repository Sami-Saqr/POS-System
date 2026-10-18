using System.Collections.ObjectModel;
using System.Windows;
using CarPartManager.Models;
using CarPartManager.MVVM;
using CarPartManager.Services;

namespace CarPartManager.ViewModels
{
    /// <summary>
    /// نموذج عرض شاشة فواتير المشتريات وإدارة حركات التوريد للمخزون
    /// </summary>
    public class PurchaseInvoicesViewModel : ViewModelBase
    {
        private readonly IPurchaseInvoiceService _invoiceService;
        private readonly IProductService _productService;
        private readonly ISupplierService _supplierService;

        private readonly List<ProductModel> _allProducts = new();
        private readonly List<PurchaseInvoiceModel> _allInvoices = new();

        // قوائم البيانات المعروضة
        public ObservableCollection<SupplierModel> SuppliersList { get; } = new();
        public ObservableCollection<ProductModel> FilteredAvailableProducts { get; } = new();
        public ObservableCollection<PurchaseInvoiceItemModel> CurrentInvoiceItems { get; } = new();
        public ObservableCollection<PurchaseInvoiceModel> InvoicesHistory { get; } = new();

        // 1. بيانات ترويسة الفاتورة النشطة
        private int _currentInvoiceId;
        public int CurrentInvoiceId
        {
            get => _currentInvoiceId;
            set => SetProperty(ref _currentInvoiceId, value);
        }

        private string _invoiceNumber = string.Empty;
        public string InvoiceNumber
        {
            get => _invoiceNumber;
            set => SetProperty(ref _invoiceNumber, value);
        }

        private DateTime _invoiceDate = DateTime.Now;
        public DateTime InvoiceDate
        {
            get => _invoiceDate;
            set => SetProperty(ref _invoiceDate, value);
        }

        private SupplierModel? _selectedSupplier;
        public SupplierModel? SelectedSupplier
        {
            get => _selectedSupplier;
            set => SetProperty(ref _selectedSupplier, value);
        }

        private string _status = "مسودة";
        public string Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    OnPropertyChanged(nameof(IsDraft));
                    OnPropertyChanged(nameof(IsPosted));
                    OnPropertyChanged(nameof(IsCancelled));
                    OnPropertyChanged(nameof(CanEditInvoice));
                    OnPropertyChanged(nameof(CanCancelInvoice));
                    OnPropertyChanged(nameof(StatusColor));
                    CancelInvoiceCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool IsDraft => Status == "مسودة";
        public bool IsPosted => Status == "معتمدة";
        public bool IsCancelled => Status == "ملغاة";
        public bool CanEditInvoice => !IsPosted && !IsCancelled;
        public bool CanCancelInvoice => IsPosted && !IsCancelled && CurrentInvoiceId > 0;
        public string StatusColor => IsPosted ? "#059669" : (IsCancelled ? "#DC2626" : "#D97706");

        private decimal _totalInvoiceAmount;
        public decimal TotalInvoiceAmount
        {
            get => _totalInvoiceAmount;
            set => SetProperty(ref _totalInvoiceAmount, value);
        }

        private string _notes = string.Empty;
        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // 2. مدخلات إضافة صنف جديد للفاتورة
        private ProductModel? _selectedProductToAdd;
        public ProductModel? SelectedProductToAdd
        {
            get => _selectedProductToAdd;
            set
            {
                if (SetProperty(ref _selectedProductToAdd, value) && value != null)
                {
                    // ملء سعر الشراء الافتراضي المسجل في كارت الصنف تلقائياً
                    InputUnitPrice = value.PurchasePrice;
                }
            }
        }

        private int _inputQuantity = 1;
        public int InputQuantity
        {
            get => _inputQuantity;
            set => SetProperty(ref _inputQuantity, value);
        }

        private decimal _inputUnitPrice;
        public decimal InputUnitPrice
        {
            get => _inputUnitPrice;
            set => SetProperty(ref _inputUnitPrice, value);
        }

        private string _productSearchText = string.Empty;
        public string ProductSearchText
        {
            get => _productSearchText;
            set
            {
                if (SetProperty(ref _productSearchText, value))
                {
                    FilterProductsDropdown();
                }
            }
        }

        // 3. سجل الفواتير السابقة والبحث
        private string _searchHistoryText = string.Empty;
        public string SearchHistoryText
        {
            get => _searchHistoryText;
            set
            {
                if (SetProperty(ref _searchHistoryText, value))
                {
                    FilterInvoicesHistory();
                }
            }
        }

        private PurchaseInvoiceModel? _selectedHistoryInvoice;
        public PurchaseInvoiceModel? SelectedHistoryInvoice
        {
            get => _selectedHistoryInvoice;
            set
            {
                if (SetProperty(ref _selectedHistoryInvoice, value) && value != null)
                {
                    LoadInvoiceDetailsIntoForm(value);
                }
            }
        }

        // الأوامر (Commands)
        public RelayCommand AddItemCommand { get; }
        public RelayCommand RemoveItemCommand { get; }
        public RelayCommand SaveDraftCommand { get; }
        public RelayCommand PostInvoiceCommand { get; }
        public RelayCommand CancelInvoiceCommand { get; }
        public RelayCommand NewInvoiceCommand { get; }
        public RelayCommand LoadInvoiceCommand { get; }
        public RelayCommand RefreshCommand { get; }

        public PurchaseInvoicesViewModel(
            IPurchaseInvoiceService? invoiceService = null,
            IProductService? productService = null,
            ISupplierService? supplierService = null)
        {
            _invoiceService = invoiceService ?? new SqlitePurchaseInvoiceService();
            _productService = productService ?? new SqliteProductService();
            _supplierService = supplierService ?? new SqliteSupplierService();

            AddItemCommand = new RelayCommand(ExecuteAddItem, () => CanEditInvoice);
            RemoveItemCommand = new RelayCommand(ExecuteRemoveItem, () => CanEditInvoice);
            SaveDraftCommand = new RelayCommand(async () => await ExecuteSaveDraftAsync(), () => CanEditInvoice);
            PostInvoiceCommand = new RelayCommand(async () => await ExecutePostInvoiceAsync(), () => CanEditInvoice);
            CancelInvoiceCommand = new RelayCommand(async () => await ExecuteCancelInvoiceAsync(), () => CanCancelInvoice);
            NewInvoiceCommand = new RelayCommand(async () => await ExecuteNewInvoiceAsync());
            LoadInvoiceCommand = new RelayCommand(p =>
            {
                if (p is PurchaseInvoiceModel inv) SelectedHistoryInvoice = inv;
            });
            RefreshCommand = new RelayCommand(async () => await LoadAllDataAsync());

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadAllDataAsync();
            await PrepareNewInvoiceAsync();
        }

        public async Task LoadAllDataAsync()
        {
            try
            {
                // تحميل الموردين
                var suppliers = await _supplierService.GetAllSuppliersAsync();
                SuppliersList.Clear();
                foreach (var s in suppliers) SuppliersList.Add(s);

                // تحميل الأصناف المتاحة
                var products = await _productService.GetAllProductsAsync();
                _allProducts.Clear();
                _allProducts.AddRange(products);
                FilterProductsDropdown();

                // تحميل أرشيف الفواتير
                var invoices = await _invoiceService.GetAllInvoicesAsync();
                _allInvoices.Clear();
                _allInvoices.AddRange(invoices);
                FilterInvoicesHistory();
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ في تحميل البيانات: {ex.Message}";
            }
        }

        private void FilterProductsDropdown()
        {
            FilteredAvailableProducts.Clear();
            if (string.IsNullOrWhiteSpace(ProductSearchText))
            {
                foreach (var p in _allProducts) FilteredAvailableProducts.Add(p);
            }
            else
            {
                var q = ProductSearchText.Trim().ToLower();
                var matches = _allProducts.Where(p =>
                    p.Name.ToLower().Contains(q) ||
                    p.PartNumber.ToLower().Contains(q) ||
                    p.Barcode.ToLower().Contains(q)
                );
                foreach (var p in matches) FilteredAvailableProducts.Add(p);
            }
        }

        private void FilterInvoicesHistory()
        {
            InvoicesHistory.Clear();
            if (string.IsNullOrWhiteSpace(SearchHistoryText))
            {
                foreach (var inv in _allInvoices) InvoicesHistory.Add(inv);
            }
            else
            {
                var q = SearchHistoryText.Trim().ToLower();
                var matches = _allInvoices.Where(inv =>
                    inv.InvoiceNumber.ToLower().Contains(q) ||
                    (inv.SupplierName != null && inv.SupplierName.ToLower().Contains(q)) ||
                    (inv.Notes != null && inv.Notes.ToLower().Contains(q))
                );
                foreach (var inv in matches) InvoicesHistory.Add(inv);
            }
        }

        private async Task PrepareNewInvoiceAsync()
        {
            CurrentInvoiceId = 0;
            InvoiceNumber = await _invoiceService.GenerateNextInvoiceNumberAsync();
            InvoiceDate = DateTime.Now;
            SelectedSupplier = SuppliersList.FirstOrDefault();
            Status = "مسودة";
            Notes = string.Empty;
            CurrentInvoiceItems.Clear();
            SelectedProductToAdd = FilteredAvailableProducts.FirstOrDefault();
            InputQuantity = 1;
            InputUnitPrice = SelectedProductToAdd?.PurchasePrice ?? 0m;
            RecalculateTotal();
            StatusMessage = "فاتورة مشتريات جديدة جاهزة للإدخال.";
        }

        private async Task ExecuteNewInvoiceAsync()
        {
            await PrepareNewInvoiceAsync();
        }

        private void ExecuteAddItem()
        {
            if (SelectedProductToAdd == null)
            {
                MessageBox.Show("يرجى اختيار صنف لإضافته إلى الفاتورة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (InputQuantity <= 0)
            {
                MessageBox.Show("الكمية يجب أن تكون أكبر من الصفر.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (InputUnitPrice < 0)
            {
                MessageBox.Show("سعر الشراء لا يمكن أن يكون سالباً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // فحص هل الصنف موجود مسبقاً في الفاتورة لدمجه
            var existingItem = CurrentInvoiceItems.FirstOrDefault(i => i.ProductId == SelectedProductToAdd.Id);
            if (existingItem != null)
            {
                existingItem.Quantity += InputQuantity;
                existingItem.UnitPrice = InputUnitPrice;
            }
            else
            {
                var lineItem = new PurchaseInvoiceItemModel(SelectedProductToAdd, InputQuantity, InputUnitPrice);
                CurrentInvoiceItems.Add(lineItem);
            }

            RecalculateTotal();
            StatusMessage = $"تمت إضافة '{SelectedProductToAdd.Name}' إلى بنود الفاتورة.";
        }

        private void ExecuteRemoveItem(object? parameter)
        {
            if (parameter is PurchaseInvoiceItemModel item)
            {
                CurrentInvoiceItems.Remove(item);
                RecalculateTotal();
                StatusMessage = $"تم حذف البند '{item.ProductName}'.";
            }
        }

        public void RecalculateTotal()
        {
            TotalInvoiceAmount = CurrentInvoiceItems.Sum(i => i.TotalPrice);
        }

        /// <summary>
        /// حفظ الفاتورة كمسودة في قاعدة البيانات دون التأثير على المخزون
        /// </summary>
        private async Task ExecuteSaveDraftAsync()
        {
            if (CurrentInvoiceItems.Count == 0)
            {
                MessageBox.Show("لا يمكن حفظ فاتورة خالية من الأصناف.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedSupplier == null)
            {
                MessageBox.Show("يرجى اختيار اسم المورد أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var invoice = new PurchaseInvoiceModel
                {
                    Id = CurrentInvoiceId,
                    InvoiceNumber = InvoiceNumber,
                    InvoiceDate = InvoiceDate,
                    SupplierId = SelectedSupplier.Id,
                    SupplierName = SelectedSupplier.Name,
                    Status = "مسودة",
                    TotalAmount = TotalInvoiceAmount,
                    Notes = Notes,
                    Items = CurrentInvoiceItems.ToList()
                };

                var saved = await _invoiceService.SaveDraftInvoiceAsync(invoice);
                CurrentInvoiceId = saved.Id;
                Status = "مسودة";
                StatusMessage = $"تم حفظ المسودة '{saved.InvoiceNumber}' بنجاح في قاعدة البيانات.";

                // تحديث الأرشيف وإشعار النظام
                await LoadAllDataAsync();
                DataEvents.RaiseInventoryOrInvoicesChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء حفظ المسودة:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// اعتماد الفاتورة وتحديث أرصدة المخزون تلقائياً داخل معاملة آمنة
        /// </summary>
        private async Task ExecutePostInvoiceAsync()
        {
            if (IsPosted)
            {
                MessageBox.Show("هذه الفاتورة معتمدة بالفعل مسبقاً!", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (CurrentInvoiceItems.Count == 0)
            {
                MessageBox.Show("لا يمكن اعتماد فاتورة خالية من الأصناف.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedSupplier == null)
            {
                MessageBox.Show("يرجى اختيار اسم المورد أولاً.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"هل أنت متأكد من رغبتك في اعتماد الفاتورة رقم ({InvoiceNumber}) بقيمة {TotalInvoiceAmount:N2} ج.م؟\n\nتنبيه: سيتم ترحيل الكميات فوراً وزيادة رصيد المستودع لجميع بنود الفاتورة.",
                "تأكيد اعتماد وترحيل الفاتورة",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var invoice = new PurchaseInvoiceModel
                {
                    Id = CurrentInvoiceId,
                    InvoiceNumber = InvoiceNumber,
                    InvoiceDate = InvoiceDate,
                    SupplierId = SelectedSupplier.Id,
                    SupplierName = SelectedSupplier.Name,
                    Status = "مسودة",
                    TotalAmount = TotalInvoiceAmount,
                    Notes = Notes,
                    Items = CurrentInvoiceItems.ToList()
                };

                var posted = await _invoiceService.PostInvoiceAsync(invoice);
                CurrentInvoiceId = posted.Id;
                Status = "معتمدة";
                StatusMessage = $"تم اعتماد الفاتورة '{posted.InvoiceNumber}' بنجاح وزيادة كميات المخزون في المستودع.";

                MessageBox.Show(
                    $"تم اعتماد فاتورة المشتريات رقم ({posted.InvoiceNumber}) بنجاح!\nتم تحديث أرصدة قطع الغيار في المخزن تلقائياً.",
                    "تم الاعتماد بنجاح",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // إعادة تحميل الأرشيف والأصناف المحدثة وإشعار لوحة التحكم
                await LoadAllDataAsync();
                DataEvents.RaiseInventoryOrInvoicesChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل اعتماد الفاتورة وتحديث المخزون:\n{ex.Message}", "خطأ في المعاملة", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// إلغاء فاتورة المشتريات المعتمدة وعكس كميات المخزون بحركة 'إلغاء شراء صادر'
        /// </summary>
        private async Task ExecuteCancelInvoiceAsync()
        {
            if (CurrentInvoiceId <= 0 || !CanCancelInvoice) return;

            var confirm = MessageBox.Show(
                $"هل أنت متأكد من رغبتك في إلغاء فاتورة المشتريات المعتمدة رقم ({InvoiceNumber})؟\n\n" +
                "تنبيهات الأمان والمخزون:\n" +
                "• لن يتم حذف الفاتورة بل ستتغير حالتها إلى (ملغاة) حفظاً للسجلات التاريخية.\n" +
                "• سيتم خصم الكميات من المستودع بحركة عكسية 'إلغاء شراء صادر'.\n" +
                "• سيتم رفض الإلغاء إذا كانت الكمية المتاحة في المخزون حالياً غير كافية لمنع حدوث رصيد سالب.",
                "تأكيد إلغاء فاتورة المشتريات",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                string reason = "إلغاء بناءً على طلب المستخدم";
                await _invoiceService.CancelInvoiceAsync(CurrentInvoiceId, reason);

                Status = "ملغاة";
                StatusMessage = $"تم إلغاء فاتورة المشتريات رقم '{InvoiceNumber}' بنجاح، وتم عكس حركات المخزون.";

                MessageBox.Show(
                    $"تم إلغاء فاتورة المشتريات رقم ({InvoiceNumber}) بنجاح!\nتم خصم الكميات من رصيد المستودع وتسجيل الحركات المعكوسة بدقة.",
                    "تم الإلغاء بنجاح",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await LoadAllDataAsync();
                DataEvents.RaiseInventoryOrInvoicesChanged();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"تعذر إلغاء فاتورة المشتريات:\n{ex.Message}", "رفض عملية الإلغاء", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadInvoiceDetailsIntoForm(PurchaseInvoiceModel invoice)
        {
            CurrentInvoiceId = invoice.Id;
            InvoiceNumber = invoice.InvoiceNumber;
            InvoiceDate = invoice.InvoiceDate;
            SelectedSupplier = SuppliersList.FirstOrDefault(s => s.Id == invoice.SupplierId || s.Name == invoice.SupplierName);
            Status = invoice.Status;
            Notes = invoice.Notes ?? string.Empty;

            CurrentInvoiceItems.Clear();
            if (invoice.Items != null)
            {
                foreach (var i in invoice.Items) CurrentInvoiceItems.Add(i);
            }

            RecalculateTotal();
            StatusMessage = $"تم فتح الفاتورة '{invoice.InvoiceNumber}' ({Status}).";
        }
    }
}
