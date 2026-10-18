using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CarPartManager.Models;
using CarPartManager.MVVM;
using CarPartManager.Services;

namespace CarPartManager.ViewModels
{
    /// <summary>
    /// نموذج عرض لوحة التحكم الرئيسية (Dashboard ViewModel).
    /// يربط المؤشرات والإحصائيات وجداول النواقص والفواتير بقاعدة بيانات SQLite الحقيقية.
    /// </summary>
    public class DashboardViewModel : ViewModelBase
    {
        private readonly IDashboardService _dashboardService;

        // بطاقة 1: قيمة المبيعات المعتمدة اليوم
        private string _todaySalesAmount = "0.00 ج.م";
        public string TodaySalesAmount
        {
            get => _todaySalesAmount;
            set
            {
                if (SetProperty(ref _todaySalesAmount, value))
                {
                    OnPropertyChanged(nameof(TodaySales)); // للتوافق العكسي
                }
            }
        }

        // للتوافق العكسي
        public string TodaySales
        {
            get => _todaySalesAmount;
            set => TodaySalesAmount = value;
        }

        private string _todaySalesSubtext = "جاري تحميل البيانات...";
        public string TodaySalesSubtext
        {
            get => _todaySalesSubtext;
            set => SetProperty(ref _todaySalesSubtext, value);
        }

        // بطاقة 2: قيمة المشتريات المعتمدة اليوم
        private string _todayPurchasesAmount = "0.00 ج.م";
        public string TodayPurchasesAmount
        {
            get => _todayPurchasesAmount;
            set => SetProperty(ref _todayPurchasesAmount, value);
        }

        private string _todayPurchasesSubtext = "جاري تحميل البيانات...";
        public string TodayPurchasesSubtext
        {
            get => _todayPurchasesSubtext;
            set => SetProperty(ref _todayPurchasesSubtext, value);
        }

        // فواتير اليوم (بيع ومشتريات)
        private string _invoiceCount = "0 فواتير";
        public string InvoiceCount
        {
            get => _invoiceCount;
            set => SetProperty(ref _invoiceCount, value);
        }

        private string _invoiceCountSubtext = "إجمالي الفواتير المسجلة: 0 فاتورة";
        public string InvoiceCountSubtext
        {
            get => _invoiceCountSubtext;
            set => SetProperty(ref _invoiceCountSubtext, value);
        }

        // بطاقة 3: إجمالي قطع الغيار والأصناف
        private string _productCount = "0 صنف";
        public string ProductCount
        {
            get => _productCount;
            set => SetProperty(ref _productCount, value);
        }

        private string _productCountSubtext = "موزعة على 0 أقسام سيارات";
        public string ProductCountSubtext
        {
            get => _productCountSubtext;
            set => SetProperty(ref _productCountSubtext, value);
        }

        // بطاقة 4: تنبيهات نقص المخزون
        private string _lowStockCount = "0 صنف حرج";
        public string LowStockCount
        {
            get => _lowStockCount;
            set => SetProperty(ref _lowStockCount, value);
        }

        private string _lowStockSubtext = "المخزون آمن وكافة الأصناف كافية";
        public string LowStockSubtext
        {
            get => _lowStockSubtext;
            set => SetProperty(ref _lowStockSubtext, value);
        }

        // معلومات المستودع والفرع (الصندوق السفلي الأيسر)
        private string _warehouseInventoryCost = "0.00 ج.م";
        public string WarehouseInventoryCost
        {
            get => _warehouseInventoryCost;
            set => SetProperty(ref _warehouseInventoryCost, value);
        }

        private string _salesCountText = "0 فاتورة بيع مسجلة";
        public string SalesCountText
        {
            get => _salesCountText;
            set => SetProperty(ref _salesCountText, value);
        }

        private string _purchasesCountText = "0 فاتورة شراء مسجلة";
        public string PurchasesCountText
        {
            get => _purchasesCountText;
            set => SetProperty(ref _purchasesCountText, value);
        }

        private string _suppliersCountText = "0 موردين مسجلين";
        public string SuppliersCountText
        {
            get => _suppliersCountText;
            set => SetProperty(ref _suppliersCountText, value);
        }

        private string _inventoryMovementsText = "0 حركة مسجلة";
        public string InventoryMovementsText
        {
            get => _inventoryMovementsText;
            set => SetProperty(ref _inventoryMovementsText, value);
        }

        private string _systemStatusNote = "قاعدة البيانات متصلة بنجاح (SQLite) • يتم تحديث المؤشرات حياً من السجلات.";
        public string SystemStatusNote
        {
            get => _systemStatusNote;
            set => SetProperty(ref _systemStatusNote, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        // جداول وبيانات الـ Dashboard
        public ObservableCollection<LowStockItem> LowStockItems { get; }
        public ObservableCollection<RecentInvoiceSummary> RecentInvoices { get; }

        // الأوامر (Commands)
        public RelayCommand RefreshCommand { get; }

        public DashboardViewModel(IDashboardService? dashboardService = null)
        {
            _dashboardService = dashboardService ?? new SqliteDashboardService();

            LowStockItems = new ObservableCollection<LowStockItem>();
            RecentInvoices = new ObservableCollection<RecentInvoiceSummary>();

            RefreshCommand = new RelayCommand(async () => await LoadDashboardDataAsync());

            // الاشتراك في حدث تحديث البيانات المركزي لتحديث لوحة التحكم تلقائياً
            DataEvents.InventoryOrInvoicesChanged += OnExternalDataChanged;

            _ = LoadDashboardDataAsync();
        }

        private void OnExternalDataChanged()
        {
            // ضمان استدعاء التحديث على خيط واجهة المستخدم الرئيسي (UI Thread)
            if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.InvokeAsync(async () => await LoadDashboardDataAsync());
            }
            else
            {
                _ = LoadDashboardDataAsync();
            }
        }

        /// <summary>
        /// استخراج المؤشرات والإحصائيات وجداول النواقص والفواتير الحقيقية من SQLite
        /// </summary>
        public async Task LoadDashboardDataAsync()
        {
            if (IsLoading) return;

            try
            {
                IsLoading = true;
                var summary = await _dashboardService.GetDashboardSummaryAsync();

                // 1. تحديث بطاقة قيمة المبيعات المعتمدة اليوم
                TodaySalesAmount = $"{summary.TodaySalesTotal:N2} ج.م";
                TodaySalesSubtext = summary.TodaySalesCount > 0
                    ? $"{summary.TodaySalesCount} فواتير بيع معتمدة" + (summary.TodaySalesReturnsCount > 0 ? $" • {summary.TodaySalesReturnsCount} مرتجع مبيعات ({summary.TodaySalesReturnsTotal:N2} ج.م)" : "")
                    : (summary.TodaySalesReturnsCount > 0 ? $"مرتجع مبيعات اليوم: {summary.TodaySalesReturnsTotal:N2} ج.م" : "لا توجد مبيعات معتمدة اليوم");

                // 2. تحديث بطاقة قيمة المشتريات المعتمدة اليوم
                TodayPurchasesAmount = $"{summary.TodayPurchasesTotal:N2} ج.م";
                TodayPurchasesSubtext = summary.TodayPurchasesCount > 0
                    ? $"{summary.TodayPurchasesCount} فواتير شراء معتمدة" + (summary.TodayPurchaseReturnsCount > 0 ? $" • {summary.TodayPurchaseReturnsCount} مرتجع مشتريات ({summary.TodayPurchaseReturnsTotal:N2} ج.م)" : "")
                    : (summary.TodayPurchaseReturnsCount > 0 ? $"مرتجع مشتريات اليوم: {summary.TodayPurchaseReturnsTotal:N2} ج.م" : "لا توجد مشتريات معتمدة اليوم");

                // 3. تحديث بطاقة فواتير اليوم الإجمالية
                int totalTodayInvoices = summary.TodaySalesCount + summary.TodayPurchasesCount;
                int totalAllInvoices = summary.TotalSalesCount + summary.TotalPurchasesCount;
                InvoiceCount = $"{totalTodayInvoices} فواتير اليوم";
                InvoiceCountSubtext = $"{summary.TodaySalesCount} بيع • {summary.TodayPurchasesCount} شراء (مرتجعات: {summary.TotalReturnsCount})";

                // 4. تحديث بطاقة الأصناف وقطع الغيار
                ProductCount = $"{summary.TotalProductsCount:N0} صنف";
                ProductCountSubtext = $"موزعة على {summary.TotalCategoriesCount} تصنيفات • {summary.TotalPiecesCount:N0} قطعة بالمستودع";

                // 5. تحديث بطاقة تنبيهات نقص المخزون
                LowStockCount = $"{summary.LowStockProductsCount} أصناف حرجة";
                LowStockSubtext = summary.LowStockProductsCount > 0
                    ? "أصناف وصلت إلى أو أقل من حد الأمان"
                    : "المخزون آمن وكافة الأصناف أعلى من حد الطلب";

                // 6. تحديث جدول النواقص الحرجة
                LowStockItems.Clear();
                foreach (var item in summary.LowStockItems)
                {
                    LowStockItems.Add(item);
                }

                // 7. تحديث قائمة آخر الفواتير الصادرة والواردة الفعلية
                RecentInvoices.Clear();
                foreach (var inv in summary.RecentInvoices)
                {
                    RecentInvoices.Add(inv);
                }

                // 8. تحديث صندوق معلومات المستودع والفرع
                WarehouseInventoryCost = $"{summary.TotalInventoryCost:N2} ج.م";
                SalesCountText = $"{summary.TotalSalesCount} فاتورة بيع مسجلة";
                PurchasesCountText = $"{summary.TotalPurchasesCount} فاتورة شراء مسجلة";
                SuppliersCountText = $"{summary.TotalSuppliersCount} موردين مسجلين";
                InventoryMovementsText = $"{summary.TotalInventoryMovementsCount} حركة مسجلة";
                SystemStatusNote = $"قاعدة البيانات متصلة بنجاح (SQLite) • آخر تحديث: {summary.LastUpdated:hh:mm:ss tt}";

                StatusMessage = "تم تحديث بيانات لوحة التحكم بنجاح.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DashboardViewModel Error]: {ex}");
                StatusMessage = $"تعذر تحديث لوحة التحكم: {ex.Message}";
                SystemStatusNote = $"تنبيه في قراءة البيانات: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
