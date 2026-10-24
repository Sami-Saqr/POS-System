using System.Collections.ObjectModel;
using CarPartManager.Models;
using CarPartManager.MVVM;
using CarPartManager.Views;

namespace CarPartManager.ViewModels
{
    /// <summary>
    /// الـ ViewModel الرئيسي للتطبيق. يتحكم في شريط التنقل الجانبي والتبديل بين الصفحات (Views)
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private ViewModelBase _currentViewModel;
        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }

        private string _storeName = "مركز الصقر لقطع غيار السيارات";
        public string StoreName
        {
            get => _storeName;
            set => SetProperty(ref _storeName, value);
        }

        private string _storeBranch = "الفرع الرئيسي • القاهرة";
        public string StoreBranch
        {
            get => _storeBranch;
            set => SetProperty(ref _storeBranch, value);
        }

        private string _currentUser = "المهندس أحمد (مدير النظام)";
        public string CurrentUser
        {
            get => _currentUser;
            set => SetProperty(ref _currentUser, value);
        }

        private string _currentDate = DateTime.Now.ToString("dddd، d MMMM yyyy", new System.Globalization.CultureInfo("ar-EG"));
        public string CurrentDate
        {
            get => _currentDate;
            set => SetProperty(ref _currentDate, value);
        }

        public ObservableCollection<NavItem> NavigationItems { get; }

        public RelayCommand NavigateCommand { get; }

        // التخزين المؤقت للـ ViewModels لتجنب إعادة بنائها
        private readonly DashboardViewModel _dashboardViewModel;
        private readonly ProductsViewModel _productsViewModel;
        private readonly InventoryViewModel _inventoryViewModel;
        private readonly PurchaseInvoicesViewModel _purchaseInvoicesViewModel;
        private readonly PurchaseInvoicesHistoryViewModel _purchaseInvoicesHistoryViewModel;
        private readonly SalesInvoicesViewModel _salesInvoicesViewModel;
        private readonly SalesInvoicesHistoryViewModel _salesInvoicesHistoryViewModel;
        private readonly SuppliersViewModel _suppliersViewModel;
        private readonly ReturnsViewModel _returnsViewModel;
        private readonly CustomersViewModel _customersViewModel;
        private readonly ExpensesViewModel _expensesViewModel;
        private readonly Dictionary<string, PlaceholderViewModel> _placeholderCache = new();

        public MainViewModel()
        {
            _dashboardViewModel = new DashboardViewModel();
            _productsViewModel = new ProductsViewModel();
            _inventoryViewModel = new InventoryViewModel();
            _purchaseInvoicesViewModel = new PurchaseInvoicesViewModel();
            _purchaseInvoicesHistoryViewModel = new PurchaseInvoicesHistoryViewModel();
            _salesInvoicesViewModel = new SalesInvoicesViewModel();
            _salesInvoicesHistoryViewModel = new SalesInvoicesHistoryViewModel();
            _suppliersViewModel = new SuppliersViewModel();
            _returnsViewModel = new ReturnsViewModel();
            _customersViewModel = new CustomersViewModel();
            _expensesViewModel = new ExpensesViewModel(currentUser: CurrentUser);
            _currentViewModel = _dashboardViewModel;

            NavigationItems = new ObservableCollection<NavItem>
            {
                new NavItem("Dashboard", "الرئيسية", "M3 13h8V3H3v10zm0 8h8v-6H3v6zm10 0h8V11h-8v10zm0-18v6h8V3h-8z", true),
                new NavItem("Products", "الأصناف وقطع الغيار", "M20 7l-8-4-8 4 8 4 8-4zm-8 6l-8-4v8l8 4 8-4v-8l-8 4z"),
                new NavItem("Inventory", "المخزون والجرد", "M4 6h16v2H4V6zm2 4h12v2H6v-2zm-2 4h16v2H4v-2zm2 4h12v2H6v-2z"),
                new NavItem("POS", "نقطة البيع (الكاشير)", "M7 18c-1.1 0-1.99.9-1.99 2S5.9 22 7 22s2-.9 2-2-.9-2-2-2zM1 2v2h2l3.6 7.59-1.35 2.45c-.16.28-.25.61-.25.96 0 1.1.9 2 2 2h12v-2H7.42c-.14 0-.25-.11-.25-.25l.03-.12.9-1.63h7.45c.75 0 1.41-.41 1.75-1.03l3.58-6.49c.08-.14.12-.31.12-.48 0-.55-.45-1-1-1H5.21l-.94-2H1zm16 16c-1.1 0-1.99.9-1.99 2s.89 2 1.99 2 2-.9 2-2-.9-2-2-2z"),
                new NavItem("SalesInvoicesHistory", "سجل فواتير المبيعات", "M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-5 14H7v-2h7v2zm3-4H7v-2h10v2zm0-4H7V7h10v2z"),
                new NavItem("PurchaseInvoices", "فواتير المشتريات", "M14 2H6c-1.1 0-1.99.9-1.99 2L4 20c0 1.1.89 2 1.99 2H18c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z"),
                new NavItem("PurchaseInvoicesHistory", "سجل فواتير المشتريات", "M19 3h-4.18C14.4 1.84 13.3 1 12 1c-1.3 0-2.4.84-2.82 2H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-7 0c.55 0 1 .45 1 1s-.45 1-1 1-1-.45-1-1 .45-1 1-1zm2 14H7v-2h7v2zm3-4H7v-2h10v2zm0-4H7V7h10v2z"),
                new NavItem("Returns", "المرتجعات (بيع وشراء)", "M19 19H5V5h7V3H5c-1.11 0-2 .9-2 2v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2v-7h-2v7zM14 3v2h3.59l-9.83 9.83 1.41 1.41L19 6.41V10h2V3h-7z"),
                new NavItem("Customers", "العملاء", "M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5c-1.66 0-3 1.34-3 3s1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5C6.34 5 5 6.34 5 3s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z"),
                new NavItem("Suppliers", "الموردين", "M20 8h-3V4H3c-1.1 0-2 .9-2 2v11h2c0 1.66 1.34 3 3 3s3-1.34 3-3h6c0 1.66 1.34 3 3 3s3-1.34 3-3h2v-5l-3-4zM6 18.5c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5zm13.5-9l1.96 2.5H17V9.5h2.5zm-2 9c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5z"),
                new NavItem("Expenses", "المصروفات", "M21 18v1c0 1.1-.9 2-2 2H5c-1.11 0-2-.9-2-2V5c0-1.1.89-2 2-2h14c1.1 0 2 .9 2 2v1h-9c-1.11 0-2 .9-2 2v8c0 1.1.89 2 2 2h9zm-9-2h10V8H12v8zm4-2.5c-.83 0-1.5-.67-1.5-1.5s.67-1.5 1.5-1.5 1.5.67 1.5 1.5-.67 1.5-1.5 1.5z"),
                new NavItem("Reports", "التقارير المحاسبية", "M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zM9 17H7v-7h2v7zm4 0h-2V7h2v10zm4 0h-2v-4h2v4z")
            };

            NavigateCommand = new RelayCommand(param =>
            {
                if (param is NavItem navItem)
                {
                    NavigateTo(navItem);
                }
                else if (param is string tag)
                {
                    if (tag == "SalesInvoices") tag = "POS";
                    var found = NavigationItems.FirstOrDefault(x => x.Tag == tag);
                    if (found != null)
                    {
                        NavigateTo(found);
                    }
                }
            });
        }

        private void NavigateTo(NavItem selectedItem)
        {
            // تحديث حالة الاختيار لجميع العناصر
            foreach (var item in NavigationItems)
            {
                item.IsSelected = (item == selectedItem);
            }

            if (selectedItem.Tag == "Dashboard")
            {
                _ = _dashboardViewModel.LoadDashboardDataAsync();
                CurrentViewModel = _dashboardViewModel;
                return;
            }

            if (selectedItem.Tag == "Products")
            {
                _ = _productsViewModel.LoadProductsAsync();
                CurrentViewModel = _productsViewModel;
                return;
            }

            if (selectedItem.Tag == "Inventory")
            {
                _ = _inventoryViewModel.LoadAllDataAsync();
                CurrentViewModel = _inventoryViewModel;
                return;
            }

            if (selectedItem.Tag == "PurchaseInvoices")
            {
                _ = _purchaseInvoicesViewModel.LoadAllDataAsync();
                CurrentViewModel = _purchaseInvoicesViewModel;
                return;
            }

            if (selectedItem.Tag == "PurchaseInvoicesHistory")
            {
                _ = _purchaseInvoicesHistoryViewModel.LoadAllDataAsync();
                CurrentViewModel = _purchaseInvoicesHistoryViewModel;
                return;
            }

            if (selectedItem.Tag == "SalesInvoices" || selectedItem.Tag == "POS")
            {
                _ = _salesInvoicesViewModel.LoadAllDataAsync();
                CurrentViewModel = _salesInvoicesViewModel;
                return;
            }

            if (selectedItem.Tag == "SalesInvoicesHistory")
            {
                _ = _salesInvoicesHistoryViewModel.LoadAllDataAsync();
                CurrentViewModel = _salesInvoicesHistoryViewModel;
                return;
            }

            if (selectedItem.Tag == "Suppliers")
            {
                _ = _suppliersViewModel.LoadSuppliersAsync();
                CurrentViewModel = _suppliersViewModel;
                return;
            }

            if (selectedItem.Tag == "Returns")
            {
                _ = _returnsViewModel.LoadAllDataAsync();
                CurrentViewModel = _returnsViewModel;
                return;
            }

            if (selectedItem.Tag == "Customers")
            {
                _ = _customersViewModel.LoadCustomersAsync();
                CurrentViewModel = _customersViewModel;
                return;
            }

            if (selectedItem.Tag == "Expenses")
            {
                _ = _expensesViewModel.LoadAllDataAsync();
                CurrentViewModel = _expensesViewModel;
                return;
            }

            if (!_placeholderCache.TryGetValue(selectedItem.Tag, out var placeholder))
            {
                var desc = selectedItem.Tag switch
                {
                    "POS" => "شاشة نقطة البيع السريعة (الكاشير): إصدار فواتير بيع فورية مع مسح الباركود، دعم طرق الدفع المتعددة والطباعة المباشرة.",
                    "SalesInvoices" => "سجل فواتير المبيعات: أرشيف الفواتير الصادرة، إدارة المرتجعات، وتفاصيل الضرائب وبيانات الزبائن.",
                    "PurchaseInvoices" => "سجل فواتير المشتريات: تتبع فواتير الوكلاء وموردي قطع الغيار، ومطابقة الكميات المستلمة في المستودع.",
                    "Customers" => "سجل العملاء والورش: إدارة بيانات العملاء الأفراد والورش الفنية ومراكز الصيانة وحسابات الديون والآجل.",
                    "Suppliers" => "سجل الموردين والشركات: دمج بيانات وكلاء القطع وشركات الاستيراد وأرصدة المستحقات والدفعات.",
                    "Reports" => "التقارير والإحصائيات: كشوف الأرباح والخسائر، حركة المبيعات، تقارير ضريبة القيمة المضافة، وتقييم بضاعة آخر المدة.",
                    _ => "سيتم تطوير وبرمجة هذه الشاشة وربطها بقاعدة البيانات في المراحل القادمة."
                };

                placeholder = new PlaceholderViewModel(selectedItem.Title, desc);
                _placeholderCache[selectedItem.Tag] = placeholder;
            }

            CurrentViewModel = placeholder;
        }
    }
}
