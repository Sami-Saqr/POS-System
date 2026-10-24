using System.Collections.ObjectModel;
using System.Windows;
using CarPartManager.Models;
using CarPartManager.MVVM;
using CarPartManager.Services;
using CarPartManager.Views;

namespace CarPartManager.ViewModels
{
    /// <summary>
    /// نموذج عرض شاشة المصروفات (Expenses ViewModel).
    /// كل الاستعلامات تُفلتر في قاعدة البيانات (AsNoTracking) والمصروفات الملغاة مستبعدة من جميع الإجماليات.
    /// المصروفات وحدة مستقلة تماماً لا تؤثر على المخزون أو العملاء أو فواتير البيع.
    /// </summary>
    public class ExpensesViewModel : ViewModelBase
    {
        private readonly IExpenseService _expenseService;
        private readonly string? _currentUser;

        /// <summary>يمنع الكتابة فوق نتائج بحث قديمة عند تتابع تغييرات الفلاتر بسرعة.</summary>
        private int _loadToken;

        /// <summary>يمنع إعادة التحميل أثناء إعادة بناء قوائم الفلاتر.</summary>
        private bool _suppressFilterReload;

        public ExpensesViewModel(IExpenseService? expenseService = null, string? currentUser = null)
        {
            _expenseService = expenseService ?? new SqliteExpenseService();
            _currentUser = currentUser;

            RefreshCommand = new RelayCommand(() => _ = LoadAllDataAsync());
            ApplyFiltersCommand = new RelayCommand(() => _ = LoadExpensesAsync());
            ClearFiltersCommand = new RelayCommand(ExecuteClearFilters);
            AddExpenseCommand = new RelayCommand(ExecuteAddExpense);
            EditExpenseCommand = new RelayCommand(
                param => ExecuteEditExpense(param as ExpenseModel),
                param => (param as ExpenseModel ?? SelectedExpense) != null);
            CancelExpenseCommand = new RelayCommand(
                param => ExecuteCancelExpense(param as ExpenseModel),
                param => (param as ExpenseModel ?? SelectedExpense) is { IsCancelled: false });
            ViewHistoryCommand = new RelayCommand(
                param => ExecuteViewHistory(param as ExpenseModel),
                param => (param as ExpenseModel ?? SelectedExpense) != null);
            ToggleCategoryPanelCommand = new RelayCommand(() => IsCategoryPanelOpen = !IsCategoryPanelOpen);
            ToggleHistoryPanelCommand = new RelayCommand(() => IsHistoryPanelOpen = !IsHistoryPanelOpen);
            AddCategoryCommand = new RelayCommand(() => _ = AddCategoryAsync(), () => !IsCategoryBusy);
            ToggleCategoryStatusCommand = new RelayCommand(param => _ = ToggleCategoryStatusAsync(param as ExpenseCategoryModel), param => param is ExpenseCategoryModel);
        }

        // ── الأوامر ────────────────────────────────────────────────────────────

        public RelayCommand RefreshCommand { get; }
        public RelayCommand ApplyFiltersCommand { get; }
        public RelayCommand ClearFiltersCommand { get; }
        public RelayCommand AddExpenseCommand { get; }
        public RelayCommand EditExpenseCommand { get; }
        public RelayCommand CancelExpenseCommand { get; }
        public RelayCommand ViewHistoryCommand { get; }
        public RelayCommand ToggleCategoryPanelCommand { get; }
        public RelayCommand ToggleHistoryPanelCommand { get; }
        public RelayCommand AddCategoryCommand { get; }
        public RelayCommand ToggleCategoryStatusCommand { get; }

        // ── البيانات ───────────────────────────────────────────────────────────

        public ObservableCollection<ExpenseModel> Expenses { get; } = new();
        public ObservableCollection<ExpenseHistoryModel> ExpenseHistory { get; } = new();
        public ObservableCollection<ExpenseCategoryModel> Categories { get; } = new();
        public ObservableCollection<ExpenseCategoryModel> CategoryFilters { get; } = new();

        public List<string> PaymentMethodFilters { get; } = new() { "الكل", "نقدي", "شبكة / فيزا", "تحويل بنكي", "شيك" };

        private ExpenseModel? _selectedExpense;
        public ExpenseModel? SelectedExpense
        {
            get => _selectedExpense;
            set
            {
                if (SetProperty(ref _selectedExpense, value))
                {
                    OnPropertyChanged(nameof(HasSelectedExpense));
                    ExpenseHistory.Clear();
                    EditExpenseCommand.RaiseCanExecuteChanged();
                    CancelExpenseCommand.RaiseCanExecuteChanged();
                    ViewHistoryCommand.RaiseCanExecuteChanged();
                    if (value != null) _ = LoadHistoryAsync();
                }
            }
        }

        public bool HasSelectedExpense => SelectedExpense != null;

        // ── الفلاتر ────────────────────────────────────────────────────────────

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) ReloadOnFilterChange(); }
        }

        private ExpenseCategoryModel? _selectedCategoryFilter;
        public ExpenseCategoryModel? SelectedCategoryFilter
        {
            get => _selectedCategoryFilter;
            set { if (SetProperty(ref _selectedCategoryFilter, value)) ReloadOnFilterChange(); }
        }

        private DateTime? _fromDate;
        public DateTime? FromDate
        {
            get => _fromDate;
            set { if (SetProperty(ref _fromDate, value)) ReloadOnFilterChange(); }
        }

        private DateTime? _toDate;
        public DateTime? ToDate
        {
            get => _toDate;
            set { if (SetProperty(ref _toDate, value)) ReloadOnFilterChange(); }
        }

        private string _selectedPaymentMethod = "الكل";
        public string SelectedPaymentMethod
        {
            get => _selectedPaymentMethod;
            set { if (SetProperty(ref _selectedPaymentMethod, value)) ReloadOnFilterChange(); }
        }

        private bool _includeCancelled;
        public bool IncludeCancelled
        {
            get => _includeCancelled;
            set { if (SetProperty(ref _includeCancelled, value)) ReloadOnFilterChange(); }
        }

        private void ReloadOnFilterChange()
        {
            if (_suppressFilterReload) return;
            _ = LoadExpensesAsync();
        }

        // ── الملخص والإجماليات ─────────────────────────────────────────────────

        private string _todayTotalText = "0.00 ج.م";
        public string TodayTotalText { get => _todayTotalText; set => SetProperty(ref _todayTotalText, value); }

        private string _currentMonthTotalText = "0.00 ج.م";
        public string CurrentMonthTotalText { get => _currentMonthTotalText; set => SetProperty(ref _currentMonthTotalText, value); }

        private string _previousMonthTotalText = "0.00 ج.م";
        public string PreviousMonthTotalText { get => _previousMonthTotalText; set => SetProperty(ref _previousMonthTotalText, value); }

        private string _topCategoryText = "—";
        public string TopCategoryText { get => _topCategoryText; set => SetProperty(ref _topCategoryText, value); }

        private string _currentMonthCountText = "0 مصروف";
        public string CurrentMonthCountText { get => _currentMonthCountText; set => SetProperty(ref _currentMonthCountText, value); }

        private string _filteredTotalText = "0.00 ج.م";
        public string FilteredTotalText { get => _filteredTotalText; set => SetProperty(ref _filteredTotalText, value); }

        private string _filteredCountText = "0 مصروف";
        public string FilteredCountText { get => _filteredCountText; set => SetProperty(ref _filteredCountText, value); }

        // ── إدارة التصنيفات ────────────────────────────────────────────────────

        private bool _isCategoryPanelOpen;
        public bool IsCategoryPanelOpen
        {
            get => _isCategoryPanelOpen;
            set => SetProperty(ref _isCategoryPanelOpen, value);
        }

        private bool _isHistoryPanelOpen;
        /// <summary>لوحة سجل التدقيق (تعديلات/إلغاءات) للمصروف المحدد.</summary>
        public bool IsHistoryPanelOpen
        {
            get => _isHistoryPanelOpen;
            set => SetProperty(ref _isHistoryPanelOpen, value);
        }

        private string _newCategoryName = string.Empty;
        public string NewCategoryName
        {
            get => _newCategoryName;
            set => SetProperty(ref _newCategoryName, value);
        }

        private bool _isCategoryBusy;
        public bool IsCategoryBusy
        {
            get => _isCategoryBusy;
            set
            {
                if (SetProperty(ref _isCategoryBusy, value))
                    AddCategoryCommand.RaiseCanExecuteChanged();
            }
        }

        // ── الحالة ─────────────────────────────────────────────────────────────

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                    OnPropertyChanged(nameof(ShowEmptyState));
            }
        }

        public bool ShowEmptyState => !IsLoading && Expenses.Count == 0;

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

        private void SetStatus(string message, bool error = false)
        {
            IsStatusError = error;
            StatusMessage = message;
        }

        // ── التحميل ────────────────────────────────────────────────────────────

        public async Task LoadAllDataAsync()
        {
            IsLoading = true;
            try
            {
                SetStatus(string.Empty);
                await LoadCategoriesAsync();
                await LoadSummaryAsync();
                await LoadExpensesAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"تعذر تحميل شاشة المصروفات: {ex.Message}", error: true);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadSummaryAsync()
        {
            var summary = await _expenseService.GetSummaryAsync();
            TodayTotalText = $"{summary.TodayTotal:N2} ج.م";
            CurrentMonthTotalText = $"{summary.CurrentMonthTotal:N2} ج.م";
            PreviousMonthTotalText = $"{summary.PreviousMonthTotal:N2} ج.م";
            TopCategoryText = summary.TopCategoryTotal > 0
                ? $"{summary.TopCategoryName} ({summary.TopCategoryTotal:N2} ج.م)"
                : "—";
            CurrentMonthCountText = $"{summary.CurrentMonthCount} مصروف";
        }

        private async Task LoadCategoriesAsync()
        {
            var categories = (await _expenseService.GetCategoriesAsync(true)).ToList();

            Categories.Clear();
            foreach (var category in categories) Categories.Add(category);

            _suppressFilterReload = true;
            try
            {
                var previousId = SelectedCategoryFilter?.Id ?? 0;
                CategoryFilters.Clear();
                CategoryFilters.Add(new ExpenseCategoryModel { Id = 0, Name = "كل التصنيفات" });
                foreach (var category in Categories) CategoryFilters.Add(category);
                SelectedCategoryFilter = CategoryFilters.FirstOrDefault(c => c.Id == previousId) ?? CategoryFilters[0];
            }
            finally
            {
                _suppressFilterReload = false;
            }
        }

        public async Task LoadExpensesAsync(bool showLoading = false)
        {
            if (FromDate.HasValue && ToDate.HasValue && FromDate.Value.Date > ToDate.Value.Date)
            {
                SetStatus("تاريخ البداية يجب أن يكون قبل تاريخ النهاية.", error: true);
                return;
            }

            var token = ++_loadToken;
            if (showLoading) IsLoading = true;

            try
            {
                var categoryId = SelectedCategoryFilter?.Id ?? 0;
                var paymentMethod = SelectedPaymentMethod == "الكل" ? null : SelectedPaymentMethod;

                var data = (await _expenseService.GetExpensesAsync(
                    FromDate,
                    ToDate,
                    categoryId,
                    paymentMethod,
                    IncludeCancelled,
                    SearchText)).ToList();

                if (token != _loadToken) return; // نتائج قديمة — تجاهل

                Expenses.Clear();
                foreach (var expense in data) Expenses.Add(expense);

                // إجمالي النتائج المفلترة — للمصروفات النشطة فقط (الملغاة لا تدخل في أي إجمالي)
                var activeTotal = Expenses.Where(e => !e.IsCancelled).Sum(e => e.Amount);
                var activeCount = Expenses.Count(e => !e.IsCancelled);
                FilteredTotalText = $"{activeTotal:N2} ج.م";
                FilteredCountText = $"{activeCount} مصروف نشط";

                OnPropertyChanged(nameof(ShowEmptyState));
                SetStatus($"تم تحميل {Expenses.Count} مصروف — إجمالي النشط منها {FilteredTotalText}.");
            }
            catch (Exception ex)
            {
                if (token != _loadToken) return;
                Expenses.Clear();
                OnPropertyChanged(nameof(ShowEmptyState));
                SetStatus($"تعذر تحميل قائمة المصروفات: {ex.Message}", error: true);
            }
            finally
            {
                if (showLoading && token == _loadToken) IsLoading = false;
            }
        }

        private async Task LoadHistoryAsync()
        {
            var selected = SelectedExpense;
            if (selected == null) return;

            try
            {
                var history = await _expenseService.GetExpenseHistoryAsync(selected.Id);
                ExpenseHistory.Clear();
                foreach (var item in history) ExpenseHistory.Add(item);
            }
            catch (Exception ex)
            {
                ExpenseHistory.Clear();
                SetStatus($"تعذر تحميل سجل تعديلات المصروف: {ex.Message}", error: true);
            }
        }

        // ── الإجراءات ──────────────────────────────────────────────────────────

        private void ExecuteClearFilters()
        {
            _suppressFilterReload = true;
            try
            {
                SearchText = string.Empty;
                FromDate = null;
                ToDate = null;
                SelectedPaymentMethod = "الكل";
                IncludeCancelled = false;
                SelectedCategoryFilter = CategoryFilters.FirstOrDefault();
            }
            finally
            {
                _suppressFilterReload = false;
            }

            _ = LoadExpensesAsync();
        }

        private void ExecuteAddExpense()
        {
            var activeCategories = Categories.Where(c => c.IsActive).OrderBy(c => c.Name).ToList();
            if (activeCategories.Count == 0)
            {
                SetStatus("لا يوجد تصنيف مفعّل — يرجى إضافة تصنيف أولاً من زر «إدارة التصنيفات».", error: true);
                return;
            }

            var dialogVm = new ExpenseDialogViewModel(activeCategories, null, _currentUser);
            var dialog = new ExpenseDialogWindow(dialogVm) { Owner = Application.Current.MainWindow };

            if (dialog.ShowDialog() == true)
                _ = SaveExpenseAsync(dialogVm.ResultExpense, isNew: true);
        }

        private void ExecuteEditExpense(ExpenseModel? expense)
        {
            var selected = expense ?? SelectedExpense;
            if (selected == null)
            {
                SetStatus("يرجى اختيار مصروف من الجدول أولاً.", error: true);
                return;
            }

            SelectedExpense = selected;

            if (selected.IsCancelled)
            {
                SetStatus("لا يمكن تعديل مصروف ملغى — يمكنك عرض سجل تدقيقه فقط.", error: true);
                return;
            }

            var categoriesForEdit = Categories.Where(c => c.IsActive || c.Id == selected.CategoryId)
                                              .ToList();

            var dialogVm = new ExpenseDialogViewModel(categoriesForEdit, selected, _currentUser);
            var dialog = new ExpenseDialogWindow(dialogVm) { Owner = Application.Current.MainWindow };

            if (dialog.ShowDialog() == true)
                _ = SaveExpenseAsync(dialogVm.ResultExpense, isNew: false);
        }

        private void ExecuteCancelExpense(ExpenseModel? expense)
        {
            var selected = expense ?? SelectedExpense;
            if (selected == null)
            {
                SetStatus("يرجى اختيار مصروف من الجدول أولاً.", error: true);
                return;
            }

            SelectedExpense = selected;

            if (selected.IsCancelled)
            {
                SetStatus("هذا المصروف ملغى بالفعل.", error: true);
                return;
            }

            var summary = $"{selected.ExpenseDateText} — {selected.CategoryName} — {selected.AmountText}" +
                          (string.IsNullOrWhiteSpace(selected.Description) ? string.Empty : $" — {selected.Description}");

            var dialogVm = new CancelExpenseDialogViewModel(summary);
            var dialog = new CancelExpenseDialogWindow(dialogVm) { Owner = Application.Current.MainWindow };

            if (dialog.ShowDialog() == true)
                _ = CancelExpenseAsync(selected.Id, dialogVm.ResultReason);
        }

        private void ExecuteViewHistory(ExpenseModel? expense)
        {
            var selected = expense ?? SelectedExpense;
            if (selected == null)
            {
                SetStatus("يرجى اختيار مصروف من الجدول أولاً.", error: true);
                return;
            }

            SelectedExpense = selected;
            IsHistoryPanelOpen = true;
            _ = LoadHistoryAsync();
        }

        private async Task SaveExpenseAsync(ExpenseModel expense, bool isNew)
        {
            try
            {
                if (isNew)
                {
                    await _expenseService.AddExpenseAsync(expense);
                    SetStatus("تم حفظ المصروف بنجاح.");
                }
                else
                {
                    var updated = await _expenseService.UpdateExpenseAsync(expense);
                    SetStatus(updated
                        ? "تم تحديث المصروف بنجاح، مع الاحتفاظ بالقيم السابقة في سجل التدقيق."
                        : "لم يتم العثور على المصروف المطلوب تعديله.", error: !updated);
                }

                await LoadAllDataAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"فشل حفظ المصروف: {ex.Message}", error: true);
            }
        }

        private async Task CancelExpenseAsync(int expenseId, string reason)
        {
            try
            {
                var cancelled = await _expenseService.CancelExpenseAsync(expenseId, reason, _currentUser);
                SetStatus(cancelled
                    ? "تم إلغاء المصروف مع الاحتفاظ بسجله الكامل — لن يدخل في أي إجمالي."
                    : "لم يتم العثور على المصروف المطلوب إلغاؤه.", error: !cancelled);

                await LoadAllDataAsync();
            }
            catch (Exception ex)
            {
                SetStatus($"فشل إلغاء المصروف: {ex.Message}", error: true);
            }
        }

        private async Task AddCategoryAsync()
        {
            var name = NewCategoryName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                SetStatus("يرجى كتابة اسم التصنيف الجديد.", error: true);
                return;
            }

            IsCategoryBusy = true;
            try
            {
                await _expenseService.AddCategoryAsync(name);
                NewCategoryName = string.Empty;
                await LoadCategoriesAsync();
                SetStatus($"تمت إضافة التصنيف «{name}» بنجاح.");
            }
            catch (Exception ex)
            {
                SetStatus($"تعذر إضافة التصنيف: {ex.Message}", error: true);
            }
            finally
            {
                IsCategoryBusy = false;
            }
        }

        private async Task ToggleCategoryStatusAsync(ExpenseCategoryModel? category)
        {
            if (category == null) return;

            try
            {
                var targetState = !category.IsActive;
                var updated = await _expenseService.SetCategoryActiveAsync(category.Id, targetState);
                if (!updated)
                {
                    SetStatus("لم يتم العثور على التصنيف المطلوب.", error: true);
                    return;
                }

                await LoadCategoriesAsync();
                SetStatus(targetState
                    ? $"تم تفعيل التصنيف «{category.Name}»."
                    : $"تم تعطيل التصنيف «{category.Name}» — السجلات المرتبطة به محفوظة كما هي.");
            }
            catch (Exception ex)
            {
                SetStatus($"تعذر تحديث حالة التصنيف: {ex.Message}", error: true);
            }
        }
    }
}
