using System.Collections.ObjectModel;
using CarPartManager.Models;
using CarPartManager.MVVM;
using CarPartManager.Services;

namespace CarPartManager.ViewModels
{
    /// <summary>
    /// نموذج عرض شاشة التقارير المحاسبية (الجزء أ): مرشّح تاريخي عام + ملخص الأرباح + تقرير المبيعات + تقرير المصروفات.
    /// الشاشة للقراءة فقط ولا تُعدّل أي بيانات.
    /// </summary>
    public class ReportsViewModel : ViewModelBase
    {
        private const string PresetToday = "اليوم";
        private const string PresetWeek = "الأسبوع";
        private const string PresetMonth = "الشهر";
        private const string PresetYear = "السنة";
        private const string PresetCustom = "مخصص";

        private readonly IReportsService _reportsService;

        /// <summary>يمنع إعادة التحميل عند تحديث حقول التاريخ برمجياً</summary>
        private bool _suppressReload;

        /// <summary>يُستخدم لإهمال نتائج التحميل القديمة عند تغيير المرشّح بسرعة</summary>
        private int _loadToken;

        /// <summary>يمنع إعادة تحميل تبويب تقييم المخزون قبل أول تحميل كامل للشاشة</summary>
        private bool _hasLoadedOnce;

        public ReportsViewModel(IReportsService? reportsService = null)
        {
            _reportsService = reportsService ?? new SqliteReportsService();

            var today = DateTime.Today;
            _fromDate = new DateTime(today.Year, today.Month, 1);
            _toDate = today;

            RefreshCommand = new RelayCommand(() => _ = LoadAllAsync());
            SelectPresetCommand = new RelayCommand(param =>
            {
                if (param is string preset && PeriodPresets.Contains(preset))
                    SelectedPreset = preset;
            });

            SyncDatesWithPreset();
        }

        // ── المرشّح التاريخي العام ─────────────────────────────────────────────

        public ObservableCollection<string> PeriodPresets { get; } =
            new() { PresetToday, PresetWeek, PresetMonth, PresetYear, PresetCustom };

        private string _selectedPreset = PresetMonth;
        public string SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (SetProperty(ref _selectedPreset, value))
                {
                    OnPropertyChanged(nameof(IsCustomRange));
                    SyncDatesWithPreset();
                    _ = LoadAllAsync();
                }
            }
        }

        public bool IsCustomRange => SelectedPreset == PresetCustom;

        private DateTime? _fromDate;
        public DateTime? FromDate
        {
            get => _fromDate;
            set
            {
                if (SetProperty(ref _fromDate, value) && !_suppressReload && IsCustomRange)
                    _ = LoadAllAsync();
            }
        }

        private DateTime? _toDate;
        public DateTime? ToDate
        {
            get => _toDate;
            set
            {
                if (SetProperty(ref _toDate, value) && !_suppressReload && IsCustomRange)
                    _ = LoadAllAsync();
            }
        }

        private string _periodText = string.Empty;
        /// <summary>وصف الفترة المعروضة (نطاق شامل الطرفين للعرض فقط)</summary>
        public string PeriodText
        {
            get => _periodText;
            set => SetProperty(ref _periodText, value);
        }

        private int _selectedTabIndex;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        // ── الأوامر ───────────────────────────────────────────────────────────
        public RelayCommand RefreshCommand { get; }
        public RelayCommand SelectPresetCommand { get; }

        // ── حالة الشاشة ───────────────────────────────────────────────────────
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    OnPropertyChanged(nameof(ShowProfitEmptyState));
                    OnPropertyChanged(nameof(ShowSalesEmptyState));
                    OnPropertyChanged(nameof(ShowExpensesEmptyState));
                    OnPropertyChanged(nameof(ShowPurchasesEmptyState));
                    OnPropertyChanged(nameof(ShowInventoryEmptyState));
                    OnPropertyChanged(nameof(ShowCustomerDebtsEmptyState));
                    OnPropertyChanged(nameof(ShowItemProfitEmptyState));
                }
            }
        }

        private bool _hasProfitData;
        public bool HasProfitData
        {
            get => _hasProfitData;
            set
            {
                if (SetProperty(ref _hasProfitData, value))
                    OnPropertyChanged(nameof(ShowProfitEmptyState));
            }
        }

        public bool ShowProfitEmptyState => !IsLoading && !HasProfitData;
        public bool ShowSalesEmptyState => !IsLoading && DailySales.Count == 0 && CustomerSales.Count == 0;
        public bool ShowExpensesEmptyState => !IsLoading && ExpenseDetails.Count == 0;
        public string EmptyStateText => "لا توجد بيانات في هذه الفترة";

        private bool _hasDataWarning;
        public bool HasDataWarning
        {
            get => _hasDataWarning;
            set => SetProperty(ref _hasDataWarning, value);
        }

        private string _dataWarningText = string.Empty;
        public string DataWarningText
        {
            get => _dataWarningText;
            set => SetProperty(ref _dataWarningText, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (SetProperty(ref _statusMessage, value))
                    OnPropertyChanged(nameof(HasStatusMessage));
            }
        }

        public bool HasStatusMessage => !string.IsNullOrWhiteSpace(_statusMessage);

        private bool _isStatusError;
        public bool IsStatusError
        {
            get => _isStatusError;
            set
            {
                if (SetProperty(ref _isStatusError, value))
                {
                    OnPropertyChanged(nameof(StatusBackground));
                    OnPropertyChanged(nameof(StatusForeground));
                }
            }
        }

        public string StatusBackground => IsStatusError ? "#FEF2F2" : "#ECFDF5";
        public string StatusForeground => IsStatusError ? "#B91C1C" : "#065F46";

        // ── تبويب ملخص الأرباح ────────────────────────────────────────────────
        public string GrossSalesText { get => _grossSalesText; private set => SetProperty(ref _grossSalesText, value); }
        private string _grossSalesText = "0.00 ج.م";
        public string DiscountsText { get => _discountsText; private set => SetProperty(ref _discountsText, value); }
        private string _discountsText = "0.00 ج.م";
        public string SalesReturnsText { get => _salesReturnsText; private set => SetProperty(ref _salesReturnsText, value); }
        private string _salesReturnsText = "0.00 ج.م";
        public string NetSalesText { get => _netSalesText; private set => SetProperty(ref _netSalesText, value); }
        private string _netSalesText = "0.00 ج.م";
        public string CogsText { get => _cogsText; private set => SetProperty(ref _cogsText, value); }
        private string _cogsText = "0.00 ج.م";
        public string ReturnedCogsReductionText { get => _returnedCogsReductionText; private set => SetProperty(ref _returnedCogsReductionText, value); }
        private string _returnedCogsReductionText = "0.00 ج.م";
        public string GrossProfitText { get => _grossProfitText; private set => SetProperty(ref _grossProfitText, value); }
        private string _grossProfitText = "0.00 ج.م";
        public string ExpensesText { get => _expensesText; private set => SetProperty(ref _expensesText, value); }
        private string _expensesText = "0.00 ج.م";
        public string NetProfitText { get => _netProfitText; private set => SetProperty(ref _netProfitText, value); }
        private string _netProfitText = "0.00 ج.م";
        public string InvoiceCountText { get => _invoiceCountText; private set => SetProperty(ref _invoiceCountText, value); }
        private string _invoiceCountText = "0 فاتورة";

        public string NetSalesComparisonText { get => _netSalesComparisonText; private set => SetProperty(ref _netSalesComparisonText, value); }
        private string _netSalesComparisonText = string.Empty;
        public string GrossProfitComparisonText { get => _grossProfitComparisonText; private set => SetProperty(ref _grossProfitComparisonText, value); }
        private string _grossProfitComparisonText = string.Empty;
        public string ExpensesComparisonText { get => _expensesComparisonText; private set => SetProperty(ref _expensesComparisonText, value); }
        private string _expensesComparisonText = string.Empty;
        public string NetProfitComparisonText { get => _netProfitComparisonText; private set => SetProperty(ref _netProfitComparisonText, value); }
        private string _netProfitComparisonText = string.Empty;

        public string NetProfitColor { get => _netProfitColor; private set => SetProperty(ref _netProfitColor, value); }
        private string _netProfitColor = "#0F172A";

        public bool IsCogsEstimated { get => _isCogsEstimated; private set => SetProperty(ref _isCogsEstimated, value); }
        private bool _isCogsEstimated;

        public string CogsNoteText { get => _cogsNoteText; private set => SetProperty(ref _cogsNoteText, value); }
        private string _cogsNoteText = string.Empty;

        public string ReturnsNoteText { get => _returnsNoteText; private set => SetProperty(ref _returnsNoteText, value); }
        private string _returnsNoteText = string.Empty;

        // ── تبويب تقرير المبيعات ──────────────────────────────────────────────
        public ObservableCollection<SalesDailyReportRow> DailySales { get; } = new();
        public ObservableCollection<TopProductReportRow> TopProductsByQuantity { get; } = new();
        public ObservableCollection<TopProductReportRow> TopProductsByValue { get; } = new();
        public ObservableCollection<CustomerSalesReportRow> CustomerSales { get; } = new();

        public string DailySalesTotalText { get => _dailySalesTotalText; private set => SetProperty(ref _dailySalesTotalText, value); }
        private string _dailySalesTotalText = "0.00 ج.م";

        /// <summary>ملاحظة توضيحية: لا توجد مكتبة رسوم بيانية في المشروع</summary>
        public string ChartsNoteText => "لا توجد مكتبة رسوم بيانية مضافة إلى المشروع، لذلك تُعرض جميع البيانات في جداول تفصيلية.";

        // ── تبويب تقرير المصروفات ─────────────────────────────────────────────
        public ObservableCollection<ExpenseCategoryReportRow> ExpenseCategories { get; } = new();
        public ObservableCollection<ExpenseReportDetailRow> ExpenseDetails { get; } = new();

        public string ExpenseTotalText { get => _expenseTotalText; private set => SetProperty(ref _expenseTotalText, value); }
        private string _expenseTotalText = "0.00 ج.م";
        public string ExpenseCountText { get => _expenseCountText; private set => SetProperty(ref _expenseCountText, value); }
        private string _expenseCountText = "0 مصروف";
        public string ExpenseComparisonText { get => _expenseComparisonText; private set => SetProperty(ref _expenseComparisonText, value); }
        private string _expenseComparisonText = string.Empty;

        // ── تبويب المشتريات ───────────────────────────────────────────────────

        public ObservableCollection<PurchaseDailyReportRow> PurchasesDaily { get; } = new();
        public ObservableCollection<SupplierPurchaseReportRow> PurchasesBySupplier { get; } = new();
        public ObservableCollection<TopProductReportRow> PurchasesTopProducts { get; } = new();

        public string PurchasesTotalText { get => _purchasesTotalText; private set => SetProperty(ref _purchasesTotalText, value); }
        private string _purchasesTotalText = "0.00 ج.م";
        public string PurchasesInvoiceCountText { get => _purchasesInvoiceCountText; private set => SetProperty(ref _purchasesInvoiceCountText, value); }
        private string _purchasesInvoiceCountText = "0 فاتورة";
        public string PurchasesComparisonText { get => _purchasesComparisonText; private set => SetProperty(ref _purchasesComparisonText, value); }
        private string _purchasesComparisonText = string.Empty;
        public string PurchasesDailyTotalText { get => _purchasesDailyTotalText; private set => SetProperty(ref _purchasesDailyTotalText, value); }
        private string _purchasesDailyTotalText = "0.00 ج.م";

        private bool _hasPurchasesData;
        public bool HasPurchasesData
        {
            get => _hasPurchasesData;
            set
            {
                if (SetProperty(ref _hasPurchasesData, value))
                    OnPropertyChanged(nameof(ShowPurchasesEmptyState));
            }
        }

        public bool ShowPurchasesEmptyState => !IsLoading && PurchasesDaily.Count == 0;

        private string _purchasesSupplierNoteText = string.Empty;
        public string PurchasesSupplierNoteText { get => _purchasesSupplierNoteText; private set => SetProperty(ref _purchasesSupplierNoteText, value); }

        // ── تبويب تقييم المخزون (الحالة الحالية — لا يتأثر بالمرشّح التاريخي) ──

        public ObservableCollection<InventoryValuationRow> InventoryRows { get; } = new();
        public ObservableCollection<InventoryValuationRow> InventoryLowStock { get; } = new();
        public ObservableCollection<InventoryValuationRow> InventoryOutOfStock { get; } = new();
        public ObservableCollection<InventoryValuationRow> InventoryStagnant { get; } = new();
        public ObservableCollection<InventoryValuationRow> InventoryTopByValue { get; } = new();

        /// <summary>خيارات عدد الأيام لتعريف الصنف الراكد (قابل للتعديل)</summary>
        public ObservableCollection<int> StagnantDayOptions { get; } = new() { 30, 60, 90, 180 };

        private int _stagnantDays = 60;
        public int StagnantDays
        {
            get => _stagnantDays;
            set
            {
                if (value <= 0) value = 60;
                if (SetProperty(ref _stagnantDays, value))
                    _ = LoadInventoryValuationAsync();
            }
        }

        public string InventoryTotalValueText { get => _inventoryTotalValueText; private set => SetProperty(ref _inventoryTotalValueText, value); }
        private string _inventoryTotalValueText = "0.00 ج.م";
        public string InventoryProductCountText { get => _inventoryProductCountText; private set => SetProperty(ref _inventoryProductCountText, value); }
        private string _inventoryProductCountText = "0 صنف";
        public string InventoryLowStockText { get => _inventoryLowStockText; private set => SetProperty(ref _inventoryLowStockText, value); }
        private string _inventoryLowStockText = "0 صنف";
        public string InventoryOutOfStockText { get => _inventoryOutOfStockText; private set => SetProperty(ref _inventoryOutOfStockText, value); }
        private string _inventoryOutOfStockText = "0 صنف";
        public string InventoryStagnantText { get => _inventoryStagnantText; private set => SetProperty(ref _inventoryStagnantText, value); }
        private string _inventoryStagnantText = "0 صنف";

        public string InventoryStateNoteText { get => _inventoryStateNoteText; private set => SetProperty(ref _inventoryStateNoteText, value); }
        private string _inventoryStateNoteText = "هذا التقرير يعرض الحالة الحالية للمخزون ولا يتأثر بالمرشّح التاريخي.";

        public string InventoryComparisonText { get => _inventoryComparisonText; private set => SetProperty(ref _inventoryComparisonText, value); }
        private string _inventoryComparisonText = string.Empty;

        public bool ShowInventoryEmptyState => !IsLoading && InventoryRows.Count == 0;

        // ── تبويب مديونيات العملاء (الحالة الحالية) ───────────────────────────

        public ObservableCollection<CustomerDebtRow> CustomerDebtRows { get; } = new();

        public string CustomerDebtTotalText { get => _customerDebtTotalText; private set => SetProperty(ref _customerDebtTotalText, value); }
        private string _customerDebtTotalText = "0.00 ج.م";
        public string CustomerDebtCountText { get => _customerDebtCountText; private set => SetProperty(ref _customerDebtCountText, value); }
        private string _customerDebtCountText = "0 عميل";
        public string CustomerDebtNoteText { get => _customerDebtNoteText; private set => SetProperty(ref _customerDebtNoteText, value); }
        private string _customerDebtNoteText = string.Empty;

        public bool ShowCustomerDebtsEmptyState => !IsLoading && CustomerDebtRows.Count == 0;

        // ── تبويب ربحية الأصناف ───────────────────────────────────────────────

        public ObservableCollection<ItemProfitabilityRow> ItemProfitRows { get; } = new();
        public ObservableCollection<ItemProfitabilityRow> ItemProfitBest { get; } = new();
        public ObservableCollection<ItemProfitabilityRow> ItemProfitWorst { get; } = new();

        public string ItemProfitSalesText { get => _itemProfitSalesText; private set => SetProperty(ref _itemProfitSalesText, value); }
        private string _itemProfitSalesText = "0.00 ج.م";
        public string ItemProfitCostText { get => _itemProfitCostText; private set => SetProperty(ref _itemProfitCostText, value); }
        private string _itemProfitCostText = "0.00 ج.م";
        public string ItemProfitTotalText { get => _itemProfitTotalText; private set => SetProperty(ref _itemProfitTotalText, value); }
        private string _itemProfitTotalText = "0.00 ج.م";
        public string ItemProfitMarginText { get => _itemProfitMarginText; private set => SetProperty(ref _itemProfitMarginText, value); }
        private string _itemProfitMarginText = "—";

        public string ItemProfitCrossCheckText { get => _itemProfitCrossCheckText; private set => SetProperty(ref _itemProfitCrossCheckText, value); }
        private string _itemProfitCrossCheckText = string.Empty;

        public string ItemProfitNoteText { get => _itemProfitNoteText; private set => SetProperty(ref _itemProfitNoteText, value); }
        private string _itemProfitNoteText = string.Empty;

        public bool ShowItemProfitEmptyState => !IsLoading && ItemProfitRows.Count == 0;

        // ── التحميل ───────────────────────────────────────────────────────────

        /// <summary>يُستدعى عند كل تنقل إلى الشاشة أو تغيير للمرشّح</summary>
        public async Task LoadAllAsync()
        {
            if (!TryResolvePeriod(out var from, out var toExclusive)) return;

            var token = ++_loadToken;
            IsLoading = true;
            HasDataWarning = false;
            DataWarningText = string.Empty;
            SetStatus(string.Empty);

            try
            {
                var summaryTask = _reportsService.GetProfitSummaryAsync(from, toExclusive);
                var dailyTask = _reportsService.GetSalesDailyReportAsync(from, toExclusive);
                var topTask = _reportsService.GetTopProductsAsync(from, toExclusive, 10);
                var customersTask = _reportsService.GetSalesByCustomerAsync(from, toExclusive, 20);
                var expensesTask = _reportsService.GetExpensesReportAsync(from, toExclusive);
                var purchasesTask = _reportsService.GetPurchasesReportAsync(from, toExclusive);
                var inventoryTask = _reportsService.GetInventoryValuationAsync(StagnantDays, 10);
                var debtsTask = _reportsService.GetCustomerDebtsAsync();
                var itemsTask = _reportsService.GetItemProfitabilityAsync(from, toExclusive, 10);

                await Task.WhenAll(summaryTask, dailyTask, topTask, customersTask, expensesTask,
                                   purchasesTask, inventoryTask, debtsTask, itemsTask);

                if (token != _loadToken) return;

                ApplySummary(summaryTask.Result);
                ApplySalesReport(dailyTask.Result, topTask.Result, customersTask.Result);
                ApplyExpensesReport(expensesTask.Result);
                ApplyPurchasesReport(purchasesTask.Result);
                ApplyInventoryValuation(inventoryTask.Result);
                ApplyCustomerDebts(debtsTask.Result);
                ApplyItemProfitability(itemsTask.Result);

                CrossCheckDailyTotal(summaryTask.Result.NetSales);
                CrossCheckItemProfit(summaryTask.Result.GrossProfit, itemsTask.Result);

                PeriodText = $"الفترة: من {from:yyyy-MM-dd} إلى {toExclusive.AddDays(-1):yyyy-MM-dd}";

                SetStatus($"تم تحديث التقارير — فواتير البيع المعتمدة: {summaryTask.Result.InvoiceCount} — مصروفات الفترة: {ExpenseTotalText}");

                _hasLoadedOnce = true;
            }
            catch (Exception ex)
            {
                if (token != _loadToken) return;
                ClearAll();
                SetStatus($"تعذر تحديث التقارير: {ex.Message}", error: true);
            }
            finally
            {
                if (token == _loadToken) IsLoading = false;
            }
        }

        /// <summary>
        /// يحوّل المرشّح المختار إلى حدود تاريخية: بداية شاملة ونهاية حصرية (اليوم التالي) حتى لا يُقتطع أي يوم.
        /// </summary>
        private bool TryResolvePeriod(out DateTime fromInclusive, out DateTime toExclusive)
        {
            var today = DateTime.Today;
            fromInclusive = today;
            toExclusive = today.AddDays(1);

            switch (SelectedPreset)
            {
                case PresetToday:
                    return true;

                case PresetWeek:
                    // الأسبوع يبدأ السبت وفق العُرف المحلي
                    var daysSinceSaturday = ((int)today.DayOfWeek - (int)DayOfWeek.Saturday + 7) % 7;
                    fromInclusive = today.AddDays(-daysSinceSaturday);
                    toExclusive = today.AddDays(1);
                    return true;

                case PresetMonth:
                    fromInclusive = new DateTime(today.Year, today.Month, 1);
                    toExclusive = fromInclusive.AddMonths(1);
                    return true;

                case PresetYear:
                    fromInclusive = new DateTime(today.Year, 1, 1);
                    toExclusive = fromInclusive.AddYears(1);
                    return true;

                default: // مخصص
                    if (!FromDate.HasValue || !ToDate.HasValue)
                    {
                        SetStatus("يجب تحديد تاريخ البداية وتاريخ النهاية للمدى المخصص.", error: true);
                        return false;
                    }

                    if (FromDate.Value.Date > ToDate.Value.Date)
                    {
                        SetStatus("تاريخ البداية يجب أن يكون قبل تاريخ النهاية.", error: true);
                        return false;
                    }

                    fromInclusive = FromDate.Value.Date;
                    toExclusive = ToDate.Value.Date.AddDays(1);
                    return true;
            }
        }

        /// <summary>يُزامن حقلي التاريخ مع المرشّح المختار لعرض النطاق الفعلي</summary>
        private void SyncDatesWithPreset()
        {
            if (SelectedPreset == PresetCustom) return;
            if (!TryResolvePeriod(out var from, out var toExclusive)) return;

            _suppressReload = true;
            try
            {
                FromDate = from;
                ToDate = toExclusive.AddDays(-1);
            }
            finally
            {
                _suppressReload = false;
            }
        }

        // ── التطبيق على الواجهة ───────────────────────────────────────────────

        private void ApplySummary(ProfitSummaryModel summary)
        {
            HasProfitData = summary.InvoiceCount > 0 || summary.Expenses > 0m || summary.SalesReturns > 0m;

            GrossSalesText = FormatMoney(summary.GrossSales);
            DiscountsText = FormatMoney(summary.Discounts);
            SalesReturnsText = FormatMoney(summary.SalesReturns);
            NetSalesText = FormatMoney(summary.NetSales);
            CogsText = FormatMoney(summary.Cogs);
            ReturnedCogsReductionText = FormatMoney(summary.ReturnedCogsReduction);
            GrossProfitText = FormatMoney(summary.GrossProfit);
            ExpensesText = FormatMoney(summary.Expenses);
            NetProfitText = FormatMoney(summary.NetProfit);
            InvoiceCountText = $"{summary.InvoiceCount} فاتورة معتمدة";
            NetProfitColor = summary.NetProfit >= 0m ? "#059669" : "#DC2626";

            var previous = summary.Previous;
            NetSalesComparisonText = previous is null
                ? "لا توجد فترة سابقة للمقارنة"
                : FormatComparison(previous.NetSales, summary.NetSalesChange, summary.NetSalesChangePercent);
            GrossProfitComparisonText = previous is null
                ? "لا توجد فترة سابقة للمقارنة"
                : FormatComparison(previous.GrossProfit, summary.GrossProfitChange, summary.GrossProfitChangePercent);
            ExpensesComparisonText = previous is null
                ? "لا توجد فترة سابقة للمقارنة"
                : FormatComparison(previous.Expenses, summary.ExpensesChange, summary.ExpensesChangePercent);
            NetProfitComparisonText = previous is null
                ? "لا توجد فترة سابقة للمقارنة"
                : FormatComparison(previous.NetProfit, summary.NetProfitChange, summary.NetProfitChangePercent);

            IsCogsEstimated = summary.IsCogsEstimated;
            CogsNoteText = summary.IsCogsEstimated
                ? $"التكلفة تقديرية جزئياً: {summary.EstimatedLineCount} بند في {summary.EstimatedInvoiceCount} فاتورة قديمة لا تُخزّن تكلفة الوحدة وقت البيع، لذا تُحتسب تكلفتها بسعر التكلفة الحالي للأصناف، بينما بقية البنود محسوبة بتكلفتها المثبَّتة وقت البيع."
                : string.Empty;

            ReturnsNoteText = "مرتجعات المبيعات المعتمدة تُخصم من صافي المبيعات، وتُخصم تكلفة البنود الصالحة للبيع (التي تُعاد للمخزون) من تكلفة البضاعة المبيعة، أما التالف/المستبعد نهائياً فلا يخفض التكلفة.";
        }

        private void ApplySalesReport(List<SalesDailyReportRow> dailyRows, TopProductsModel topProducts, List<CustomerSalesReportRow> customers)
        {
            DailySales.Clear();
            foreach (var row in dailyRows) DailySales.Add(row);

            TopProductsByQuantity.Clear();
            foreach (var row in topProducts.ByQuantity) TopProductsByQuantity.Add(row);

            TopProductsByValue.Clear();
            foreach (var row in topProducts.ByValue) TopProductsByValue.Add(row);

            CustomerSales.Clear();
            foreach (var row in customers) CustomerSales.Add(row);

            // التدقيق المتقاطع: مجموع عمود "الصافي" في الجدول اليومي يجب أن يطابق صافي المبيعات في الملخص
            var dailyNet = DailySales.Sum(r => r.Net);
            DailySalesTotalText = FormatMoney(dailyNet);

            OnPropertyChanged(nameof(ShowSalesEmptyState));
            OnPropertyChanged(nameof(ShowExpensesEmptyState));
        }

        /// <summary>يدقّق تطابق مجموع الجدول اليومي مع بطاقة صافي المبيعات ويُظهر تنبيهاً عند وجود فرق.</summary>
        private void CrossCheckDailyTotal(decimal summaryNetSales)
        {
            var dailyNet = DailySales.Sum(r => r.Net);
            var difference = Math.Abs(dailyNet - summaryNetSales);

            if (difference > 0.01m)
            {
                AppendDataWarning(
                    $"تنبيه تعارض بيانات: مجموع الصافي في التقرير اليومي ({FormatMoney(dailyNet)}) " +
                    $"لا يطابق بطاقة صافي المبيعات ({FormatMoney(summaryNetSales)}) — الفرق {FormatMoney(difference)}.");
            }
        }

        /// <summary>
        /// يدقّق تطابق إجمالي ربح الأصناف مع الربح الإجمالي في ملخص الأرباح لنفس الفترة.
        /// يُحتسب ربح الصنف على أساس: (قيمة البيع − مرتجعات البيع) − (تكلفة المبيعات − تكلفة المرتجعات الصالحة للبيع)،
        /// مع توزيع خصم الفاتورة على بنودها بنسبة قيمة البند — لذلك يجب أن يتطابق الإجماليان.
        /// </summary>
        private void CrossCheckItemProfit(decimal summaryGrossProfit, ItemProfitabilityModel itemReport)
        {
            var difference = Math.Abs(itemReport.TotalProfit - summaryGrossProfit);

            if (difference <= 0.01m)
            {
                ItemProfitCrossCheckText =
                    $"تدقيق متقاطع: إجمالي ربح الأصناف ({FormatMoney(itemReport.TotalProfit)}) " +
                    $"يطابق الربح الإجمالي في ملخص الأرباح ({FormatMoney(summaryGrossProfit)}).";
                return;
            }

            ItemProfitCrossCheckText =
                $"تدقيق متقاطع: إجمالي ربح الأصناف ({FormatMoney(itemReport.TotalProfit)}) " +
                $"لا يطابق الربح الإجمالي في ملخص الأرباح ({FormatMoney(summaryGrossProfit)}) — الفرق {FormatMoney(difference)}. " +
                "السبب المحتمل: خصم مسجّل على فاتورة بإجمالي صفر، أو مرتجع بقيمة لا تساوي مجموع بنوده، أو بيانات فاتورة غير متسقة.";

            AppendDataWarning(
                $"تنبيه تعارض بيانات: إجمالي ربح الأصناف ({FormatMoney(itemReport.TotalProfit)}) " +
                $"لا يطابق الربح الإجمالي في ملخص الأرباح ({FormatMoney(summaryGrossProfit)}) — الفرق {FormatMoney(difference)}. " +
                "راجع تفصيل تبويب ربحية الأصناف.");
        }

        private void AppendDataWarning(string message)
        {
            HasDataWarning = true;
            DataWarningText = string.IsNullOrWhiteSpace(DataWarningText) ? message : $"{DataWarningText} {message}";
        }

        private void ApplyExpensesReport(ExpenseReportModel report)
        {
            ExpenseCategories.Clear();
            foreach (var row in report.Categories) ExpenseCategories.Add(row);

            ExpenseDetails.Clear();
            foreach (var row in report.Details) ExpenseDetails.Add(row);

            ExpenseTotalText = FormatMoney(report.Total);
            ExpenseCountText = $"{report.Count} مصروف نشط (المصروفات الملغاة مستبعدة)";

            ExpenseComparisonText = report.PreviousTotal is null
                ? "لا توجد فترة سابقة للمقارنة"
                : FormatComparison(report.PreviousTotal.Value, report.Change, report.ChangePercent);

            OnPropertyChanged(nameof(ShowExpensesEmptyState));
        }

        private void ApplyPurchasesReport(PurchaseReportModel report)
        {
            PurchasesDaily.Clear();
            foreach (var row in report.Daily) PurchasesDaily.Add(row);

            PurchasesBySupplier.Clear();
            foreach (var row in report.BySupplier) PurchasesBySupplier.Add(row);

            PurchasesTopProducts.Clear();
            foreach (var row in report.TopProducts) PurchasesTopProducts.Add(row);

            PurchasesTotalText = FormatMoney(report.Total);
            PurchasesInvoiceCountText = $"{report.InvoiceCount} فاتورة معتمدة";
            PurchasesDailyTotalText = FormatMoney(PurchasesDaily.Sum(r => r.Total));

            PurchasesComparisonText = report.PreviousTotal is null
                ? "لا توجد فترة سابقة للمقارنة"
                : FormatComparison(report.PreviousTotal.Value, report.Change, report.ChangePercent);

            HasPurchasesData = report.InvoiceCount > 0;
            PurchasesSupplierNoteText = report.HasSupplierData
                ? string.Empty
                : "لا توجد بيانات موردين مرتبطة بفواتير المشتريات المعتمدة في هذه الفترة، لذلك يظهر الإجمالي والفواتير دون تفصيل موردين.";

            OnPropertyChanged(nameof(ShowPurchasesEmptyState));
        }

        private void ApplyInventoryValuation(InventoryValuationModel report)
        {
            InventoryRows.Clear();
            foreach (var row in report.Rows) InventoryRows.Add(row);

            InventoryLowStock.Clear();
            foreach (var row in report.LowStock) InventoryLowStock.Add(row);

            InventoryOutOfStock.Clear();
            foreach (var row in report.OutOfStock) InventoryOutOfStock.Add(row);

            InventoryStagnant.Clear();
            foreach (var row in report.Stagnant) InventoryStagnant.Add(row);

            InventoryTopByValue.Clear();
            foreach (var row in report.TopByValue) InventoryTopByValue.Add(row);

            // التدقيق المتقاطع: البطاقة = مجموع (الكمية المتاحة × سعر التكلفة) لكل الأصناف
            var recomputed = InventoryRows.Sum(r => r.StockValue);
            InventoryTotalValueText = FormatMoney(report.TotalValue);

            InventoryProductCountText = $"{report.ProductCount} صنف";
            InventoryLowStockText = $"{report.LowStockCount} صنف";
            InventoryOutOfStockText = $"{report.OutOfStockCount} صنف";
            InventoryStagnantText = $"{report.StagnantCount} صنف";
            InventoryStateNoteText =
                $"هذا التقرير يعرض الحالة الحالية للمخزون ولا يتأثر بالمرشّح التاريخي (لحظة التوليد: {report.GeneratedAt:yyyy-MM-dd HH:mm}). " +
                $"قيمة المخزون = مجموع (الكمية المتاحة × سعر التكلفة الحالي). قيمة التالف (غير محتسبة): {FormatMoney(report.DamagedValue)}.";

            if (Math.Abs(recomputed - report.TotalValue) > 0.01m)
            {
                AppendDataWarning(
                    $"تنبيه تعارض بيانات: مجموع قيمة الأصناف ({FormatMoney(recomputed)}) " +
                    $"لا يطابق إجمالي قيمة المخزون ({FormatMoney(report.TotalValue)}).");
            }

            InventoryComparisonText =
                $"الراكد: أصناف عليها رصيد متاح وبلا مبيعات معتمدة خلال {report.StagnantDays} يوماً — عددها {report.StagnantCount}.";

            OnPropertyChanged(nameof(ShowInventoryEmptyState));
        }

        private void ApplyCustomerDebts(CustomerDebtReportModel report)
        {
            CustomerDebtRows.Clear();
            foreach (var row in report.Rows) CustomerDebtRows.Add(row);

            CustomerDebtTotalText = FormatMoney(report.TotalDebt);
            CustomerDebtCountText = $"{report.CustomerCount} عميل من إجمالي {report.TotalCustomers}";
            CustomerDebtNoteText = $"لا يتأثر هذا التقرير بالمرشّح التاريخي. {report.FormulaNote}";

            OnPropertyChanged(nameof(ShowCustomerDebtsEmptyState));
        }

        private void ApplyItemProfitability(ItemProfitabilityModel report)
        {
            ItemProfitRows.Clear();
            foreach (var row in report.Rows) ItemProfitRows.Add(row);

            ItemProfitBest.Clear();
            foreach (var row in report.Best) ItemProfitBest.Add(row);

            ItemProfitWorst.Clear();
            foreach (var row in report.Worst) ItemProfitWorst.Add(row);

            ItemProfitSalesText = FormatMoney(report.TotalSalesValue);
            ItemProfitCostText = FormatMoney(report.TotalCost);
            ItemProfitTotalText = FormatMoney(report.TotalProfit);
            ItemProfitMarginText = report.TotalMarginPercent.HasValue ? $"{report.TotalMarginPercent.Value:N1}٪" : "—";

            ItemProfitNoteText = report.IsCogsEstimated
                ? $"التكلفة تقديرية جزئياً: {report.EstimatedLineCount} بند في {report.EstimatedInvoiceCount} فاتورة قديمة لا تُخزّن تكلفة الوحدة وقت البيع، لذا تُحتسب تكلفتها بسعر التكلفة الحالي للأصناف."
                : string.Empty;

            OnPropertyChanged(nameof(ShowItemProfitEmptyState));
        }

        /// <summary>يُحدّث تبويب تقييم المخزون وحده عند تغيير عدد أيام الركود (لا يحتاج مرشّحاً تاريخياً).</summary>
        private async Task LoadInventoryValuationAsync()
        {
            if (!_hasLoadedOnce) return;

            var token = _loadToken;

            try
            {
                var report = await _reportsService.GetInventoryValuationAsync(StagnantDays, 10);
                if (token != _loadToken) return;

                ApplyInventoryValuation(report);
                SetStatus($"تم تحديث تقييم المخزون — الأصناف الراكدة: بلا مبيعات خلال {StagnantDays} يوماً.");
            }
            catch (Exception ex)
            {
                if (token != _loadToken) return;
                SetStatus($"تعذر تحديث تقييم المخزون: {ex.Message}", error: true);
            }
        }

        private void ClearAll()
        {
            HasProfitData = false;
            DailySales.Clear();
            TopProductsByQuantity.Clear();
            TopProductsByValue.Clear();
            CustomerSales.Clear();
            ExpenseCategories.Clear();
            ExpenseDetails.Clear();
            DailySalesTotalText = FormatMoney(0m);
            ExpenseTotalText = FormatMoney(0m);
            ExpenseCountText = "0 مصروف";

            PurchasesDaily.Clear();
            PurchasesBySupplier.Clear();
            PurchasesTopProducts.Clear();
            InventoryRows.Clear();
            InventoryLowStock.Clear();
            InventoryOutOfStock.Clear();
            InventoryStagnant.Clear();
            InventoryTopByValue.Clear();
            CustomerDebtRows.Clear();
            ItemProfitRows.Clear();
            ItemProfitBest.Clear();
            ItemProfitWorst.Clear();

            OnPropertyChanged(nameof(ShowSalesEmptyState));
            OnPropertyChanged(nameof(ShowExpensesEmptyState));
            OnPropertyChanged(nameof(ShowPurchasesEmptyState));
            OnPropertyChanged(nameof(ShowInventoryEmptyState));
            OnPropertyChanged(nameof(ShowCustomerDebtsEmptyState));
            OnPropertyChanged(nameof(ShowItemProfitEmptyState));
        }

        private void SetStatus(string message, bool error = false)
        {
            IsStatusError = error;
            StatusMessage = message;
        }

        // ── أدوات التنسيق ─────────────────────────────────────────────────────

        private static string FormatMoney(decimal value) => $"{value:N2} ج.م";

        private static string FormatComparison(decimal previousAmount, decimal change, decimal? percentChange)
        {
            var arrow = change > 0m ? "▲" : change < 0m ? "▼" : "=";
            var percentText = percentChange.HasValue
                ? $"{arrow} {Math.Abs(percentChange.Value):N1}٪"
                : arrow;

            return $"الفترة السابقة: {FormatMoney(previousAmount)} ({percentText})";
        }
    }
}
