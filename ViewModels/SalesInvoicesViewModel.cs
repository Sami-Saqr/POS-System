using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using CarPartManager.Models;
using CarPartManager.MVVM;
using CarPartManager.Services;

namespace CarPartManager.ViewModels
{
    /// <summary>
    /// نموذج عرض شاشة فواتير المبيعات ونقاط البيع وإدارة حركات الصرف والخصم من المخزون
    /// </summary>
    public class SalesInvoicesViewModel : ViewModelBase
    {
        private readonly ISalesInvoiceService _invoiceService;
        private readonly IProductService _productService;
        private readonly ICustomerService _customerService;
        private readonly Task _initializationTask;

        private readonly List<ProductModel> _allProducts = new();
        private readonly List<SalesInvoiceModel> _allInvoices = new();

        // القوائم المعروضة
        public ObservableCollection<ProductModel> FilteredAvailableProducts { get; } = new();
        public ObservableCollection<SalesInvoiceItemModel> CurrentInvoiceItems { get; } = new();
        public ObservableCollection<SalesInvoiceModel> InvoicesHistory { get; } = new();
        public ObservableCollection<ProductModel> BarcodeSuggestions { get; } = new();
        public ObservableCollection<CustomerModel> CustomerSuggestions { get; } = new();
        public ObservableCollection<string> PaymentMethodsList { get; } = new() { "نقدي", "شبكة / فيزا", "آجل" };

        public static bool IsHeadlessTestMode { get; set; }

        private static MessageBoxResult ShowMessageBox(string message, string title, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Warning)
        {
            if (IsHeadlessTestMode)
            {
                return button == MessageBoxButton.YesNo ? MessageBoxResult.Yes : MessageBoxResult.OK;
            }
            return MessageBox.Show(message, title, button, image);
        }

        private bool _isInitialized;
        public bool IsInitialized
        {
            get => _isInitialized;
            private set
            {
                // يجب تحديث الخاصية وإشعار CanExecute من UI Thread
                // لأن RaiseCanExecuteChanged يستدعي CommandManager.InvalidateRequerySuggested
                // الذي يشترط أن يعمل على UI thread ليُحدَّث حالة الأزرار فعلياً
                if (_isInitialized == value) return;
                _isInitialized = value;
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    OnPropertyChanged();
                    AddItemCommand?.RaiseCanExecuteChanged();
                    AddItemByBarcodeCommand?.RaiseCanExecuteChanged();
                    SelectBarcodeSuggestionCommand?.RaiseCanExecuteChanged();
                    NewInvoiceCommand?.RaiseCanExecuteChanged();
                    ClearInvoiceCommand?.RaiseCanExecuteChanged();
                });
            }
        }

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

        private int? _selectedCustomerId;
        public int? SelectedCustomerId
        {
            get => _selectedCustomerId;
            set
            {
                if (SetProperty(ref _selectedCustomerId, value))
                {
                    OnPropertyChanged(nameof(HasSelectedCustomer));
                }
            }
        }

        public bool HasSelectedCustomer => SelectedCustomerId.HasValue && SelectedCustomerId.Value > 0;

        private string _customerName = "عميل نقدي";
        public string CustomerName
        {
            get => _customerName;
            set
            {
                if (SetProperty(ref _customerName, value))
                {
                    if (!_suppressCustomerSuggestions)
                    {
                        if (SelectedCustomerId.HasValue && value != _selectedCustomerSuggestion?.Name)
                        {
                            SelectedCustomerId = null;
                        }
                        OnCustomerInputChanged(value);
                    }
                }
            }
        }

        private string _customerPhone = string.Empty;
        public string CustomerPhone
        {
            get => _customerPhone;
            set
            {
                if (SetProperty(ref _customerPhone, value))
                {
                    if (!_suppressCustomerSuggestions)
                    {
                        OnCustomerInputChanged(value);
                    }
                }
            }
        }

        private string _customerAddress = string.Empty;
        public string CustomerAddress
        {
            get => _customerAddress;
            set => SetProperty(ref _customerAddress, value);
        }

        private CustomerModel? _selectedCustomerSuggestion;
        public CustomerModel? SelectedCustomerSuggestion
        {
            get => _selectedCustomerSuggestion;
            set => SetProperty(ref _selectedCustomerSuggestion, value);
        }

        private bool _isCustomerPopupOpen;
        public bool IsCustomerPopupOpen
        {
            get => _isCustomerPopupOpen;
            set
            {
                if (SetProperty(ref _isCustomerPopupOpen, value))
                {
                    OnPropertyChanged(nameof(ShowNoCustomerSuggestions));
                }
            }
        }

        private bool _isLoadingCustomerSuggestions;
        public bool IsLoadingCustomerSuggestions
        {
            get => _isLoadingCustomerSuggestions;
            set
            {
                if (SetProperty(ref _isLoadingCustomerSuggestions, value))
                {
                    OnPropertyChanged(nameof(ShowNoCustomerSuggestions));
                }
            }
        }

        private string _customerSearchText = string.Empty;
        public bool ShowNoCustomerSuggestions =>
            IsCustomerPopupOpen &&
            !IsLoadingCustomerSuggestions &&
            CustomerSuggestions.Count == 0 &&
            !string.IsNullOrWhiteSpace(_customerSearchText) &&
            _customerSearchText.Trim().Length >= 2;

        private bool _suppressCustomerSuggestions;
        private CancellationTokenSource? _customerSearchCts;

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
                    AddItemCommand?.RaiseCanExecuteChanged();
                    AddItemByBarcodeCommand?.RaiseCanExecuteChanged();
                    SelectBarcodeSuggestionCommand?.RaiseCanExecuteChanged();
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

        private decimal _subTotal;
        public decimal SubTotal
        {
            get => _subTotal;
            set => SetProperty(ref _subTotal, value);
        }

        private decimal _discount;
        public decimal Discount
        {
            get => _discount;
            set
            {
                if (SetProperty(ref _discount, value))
                {
                    RecalculateTotals();
                }
            }
        }

        private decimal _grandTotal;
        public decimal GrandTotal
        {
            get => _grandTotal;
            set => SetProperty(ref _grandTotal, value);
        }

        private string _paymentMethod = "نقدي";
        public string PaymentMethod
        {
            get => _paymentMethod;
            set
            {
                if (SetProperty(ref _paymentMethod, value))
                {
                    if (value == "نقدي")
                    {
                        PaidAmount = GrandTotal;
                    }
                }
            }
        }

        private decimal _paidAmount;
        public decimal PaidAmount
        {
            get => _paidAmount;
            set
            {
                if (SetProperty(ref _paidAmount, value))
                {
                    OnPropertyChanged(nameof(RemainingAmount));
                }
            }
        }

        public decimal RemainingAmount => Math.Max(0, GrandTotal - PaidAmount);

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
                    InputUnitPrice = value.SellingPrice > 0 ? value.SellingPrice : value.PurchasePrice;
                    OnPropertyChanged(nameof(AvailableStockText));
                }
            }
        }

        public string AvailableStockText => SelectedProductToAdd != null
            ? $"الرصيد المتوفر بالمستودع: {SelectedProductToAdd.Quantity} قطعة"
            : "اختر صنفاً لعرض الرصيد المتاح";

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

        // مدخل الباركود المسحي السريع والاقتراحات التفاعلية الحية
        private string _barcodeInput = string.Empty;
        public string BarcodeInput
        {
            get => _barcodeInput;
            set
            {
                if (SetProperty(ref _barcodeInput, value))
                {
                    if (!_suppressBarcodeSuggestions)
                    {
                        OnBarcodeInputChanged(value);
                    }
                }
            }
        }

        private bool _suppressBarcodeSuggestions;
        private CancellationTokenSource? _barcodeSearchCts;

        private ProductModel? _selectedBarcodeSuggestion;
        public ProductModel? SelectedBarcodeSuggestion
        {
            get => _selectedBarcodeSuggestion;
            set => SetProperty(ref _selectedBarcodeSuggestion, value);
        }

        private bool _isBarcodePopupOpen;
        public bool IsBarcodePopupOpen
        {
            get => _isBarcodePopupOpen;
            set
            {
                if (SetProperty(ref _isBarcodePopupOpen, value))
                {
                    OnPropertyChanged(nameof(ShowNoBarcodeSuggestions));
                }
            }
        }

        private bool _isLoadingBarcodeSuggestions;
        public bool IsLoadingBarcodeSuggestions
        {
            get => _isLoadingBarcodeSuggestions;
            set
            {
                if (SetProperty(ref _isLoadingBarcodeSuggestions, value))
                {
                    OnPropertyChanged(nameof(ShowNoBarcodeSuggestions));
                }
            }
        }

        public bool ShowNoBarcodeSuggestions =>
            IsBarcodePopupOpen &&
            !IsLoadingBarcodeSuggestions &&
            BarcodeSuggestions.Count == 0 &&
            !string.IsNullOrWhiteSpace(BarcodeInput) &&
            BarcodeInput.Trim().Length >= 2;

        public Action? FocusBarcodeInput { get; set; }

        // 3. سجل وبحث الفواتير السابقة
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

        private SalesInvoiceModel? _selectedHistoryInvoice;
        public SalesInvoiceModel? SelectedHistoryInvoice
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
        public RelayCommand AddItemByBarcodeCommand { get; }
        public RelayCommand SelectBarcodeSuggestionCommand { get; }
        public RelayCommand SelectCustomerSuggestionCommand { get; }
        public RelayCommand ClearSelectedCustomerCommand { get; }
        public RelayCommand LoadInvoiceCommand { get; }
        public RelayCommand RemoveItemCommand { get; }
        public RelayCommand SaveDraftCommand { get; }
        public RelayCommand PostInvoiceCommand { get; }
        public RelayCommand CancelInvoiceCommand { get; }
        public RelayCommand NewInvoiceCommand { get; }
        public RelayCommand ClearInvoiceCommand { get; }
        public RelayCommand RefreshCommand { get; }

        public SalesInvoicesViewModel(
            ISalesInvoiceService? invoiceService = null,
            IProductService? productService = null,
            ICustomerService? customerService = null)
        {
            _invoiceService = invoiceService ?? new SqliteSalesInvoiceService();
            _productService = productService ?? new SqliteProductService();
            _customerService = customerService ?? new SqliteCustomerService();

            // CanEditInvoice (مسودة غير معتمدة ولا ملغاة) يكفي حراسةً وظيفية كاملة.
            // حذف IsInitialized من الشرط لأن تأخر إشعار UI thread كان يجعل الأزرار معطلة
            // حتى بعد اكتمال التهيئة فعلياً بسبب race condition بين async init وUI dispatcher.
            AddItemCommand = new RelayCommand(ExecuteAddItem, () => CanEditInvoice);
            AddItemByBarcodeCommand = new RelayCommand(async () => await ExecuteAddItemByBarcodeAsync(), () => CanEditInvoice);
            SelectBarcodeSuggestionCommand = new RelayCommand(p =>
            {
                if (p is ProductModel product)
                {
                    SelectBarcodeSuggestion(product);
                }
            }, () => CanEditInvoice);
            SelectCustomerSuggestionCommand = new RelayCommand(p =>
            {
                if (p is CustomerModel customer)
                {
                    SelectCustomerSuggestion(customer);
                }
            }, () => CanEditInvoice);
            ClearSelectedCustomerCommand = new RelayCommand(() =>
            {
                SelectedCustomerId = null;
                SelectedCustomerSuggestion = null;
                CustomerName = "عميل نقدي";
                CustomerPhone = string.Empty;
                CustomerAddress = string.Empty;
                CloseCustomerSuggestions();
            }, () => CanEditInvoice);
            LoadInvoiceCommand = new RelayCommand(p =>
            {
                if (p is SalesInvoiceModel inv) SelectedHistoryInvoice = inv;
            });
            RemoveItemCommand = new RelayCommand(ExecuteRemoveItem, () => CanEditInvoice);
            SaveDraftCommand = new RelayCommand(async () => await ExecuteSaveDraftAsync(), () => CanEditInvoice);
            PostInvoiceCommand = new RelayCommand(async () => await ExecutePostInvoiceAsync(), () => CanEditInvoice);
            CancelInvoiceCommand = new RelayCommand(async () => await ExecuteCancelInvoiceAsync(), () => CanCancelInvoice);
            NewInvoiceCommand = new RelayCommand(async () => await ExecuteClearOrNewInvoiceAsync(), () => IsInitialized);
            ClearInvoiceCommand = new RelayCommand(async () => await ExecuteClearOrNewInvoiceAsync(), () => IsInitialized);
            RefreshCommand = new RelayCommand(async () => await LoadAllDataAsync());

            CurrentInvoiceItems.CollectionChanged += OnCurrentInvoiceItemsCollectionChanged;

            _initializationTask = InitializeAsync();
        }

        private void OnCurrentInvoiceItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (SalesInvoiceItemModel item in e.OldItems)
                {
                    item.PropertyChanged -= OnInvoiceItemPropertyChanged;
                }
            }
            if (e.NewItems != null)
            {
                foreach (SalesInvoiceItemModel item in e.NewItems)
                {
                    item.PropertyChanged += OnInvoiceItemPropertyChanged;
                }
            }
            RecalculateTotals();
        }

        private void OnInvoiceItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SalesInvoiceItemModel.Quantity) ||
                e.PropertyName == nameof(SalesInvoiceItemModel.UnitPrice) ||
                e.PropertyName == nameof(SalesInvoiceItemModel.TotalPrice))
            {
                RecalculateTotals();
            }
        }

        private async Task InitializeAsync()
        {
            try
            {
                await LoadAllDataAsync();
                await PrepareNewInvoiceAsync();
                // IsInitialized setter يستخدم Dispatcher.InvokeAsync داخلياً،
                // لكن نضمن هنا إضافياً أن الضبط النهائي يصل للـ UI thread
                IsInitialized = true;
            }
            catch (Exception ex)
            {
                var msg = $"تعذر تجهيز فاتورة البيع الجديدة: {ex.Message}";
                System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    StatusMessage = msg;
                    ShowMessageBox(msg, "خطأ في تهيئة نقطة البيع", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        public async Task LoadAllDataAsync()
        {
            try
            {
                // تحميل الأصناف المتاحة
                var products = await _productService.GetAllProductsAsync();
                _allProducts.Clear();
                _allProducts.AddRange(products);
                FilterProductsDropdown();

                // تحميل أرشيف فواتير المبيعات
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
                    p.Barcode.ToLower().Contains(q) ||
                    (p.CompatibleVehicles != null && p.CompatibleVehicles.ToLower().Contains(q))
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
                    (inv.CustomerName != null && inv.CustomerName.ToLower().Contains(q)) ||
                    (inv.CustomerPhone != null && inv.CustomerPhone.ToLower().Contains(q)) ||
                    (inv.Notes != null && inv.Notes.ToLower().Contains(q))
                );
                foreach (var inv in matches) InvoicesHistory.Add(inv);
            }
        }

        public async Task PrepareNewInvoiceAsync()
        {
            CurrentInvoiceId = 0;
            InvoiceNumber = await _invoiceService.GenerateNextInvoiceNumberAsync();
            InvoiceDate = DateTime.Now;
            SelectedCustomerId = null;
            SelectedCustomerSuggestion = null;
            CustomerName = "عميل نقدي";
            CustomerPhone = string.Empty;
            CustomerAddress = string.Empty;
            CloseCustomerSuggestions();
            Status = "مسودة";
            PaymentMethod = "نقدي";
            Discount = 0m;
            Notes = string.Empty;
            CurrentInvoiceItems.Clear();
            ProductSearchText = string.Empty;
            FilterProductsDropdown();
            SelectedProductToAdd = FilteredAvailableProducts.FirstOrDefault();
            InputQuantity = 1;
            InputUnitPrice = SelectedProductToAdd?.SellingPrice ?? 0m;
            _selectedHistoryInvoice = null;
            OnPropertyChanged(nameof(SelectedHistoryInvoice));
            BarcodeInput = string.Empty;
            FocusBarcodeInput?.Invoke();
            RecalculateTotals();
            StatusMessage = "فاتورة بيع جديدة جاهزة للإدخال.";
        }

        /// <summary>
        /// مسح بيانات الفاتورة الحالية وتجهيز فاتورة جديدة فارغة بعد التأكيد إذا كانت هناك بيانات غير محفوظة
        /// </summary>
        public async Task ExecuteClearOrNewInvoiceAsync()
        {
            if (CurrentInvoiceItems.Count > 0)
            {
                string confirmTitle;
                string confirmPrompt;

                if (CurrentInvoiceId > 0)
                {
                    confirmTitle = "بدء فاتورة جديدة";
                    confirmPrompt = $"الفاتورة الحالية مسجلة في النظام برقم ({InvoiceNumber}).\n\n" +
                                    "هل تريد إغلاقها والبدء في فاتورة بيع جديدة فارغة؟\n" +
                                    "(تنبيه: لن يتم حذف الفاتورة المسجلة من قاعدة البيانات وسيمكنك دائماً فتحها من الأرشيف).";
                }
                else
                {
                    confirmTitle = "تأكيد مسح الفاتورة";
                    confirmPrompt = $"تحتوي الفاتورة الحالية على ({CurrentInvoiceItems.Count}) صنف غير محفوظ.\n\n" +
                                    "هل أنت متأكد من رغبتك في مسح جميع بنود الفاتورة وتصفير الحقول للبدء من جديد؟";
                }

                var confirm = ShowMessageBox(confirmPrompt, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm != MessageBoxResult.Yes) return;
            }

            await PrepareNewInvoiceAsync();
            StatusMessage = "تم مسح الفاتورة وتجهيز نموذج بيع جديد.";
        }

        private async Task ExecuteNewInvoiceAsync()
        {
            await ExecuteClearOrNewInvoiceAsync();
        }

        /// <summary>
        /// معالجة تغير نص حقل الباركود لإطلاق البحث التفاعلي الحي مع Debounce وإلغاء الاستعلامات السابقة
        /// </summary>
        private void OnBarcodeInputChanged(string? text)
        {
            _barcodeSearchCts?.Cancel();
            _barcodeSearchCts?.Dispose();
            _barcodeSearchCts = null;

            if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 2)
            {
                BarcodeSuggestions.Clear();
                SelectedBarcodeSuggestion = null;
                IsBarcodePopupOpen = false;
                IsLoadingBarcodeSuggestions = false;
                OnPropertyChanged(nameof(ShowNoBarcodeSuggestions));
                return;
            }

            var clean = text.Trim();
            _barcodeSearchCts = new CancellationTokenSource();
            var token = _barcodeSearchCts.Token;

            _ = TriggerBarcodeSearchDebouncedAsync(clean, token);
        }

        private async Task TriggerBarcodeSearchDebouncedAsync(string searchText, CancellationToken token)
        {
            try
            {
                // فترة تأخير Debounce لمنع الضغط على قاعدة البيانات أثناء الكتابة السريعة (200 ملي ثانية)
                await Task.Delay(200, token);
                if (token.IsCancellationRequested) return;

                IsLoadingBarcodeSuggestions = true;
                IsBarcodePopupOpen = true;
                OnPropertyChanged(nameof(ShowNoBarcodeSuggestions));

                var results = await _productService.GetBarcodeSuggestionsAsync(searchText, token);
                if (token.IsCancellationRequested) return;

                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    await dispatcher.InvokeAsync(() =>
                    {
                        if (token.IsCancellationRequested) return;

                        BarcodeSuggestions.Clear();
                        foreach (var p in results)
                        {
                            BarcodeSuggestions.Add(p);
                        }

                        SelectedBarcodeSuggestion = null; // لا يتم اختيار أي صنف افتراضياً لضمان توافق قارئ الباركود USB
                        IsLoadingBarcodeSuggestions = false;
                        IsBarcodePopupOpen = true;
                        OnPropertyChanged(nameof(ShowNoBarcodeSuggestions));
                    });
                }
                else
                {
                    if (token.IsCancellationRequested) return;

                    BarcodeSuggestions.Clear();
                    foreach (var p in results)
                    {
                        BarcodeSuggestions.Add(p);
                    }

                    SelectedBarcodeSuggestion = null;
                    IsLoadingBarcodeSuggestions = false;
                    IsBarcodePopupOpen = true;
                    OnPropertyChanged(nameof(ShowNoBarcodeSuggestions));
                }
            }
            catch (OperationCanceledException)
            {
                // إلغاء طبيعي عند طباعة حرف جديد
            }
            catch (Exception ex)
            {
                IsLoadingBarcodeSuggestions = false;
                OnPropertyChanged(nameof(ShowNoBarcodeSuggestions));
                StatusMessage = $"خطأ في البحث عن الباركود: {ex.Message}";
            }
        }

        public void SelectNextBarcodeSuggestion()
        {
            if (!IsBarcodePopupOpen || BarcodeSuggestions.Count == 0) return;

            if (SelectedBarcodeSuggestion == null)
            {
                SelectedBarcodeSuggestion = BarcodeSuggestions[0];
            }
            else
            {
                int idx = BarcodeSuggestions.IndexOf(SelectedBarcodeSuggestion);
                if (idx >= 0 && idx < BarcodeSuggestions.Count - 1)
                {
                    SelectedBarcodeSuggestion = BarcodeSuggestions[idx + 1];
                }
            }
        }

        public void SelectPreviousBarcodeSuggestion()
        {
            if (!IsBarcodePopupOpen || BarcodeSuggestions.Count == 0) return;

            if (SelectedBarcodeSuggestion == null) return;

            int idx = BarcodeSuggestions.IndexOf(SelectedBarcodeSuggestion);
            if (idx > 0)
            {
                SelectedBarcodeSuggestion = BarcodeSuggestions[idx - 1];
            }
            else if (idx == 0)
            {
                SelectedBarcodeSuggestion = null;
            }
        }

        public void CloseBarcodeSuggestions()
        {
            _barcodeSearchCts?.Cancel();
            _barcodeSearchCts?.Dispose();
            _barcodeSearchCts = null;

            IsBarcodePopupOpen = false;
            SelectedBarcodeSuggestion = null;
            BarcodeSuggestions.Clear();
            IsLoadingBarcodeSuggestions = false;
            OnPropertyChanged(nameof(ShowNoBarcodeSuggestions));
        }

        public void SelectBarcodeSuggestion(ProductModel? product)
        {
            if (product == null) return;

            // التحقق من المخزون: الأصناف ذات الرصيد الصفري تظهر ولكن لا يمكن إضافتها
            if (product.Quantity <= 0)
            {
                StatusMessage = $"تنبيه: الصنف '{product.Name}' نفد من المخزون (الرصيد: 0) ولا يمكن إضافته للبيع.";
                ShowMessageBox(
                    $"تنبيه نقص مخزون:\nالصنف '{product.Name}' غير متوفر حالياً بالمستودع (الرصيد: 0 قطعة).\nلا يمكن إضافة أصناف منتهية الرصيد إلى فاتورة البيع.",
                    "صنف غير متوفر",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                FocusBarcodeInput?.Invoke();
                return;
            }

            AddProductFromSuggestion(product);
        }

        private void AddProductFromSuggestion(ProductModel product)
        {
            decimal unitPrice = product.SellingPrice > 0 ? product.SellingPrice : product.PurchasePrice;
            int qtyToAdd = InputQuantity > 0 ? InputQuantity : 1;

            if (!TryAddProductToCurrentInvoice(product, qtyToAdd, unitPrice))
            {
                FocusBarcodeInput?.Invoke();
                return;
            }

            SelectedProductToAdd = product;
            InputUnitPrice = unitPrice;
            InputQuantity = 1;
            StatusMessage = $"تمت إضافة '{product.Name}' بنجاح عبر الباركود ({product.Barcode}).";

            CloseBarcodeSuggestions();

            _suppressBarcodeSuggestions = true;
            BarcodeInput = string.Empty;
            _suppressBarcodeSuggestions = false;

            FocusBarcodeInput?.Invoke();
        }

        /// <summary>
        /// البحث عن الصنف عبر الباركود المسحي وإضافته مباشرة إلى الفاتورة الحالية مع دعم قارئ الباركود ومفتاح Enter
        /// </summary>
        public async Task ExecuteAddItemByBarcodeAsync()
        {
            CloseBarcodeSuggestions();

            if (!CanEditInvoice)
            {
                ShowMessageBox("لا يمكن إضافة أصناف لفاتورة معتمدة أو ملغاة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(BarcodeInput))
            {
                FocusBarcodeInput?.Invoke();
                return;
            }

            var code = BarcodeInput.Trim();

            // 1. البحث الدقيق بالباركود كنص للحفاظ على الأصفار في البداية
            var product = _allProducts.FirstOrDefault(p => !string.IsNullOrEmpty(p.Barcode) && p.Barcode.Equals(code, StringComparison.OrdinalIgnoreCase))
                          ?? await _productService.GetByBarcodeAsync(code);

            if (product == null)
            {
                StatusMessage = "لا يوجد منتج بهذا الباركود. (لم يتم العثور على أي صنف مطابق)";
                ShowMessageBox("لا يوجد منتج بهذا الباركود.", "تنبيه الباركود", MessageBoxButton.OK, MessageBoxImage.Warning);
                _suppressBarcodeSuggestions = true;
                BarcodeInput = string.Empty;
                _suppressBarcodeSuggestions = false;
                FocusBarcodeInput?.Invoke();
                return;
            }

            decimal unitPrice = product.SellingPrice > 0 ? product.SellingPrice : product.PurchasePrice;
            int qtyToAdd = InputQuantity > 0 ? InputQuantity : 1;
            if (!TryAddProductToCurrentInvoice(product, qtyToAdd, unitPrice))
            {
                FocusBarcodeInput?.Invoke();
                return;
            }

            SelectedProductToAdd = product;
            InputUnitPrice = unitPrice;
            InputQuantity = 1;
            StatusMessage = $"تمت إضافة '{product.Name}' بنجاح عبر الباركود ({code}).";

            // 4. مسح حقل الباركود وإعادة التركيز إليه لتسهيل استمرار المسح بالكاشير
            _suppressBarcodeSuggestions = true;
            BarcodeInput = string.Empty;
            _suppressBarcodeSuggestions = false;
            FocusBarcodeInput?.Invoke();
        }

        private void ExecuteAddItem()
        {
            if (SelectedProductToAdd == null)
            {
                ShowMessageBox("يرجى اختيار قطعة غيار لإضافتها إلى الفاتورة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (TryAddProductToCurrentInvoice(SelectedProductToAdd, InputQuantity, InputUnitPrice))
            {
                StatusMessage = $"تمت إضافة '{SelectedProductToAdd.Name}' إلى الفاتورة.";
            }
        }

        private bool TryAddProductToCurrentInvoice(ProductModel product, int quantityToAdd, decimal unitPrice)
        {
            if (quantityToAdd <= 0)
            {
                StatusMessage = "الكمية المباعة يجب أن تكون أكبر من الصفر.";
                ShowMessageBox(StatusMessage, "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (unitPrice < 0)
            {
                StatusMessage = "سعر البيع لا يمكن أن يكون سالباً.";
                ShowMessageBox(StatusMessage, "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            var currentLineItem = CurrentInvoiceItems.FirstOrDefault(i => i.ProductId == product.Id);
            int totalRequestedQty = (currentLineItem?.Quantity ?? 0) + quantityToAdd;
            if (totalRequestedQty > product.Quantity)
            {
                StatusMessage = $"الكمية غير كافية للصنف ({product.Name}). المتاح: {product.Quantity}";
                ShowMessageBox(
                    $"تنبيه نقص مخزون:\nالكمية المتاحة حالياً في المستودع للصنف ({product.Name}) هي {product.Quantity} قطعة فقط.\nلا يمكنك طلب كمية إجمالية قدرها {totalRequestedQty} قطعة.",
                    "الكمية غير كافية",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (currentLineItem != null)
            {
                currentLineItem.Quantity += quantityToAdd;
                currentLineItem.UnitPrice = unitPrice;
                currentLineItem.AvailableQuantity = product.Quantity;
                if (string.IsNullOrEmpty(currentLineItem.Barcode))
                {
                    currentLineItem.Barcode = product.Barcode ?? string.Empty;
                }
            }
            else
            {
                CurrentInvoiceItems.Add(new SalesInvoiceItemModel(product, quantityToAdd, unitPrice));
            }

            RecalculateTotals();
            return true;
        }

        private void ExecuteRemoveItem(object? parameter)
        {
            if (parameter is SalesInvoiceItemModel item)
            {
                CurrentInvoiceItems.Remove(item);
                RecalculateTotals();
                StatusMessage = $"تم حذف البند '{item.ProductName}'.";
            }
        }

        public void RecalculateTotals()
        {
            SubTotal = CurrentInvoiceItems.Sum(i => i.TotalPrice);

            if (Discount < 0) Discount = 0m;
            if (Discount > SubTotal) Discount = SubTotal;

            GrandTotal = Math.Max(0, SubTotal - Discount);

            if (PaymentMethod == "نقدي")
            {
                PaidAmount = GrandTotal;
            }

            OnPropertyChanged(nameof(RemainingAmount));
        }

        /// <summary>
        /// حفظ الفاتورة كمسودة في قاعدة البيانات دون التأثير على المخزون
        /// </summary>
        private async Task ExecuteSaveDraftAsync()
        {
            if (CurrentInvoiceItems.Count == 0)
            {
                ShowMessageBox("لا يمكن حفظ فاتورة خالية من الأصناف.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var invoice = new SalesInvoiceModel
                {
                    Id = CurrentInvoiceId,
                    InvoiceNumber = InvoiceNumber,
                    InvoiceDate = InvoiceDate,
                    CustomerId = SelectedCustomerId,
                    CustomerName = string.IsNullOrWhiteSpace(CustomerName) ? "عميل نقدي" : CustomerName.Trim(),
                    CustomerPhone = CustomerPhone?.Trim() ?? string.Empty,
                    CustomerAddress = CustomerAddress?.Trim() ?? string.Empty,
                    Status = "مسودة",
                    SubTotal = SubTotal,
                    Discount = Discount,
                    GrandTotal = GrandTotal,
                    PaymentMethod = PaymentMethod,
                    PaidAmount = PaidAmount,
                    Notes = Notes,
                    Items = CurrentInvoiceItems.ToList()
                };

                var saved = await _invoiceService.SaveDraftInvoiceAsync(invoice);

                // تحديث الأرشيف وإشعار النظام
                await LoadAllDataAsync();

                // تجهيز فاتورة جديدة فارغة تلقائياً بعد نجاح العملية
                await PrepareNewInvoiceAsync();
                StatusMessage = $"تم حفظ المسودة '{saved.InvoiceNumber}' بنجاح في قاعدة البيانات، وتجهيز فاتورة جديدة.";
            }
            catch (Exception ex)
            {
                ShowMessageBox($"حدث خطأ أثناء حفظ المسودة:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// اعتماد فاتورة البيع والتحقق النهائي من المخزون وخصم الكميات ذرياً
        /// </summary>
        private async Task ExecutePostInvoiceAsync()
        {
            if (IsPosted)
            {
                ShowMessageBox("هذه الفاتورة معتمدة بالفعل مسبقاً!", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (CurrentInvoiceItems.Count == 0)
            {
                ShowMessageBox("لا يمكن اعتماد فاتورة خالية من الأصناف.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = ShowMessageBox(
                $"هل أنت متأكد من رغبتك في اعتماد الفاتورة رقم ({InvoiceNumber}) بقيمة صافية {GrandTotal:N2} ج.م؟\n\nتنبيه: سيتم خصم الكميات فوراً من رصيد المستودع وتوثيق حركة البيع.",
                "تأكيد اعتماد وترحيل فاتورة البيع",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var invoice = new SalesInvoiceModel
                {
                    Id = CurrentInvoiceId,
                    InvoiceNumber = InvoiceNumber,
                    InvoiceDate = InvoiceDate,
                    CustomerId = SelectedCustomerId,
                    CustomerName = string.IsNullOrWhiteSpace(CustomerName) ? "عميل نقدي" : CustomerName.Trim(),
                    CustomerPhone = CustomerPhone?.Trim() ?? string.Empty,
                    CustomerAddress = CustomerAddress?.Trim() ?? string.Empty,
                    Status = "مسودة",
                    SubTotal = SubTotal,
                    Discount = Discount,
                    GrandTotal = GrandTotal,
                    PaymentMethod = PaymentMethod,
                    PaidAmount = PaidAmount,
                    Notes = Notes,
                    Items = CurrentInvoiceItems.ToList()
                };

                var posted = await _invoiceService.PostInvoiceAsync(invoice);

                ShowMessageBox(
                    $"تم اعتماد فاتورة المبيعات رقم ({posted.InvoiceNumber}) بنجاح!\nتم خصم الكميات المباعة من المستودع تلقائياً.",
                    "تم الاعتماد بنجاح",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // إعادة تحميل الأرشيف والأصناف المحدثة
                await LoadAllDataAsync();

                // تجهيز فاتورة جديدة فارغة تلقائياً بعد نجاح الاعتماد
                await PrepareNewInvoiceAsync();
                StatusMessage = $"تم اعتماد الفاتورة '{posted.InvoiceNumber}' بنجاح، وتجهيز فاتورة جديدة.";
            }
            catch (Exception ex)
            {
                ShowMessageBox($"فشل اعتماد الفاتورة وخصم المخزون:\n{ex.Message}", "خطأ في المعاملة", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// إلغاء فاتورة المبيعات المعتمدة وإعادة الكميات للمخزون بحركة 'إلغاء بيع وارد'
        /// </summary>
        private async Task ExecuteCancelInvoiceAsync()
        {
            if (CurrentInvoiceId <= 0 || !CanCancelInvoice) return;

            var cancelledInvoiceNumber = InvoiceNumber;

            var confirm = ShowMessageBox(
                $"هل أنت متأكد من رغبتك في إلغاء فاتورة المبيعات المعتمدة رقم ({InvoiceNumber})؟\n\n" +
                "تنبيهات الأمان والمخزون:\n" +
                "• لن يتم حذف الفاتورة بل ستتغير حالتها إلى (ملغاة) حفظاً للسجلات التاريخية.\n" +
                "• سيتم إعادة الكميات المباعة إلى المستودع بحركة عكسية 'إلغاء بيع وارد'.\n" +
                "• لا يمكن إلغاء فاتورة تم تسجيل مرتجعات معتمدة عليها لحماية توازن الحسابات.",
                "تأكيد إلغاء فاتورة المبيعات",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                string reason = "إلغاء بناءً على طلب المستخدم";
                await _invoiceService.CancelInvoiceAsync(CurrentInvoiceId, reason);

                ShowMessageBox(
                    $"تم إلغاء فاتورة المبيعات رقم ({cancelledInvoiceNumber}) بنجاح!\nتمت إعادة الكميات إلى رصيد المستودع وتسجيل الحركات المعكوسة بدقة.",
                    "تم الإلغاء بنجاح",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await LoadAllDataAsync();
                DataEvents.RaiseInventoryOrInvoicesChanged();

                // تجهيز فاتورة جديدة فارغة تلقائياً بعد نجاح الإلغاء
                await PrepareNewInvoiceAsync();
                StatusMessage = $"تم إلغاء الفاتورة '{cancelledInvoiceNumber}' بنجاح، وتجهيز فاتورة بيع جديدة.";
            }
            catch (Exception ex)
            {
                ShowMessageBox($"تعذر إلغاء فاتورة المبيعات:\n{ex.Message}", "رفض عملية الإلغاء", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadInvoiceDetailsIntoForm(SalesInvoiceModel invoice)
        {
            CurrentInvoiceId = invoice.Id;
            InvoiceNumber = invoice.InvoiceNumber;
            InvoiceDate = invoice.InvoiceDate;
            SelectedCustomerId = invoice.CustomerId;
            CustomerName = invoice.CustomerName ?? "عميل نقدي";
            CustomerPhone = invoice.CustomerPhone ?? string.Empty;
            CustomerAddress = invoice.CustomerAddress ?? string.Empty;
            CloseCustomerSuggestions();
            Status = invoice.Status;
            SubTotal = invoice.SubTotal;
            Discount = invoice.Discount;
            GrandTotal = invoice.GrandTotal;
            PaymentMethod = invoice.PaymentMethod;
            PaidAmount = invoice.PaidAmount;
            Notes = invoice.Notes ?? string.Empty;

            CurrentInvoiceItems.Clear();
            if (invoice.Items != null)
            {
                foreach (var i in invoice.Items) CurrentInvoiceItems.Add(i);
            }

            RecalculateTotals();
            StatusMessage = $"تم فتح الفاتورة '{invoice.InvoiceNumber}' ({Status}).";
        }

        private void OnCustomerInputChanged(string input)
        {
            _customerSearchCts?.Cancel();
            _customerSearchCts?.Dispose();
            _customerSearchCts = null;

            var trimmed = input?.Trim() ?? string.Empty;
            _customerSearchText = trimmed;

            if (string.IsNullOrWhiteSpace(trimmed) || trimmed == "عميل نقدي" || trimmed.Length < 2)
            {
                CloseCustomerSuggestions();
                return;
            }

            IsCustomerPopupOpen = true;
            IsLoadingCustomerSuggestions = true;

            var cts = new CancellationTokenSource();
            _customerSearchCts = cts;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(200, cts.Token);
                    if (cts.Token.IsCancellationRequested) return;

                    var results = await _customerService.GetCustomerSuggestionsAsync(trimmed, cts.Token);
                    if (cts.Token.IsCancellationRequested) return;

                    if (System.Windows.Application.Current?.Dispatcher != null)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            if (cts.Token.IsCancellationRequested) return;
                            CustomerSuggestions.Clear();
                            foreach (var c in results) CustomerSuggestions.Add(c);
                            IsLoadingCustomerSuggestions = false;
                            SelectedCustomerSuggestion = CustomerSuggestions.FirstOrDefault();
                            OnPropertyChanged(nameof(ShowNoCustomerSuggestions));
                        });
                    }
                    else
                    {
                        CustomerSuggestions.Clear();
                        foreach (var c in results) CustomerSuggestions.Add(c);
                        IsLoadingCustomerSuggestions = false;
                        SelectedCustomerSuggestion = CustomerSuggestions.FirstOrDefault();
                        OnPropertyChanged(nameof(ShowNoCustomerSuggestions));
                    }
                }
                catch (OperationCanceledException)
                {
                    // Debounce cancelled
                }
                catch
                {
                    IsLoadingCustomerSuggestions = false;
                }
            }, cts.Token);
        }

        public void SelectCustomerSuggestion(CustomerModel customer)
        {
            if (customer == null) return;

            _suppressCustomerSuggestions = true;
            try
            {
                SelectedCustomerId = customer.Id;
                SelectedCustomerSuggestion = customer;
                CustomerName = customer.Name;
                CustomerPhone = customer.Phone ?? string.Empty;
                CustomerAddress = customer.Address ?? string.Empty;
            }
            finally
            {
                _suppressCustomerSuggestions = false;
            }

            CloseCustomerSuggestions();
        }

        public void CloseCustomerSuggestions()
        {
            _customerSearchCts?.Cancel();
            _customerSearchCts?.Dispose();
            _customerSearchCts = null;

            IsCustomerPopupOpen = false;
            CustomerSuggestions.Clear();
            IsLoadingCustomerSuggestions = false;
            OnPropertyChanged(nameof(ShowNoCustomerSuggestions));
        }

        public void SelectNextCustomerSuggestion()
        {
            if (!IsCustomerPopupOpen || CustomerSuggestions.Count == 0) return;

            if (SelectedCustomerSuggestion == null)
            {
                SelectedCustomerSuggestion = CustomerSuggestions[0];
            }
            else
            {
                int idx = CustomerSuggestions.IndexOf(SelectedCustomerSuggestion);
                if (idx >= 0 && idx < CustomerSuggestions.Count - 1)
                {
                    SelectedCustomerSuggestion = CustomerSuggestions[idx + 1];
                }
            }
        }

        public void SelectPreviousCustomerSuggestion()
        {
            if (!IsCustomerPopupOpen || CustomerSuggestions.Count == 0) return;

            if (SelectedCustomerSuggestion == null) return;

            int idx = CustomerSuggestions.IndexOf(SelectedCustomerSuggestion);
            if (idx > 0)
            {
                SelectedCustomerSuggestion = CustomerSuggestions[idx - 1];
            }
            else if (idx == 0)
            {
                SelectedCustomerSuggestion = null;
            }
        }
    }
}

