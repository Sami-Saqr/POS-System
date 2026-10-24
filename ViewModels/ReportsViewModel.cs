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

                await Task.WhenAll(summaryTask, dailyTask, topTask, customersTask, expensesTask);

                if (token != _loadToken) return;

                ApplySummary(summaryTask.Result);
                ApplySalesReport(dailyTask.Result, topTask.Result, customersTask.Result);
                ApplyExpensesReport(expensesTask.Result);
                CrossCheckDailyTotal(summaryTask.Result.NetSales);

                PeriodText = $"الفترة: من {from:yyyy-MM-dd} إلى {toExclusive.AddDays(-1):yyyy-MM-dd}";

                SetStatus($"تم تحديث التقارير — فواتير البيع المعتمدة: {summaryTask.Result.InvoiceCount} — مصروفات الفترة: {ExpenseTotalText}");
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
                ? "التكلفة تقديرية: بنود فواتير البيع لا تُخزّن تكلفة الوحدة وقت البيع، لذا تُحتسب تكلفة البضاعة المبيعة بسعر التكلفة الحالي للأصناف."
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
                HasDataWarning = true;
                DataWarningText =
                    $"تنبيه تعارض بيانات: مجموع الصافي في التقرير اليومي ({FormatMoney(dailyNet)}) " +
                    $"لا يطابق بطاقة صافي المبيعات ({FormatMoney(summaryNetSales)}) — الفرق {FormatMoney(difference)}.";
            }
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
            OnPropertyChanged(nameof(ShowSalesEmptyState));
            OnPropertyChanged(nameof(ShowExpensesEmptyState));
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
