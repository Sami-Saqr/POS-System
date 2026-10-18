using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using CarPartManager.Models;
using CarPartManager.MVVM;
using CarPartManager.Services;

namespace CarPartManager.ViewModels
{
    /// <summary>
    /// نموذج عرض شاشة إدارة العملاء (CustomersViewModel)
    /// يدعم: قائمة العملاء مع البحث، الإضافة/التعديل عبر لوحة جانبية،
    /// التعطيل/التفعيل، كشف الحساب، وتسجيل الدفعات.
    /// </summary>
    public class CustomersViewModel : ViewModelBase
    {
        // ── الأوضاع الداخلية للشاشة ───────────────────────────────────────────
        public enum ScreenMode { List, Statement, Payment }

        private readonly ICustomerService _customerService;
        private readonly List<CustomerModel> _allCustomers = new();

        // ══════════════════════════════════════════════════════════════════════
        // قائمة العملاء
        // ══════════════════════════════════════════════════════════════════════

        public ObservableCollection<CustomerModel> FilteredCustomers { get; } = new();

        private CustomerModel? _selectedCustomer;
        public CustomerModel? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (SetProperty(ref _selectedCustomer, value))
                {
                    OnPropertyChanged(nameof(HasSelectedCustomer));
                    if (value != null) PopulateForm(value);
                }
            }
        }

        public bool HasSelectedCustomer => _selectedCustomer != null;

        // ── بحث وفلترة ────────────────────────────────────────────────────────

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) FilterCustomers(); }
        }

        private string _filterMode = "all"; // "all" | "active" | "has_balance"
        public string FilterMode
        {
            get => _filterMode;
            set { if (SetProperty(ref _filterMode, value)) FilterCustomers(); }
        }

        // ── حالة التحميل ──────────────────────────────────────────────────────

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { if (SetProperty(ref _isLoading, value)) OnPropertyChanged(nameof(ShowEmpty)); }
        }

        public bool ShowEmpty => !IsLoading && FilteredCustomers.Count == 0;

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private bool _isStatusError;
        public bool IsStatusError
        {
            get => _isStatusError;
            set { if (SetProperty(ref _isStatusError, value)) OnPropertyChanged(nameof(StatusColor)); }
        }

        public string StatusColor => _isStatusError ? "#DC2626" : "#2563EB";

        // ══════════════════════════════════════════════════════════════════════
        // نموذج الإضافة / التعديل
        // ══════════════════════════════════════════════════════════════════════

        private string _formName = string.Empty;
        public string FormName
        {
            get => _formName;
            set => SetProperty(ref _formName, value);
        }

        private string _formPhone = string.Empty;
        public string FormPhone
        {
            get => _formPhone;
            set => SetProperty(ref _formPhone, value);
        }

        private string _formAddress = string.Empty;
        public string FormAddress
        {
            get => _formAddress;
            set => SetProperty(ref _formAddress, value);
        }

        private string _formNotes = string.Empty;
        public string FormNotes
        {
            get => _formNotes;
            set => SetProperty(ref _formNotes, value);
        }

        private string _formOpeningBalanceText = "0";
        public string FormOpeningBalanceText
        {
            get => _formOpeningBalanceText;
            set => SetProperty(ref _formOpeningBalanceText, value);
        }

        private bool _formIsActive = true;
        public bool FormIsActive
        {
            get => _formIsActive;
            set => SetProperty(ref _formIsActive, value);
        }

        // ══════════════════════════════════════════════════════════════════════
        // كشف الحساب
        // ══════════════════════════════════════════════════════════════════════

        private CustomerModel? _statementCustomer;
        public CustomerModel? StatementCustomer
        {
            get => _statementCustomer;
            set => SetProperty(ref _statementCustomer, value);
        }

        public ObservableCollection<CustomerStatementLineModel> StatementLines { get; } = new();

        private DateTime _statementFrom = DateTime.Now.AddMonths(-3);
        public DateTime StatementFrom
        {
            get => _statementFrom;
            set { if (SetProperty(ref _statementFrom, value)) _ = LoadStatementAsync(); }
        }

        private DateTime _statementTo = DateTime.Now;
        public DateTime StatementTo
        {
            get => _statementTo;
            set { if (SetProperty(ref _statementTo, value)) _ = LoadStatementAsync(); }
        }

        private decimal _statementTotalDebit;
        public decimal StatementTotalDebit
        {
            get => _statementTotalDebit;
            set => SetProperty(ref _statementTotalDebit, value);
        }

        private decimal _statementTotalCredit;
        public decimal StatementTotalCredit
        {
            get => _statementTotalCredit;
            set => SetProperty(ref _statementTotalCredit, value);
        }

        private decimal _statementFinalBalance;
        public decimal StatementFinalBalance
        {
            get => _statementFinalBalance;
            set => SetProperty(ref _statementFinalBalance, value);
        }

        // ══════════════════════════════════════════════════════════════════════
        // تسجيل الدفعة
        // ══════════════════════════════════════════════════════════════════════

        private string _paymentAmountText = string.Empty;
        public string PaymentAmountText
        {
            get => _paymentAmountText;
            set => SetProperty(ref _paymentAmountText, value);
        }

        private DateTime _paymentDate = DateTime.Now;
        public DateTime PaymentDate
        {
            get => _paymentDate;
            set => SetProperty(ref _paymentDate, value);
        }

        private string _paymentMethod = "نقدي";
        public string PaymentMethod
        {
            get => _paymentMethod;
            set => SetProperty(ref _paymentMethod, value);
        }

        private string _paymentNotes = string.Empty;
        public string PaymentNotes
        {
            get => _paymentNotes;
            set => SetProperty(ref _paymentNotes, value);
        }

        public List<string> PaymentMethods { get; } = new() { "نقدي", "شبكة / فيزا", "تحويل بنكي", "شيك" };

        // ══════════════════════════════════════════════════════════════════════
        // وضع الشاشة (قائمة / كشف / دفعة)
        // ══════════════════════════════════════════════════════════════════════

        private ScreenMode _currentMode = ScreenMode.List;
        public ScreenMode CurrentMode
        {
            get => _currentMode;
            set
            {
                if (SetProperty(ref _currentMode, value))
                {
                    OnPropertyChanged(nameof(IsListMode));
                    OnPropertyChanged(nameof(IsStatementMode));
                    OnPropertyChanged(nameof(IsPaymentMode));
                }
            }
        }

        public bool IsListMode => CurrentMode == ScreenMode.List;
        public bool IsStatementMode => CurrentMode == ScreenMode.Statement;
        public bool IsPaymentMode => CurrentMode == ScreenMode.Payment;

        // ══════════════════════════════════════════════════════════════════════
        // الأوامر
        // ══════════════════════════════════════════════════════════════════════

        public RelayCommand SaveCustomerCommand { get; }
        public RelayCommand ClearFormCommand { get; }
        public RelayCommand ToggleActiveCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand ShowStatementCommand { get; }
        public RelayCommand ShowPaymentCommand { get; }
        public RelayCommand BackToListCommand { get; }
        public RelayCommand SavePaymentCommand { get; }
        public RelayCommand FilterAllCommand { get; }
        public RelayCommand FilterActiveCommand { get; }
        public RelayCommand FilterBalanceCommand { get; }

        // ══════════════════════════════════════════════════════════════════════
        // البناء
        // ══════════════════════════════════════════════════════════════════════

        public CustomersViewModel(ICustomerService? customerService = null)
        {
            _customerService = customerService ?? new SqliteCustomerService();

            SaveCustomerCommand = new RelayCommand(async () => await ExecuteSaveCustomerAsync());
            ClearFormCommand = new RelayCommand(ClearForm);
            ToggleActiveCommand = new RelayCommand(
                async () => await ExecuteToggleActiveAsync(),
                () => HasSelectedCustomer);
            RefreshCommand = new RelayCommand(async () => await LoadCustomersAsync());
            ShowStatementCommand = new RelayCommand(
                async () => await ExecuteShowStatementAsync(),
                () => HasSelectedCustomer);
            ShowPaymentCommand = new RelayCommand(
                () => ExecuteShowPayment(),
                () => HasSelectedCustomer);
            BackToListCommand = new RelayCommand(() => CurrentMode = ScreenMode.List);
            SavePaymentCommand = new RelayCommand(async () => await ExecuteSavePaymentAsync());
            FilterAllCommand = new RelayCommand(() => FilterMode = "all");
            FilterActiveCommand = new RelayCommand(() => FilterMode = "active");
            FilterBalanceCommand = new RelayCommand(() => FilterMode = "has_balance");

            _ = LoadCustomersAsync();
        }

        // ══════════════════════════════════════════════════════════════════════
        // تحميل العملاء
        // ══════════════════════════════════════════════════════════════════════

        public async Task LoadCustomersAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = string.Empty;

                var customers = await _customerService.GetAllCustomersAsync();
                _allCustomers.Clear();
                _allCustomers.AddRange(customers);
                FilterCustomers();
            }
            catch (Exception ex)
            {
                SetStatus($"خطأ في تحميل العملاء: {ex.Message}", error: true);
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(ShowEmpty));
            }
        }

        private void FilterCustomers()
        {
            FilteredCustomers.Clear();

            var q = SearchText?.Trim().ToLower() ?? string.Empty;

            var filtered = _allCustomers.AsEnumerable();

            // بحث نصي
            if (!string.IsNullOrWhiteSpace(q))
            {
                filtered = filtered.Where(c =>
                    c.Name.ToLower().Contains(q) ||
                    (c.Phone?.ToLower().Contains(q) ?? false));
            }

            // فلتر الحالة
            filtered = FilterMode switch
            {
                "active" => filtered.Where(c => c.IsActive),
                "has_balance" => filtered.Where(c => c.CurrentBalance > 0),
                _ => filtered
            };

            foreach (var c in filtered) FilteredCustomers.Add(c);
            OnPropertyChanged(nameof(ShowEmpty));
        }

        // ══════════════════════════════════════════════════════════════════════
        // تعبئة ومسح النموذج
        // ══════════════════════════════════════════════════════════════════════

        private void PopulateForm(CustomerModel c)
        {
            FormName = c.Name;
            FormPhone = c.Phone ?? string.Empty;
            FormAddress = c.Address ?? string.Empty;
            FormNotes = c.Notes ?? string.Empty;
            FormOpeningBalanceText = c.OpeningBalance.ToString("F2");
            FormIsActive = c.IsActive;
        }

        private void ClearForm()
        {
            SelectedCustomer = null;
            FormName = string.Empty;
            FormPhone = string.Empty;
            FormAddress = string.Empty;
            FormNotes = string.Empty;
            FormOpeningBalanceText = "0";
            FormIsActive = true;
            StatusMessage = string.Empty;
            IsStatusError = false;
        }

        // ══════════════════════════════════════════════════════════════════════
        // حفظ العميل (إضافة أو تعديل)
        // ══════════════════════════════════════════════════════════════════════

        private async Task ExecuteSaveCustomerAsync()
        {
            // ── التحقق من الاسم ───────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(FormName))
            {
                SetStatus("يرجى إدخال اسم العميل — هذا الحقل مطلوب.", error: true);
                return;
            }

            // ── التحقق من رقم الهاتف (تحذير فقط عند التكرار) ─────────────────
            if (!string.IsNullOrWhiteSpace(FormPhone))
            {
                // تحقق شكلي بسيط
                if (!IsValidPhoneLoose(FormPhone))
                {
                    SetStatus("رقم الهاتف يبدو غير صحيح — يُرجى التحقق من الأرقام.", error: true);
                    return;
                }

                // تحذير من تكرار الهاتف (لا يمنع الحفظ)
                var excludeId = SelectedCustomer?.Id ?? 0;
                var phoneExists = await _customerService.PhoneExistsAsync(FormPhone, excludeId);
                if (phoneExists)
                {
                    var proceed = MessageBox.Show(
                        $"رقم الهاتف '{FormPhone}' مسجل بالفعل لعميل آخر.\nهل تريد المتابعة؟",
                        "تنبيه: هاتف مكرر",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (proceed != MessageBoxResult.Yes) return;
                }
            }

            // ── التحقق من الرصيد الافتتاحي ───────────────────────────────────
            if (!decimal.TryParse(FormOpeningBalanceText, out var openingBalance) || openingBalance < 0)
            {
                SetStatus("الرصيد الافتتاحي يجب أن يكون رقماً صحيحاً (0 أو أكثر).", error: true);
                return;
            }

            try
            {
                if (SelectedCustomer == null)
                {
                    // ── إضافة جديد ────────────────────────────────────────────
                    var newCustomer = new CustomerModel
                    {
                        Name = FormName.Trim(),
                        Phone = FormPhone.Trim(),
                        Address = FormAddress.Trim(),
                        Notes = FormNotes.Trim(),
                        OpeningBalance = openingBalance,
                        IsActive = FormIsActive
                    };
                    await _customerService.AddCustomerAsync(newCustomer);
                    SetStatus($"تم حفظ العميل '{newCustomer.Name}' بنجاح.", error: false);
                }
                else
                {
                    // ── تعديل موجود ──────────────────────────────────────────
                    SelectedCustomer.Name = FormName.Trim();
                    SelectedCustomer.Phone = FormPhone.Trim();
                    SelectedCustomer.Address = FormAddress.Trim();
                    SelectedCustomer.Notes = FormNotes.Trim();
                    SelectedCustomer.OpeningBalance = openingBalance;
                    SelectedCustomer.IsActive = FormIsActive;

                    await _customerService.UpdateCustomerAsync(SelectedCustomer);
                    SetStatus($"تم تحديث بيانات العميل '{SelectedCustomer.Name}' بنجاح.", error: false);
                }

                await LoadCustomersAsync();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"حدث خطأ أثناء حفظ بيانات العميل:\n{ex.Message}",
                    "خطأ في الحفظ",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // تعطيل / تفعيل العميل
        // ══════════════════════════════════════════════════════════════════════

        private async Task ExecuteToggleActiveAsync()
        {
            if (SelectedCustomer == null) return;

            var newState = !SelectedCustomer.IsActive;
            var actionText = newState ? "تفعيل" : "تعطيل";
            var reason = newState ? string.Empty
                : "\n\nملاحظة: العميل لن يُحذف — يمكن إعادة تفعيله في أي وقت.";

            var confirm = MessageBox.Show(
                $"هل أنت متأكد من {actionText} العميل '{SelectedCustomer.Name}'؟{reason}",
                $"تأكيد {actionText} العميل",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                await _customerService.SetActiveStatusAsync(SelectedCustomer.Id, newState);
                SetStatus($"تم {actionText} العميل '{SelectedCustomer.Name}' بنجاح.", error: false);
                await LoadCustomersAsync();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل تغيير حالة العميل:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // كشف الحساب
        // ══════════════════════════════════════════════════════════════════════

        private async Task ExecuteShowStatementAsync()
        {
            if (SelectedCustomer == null) return;

            StatementCustomer = SelectedCustomer;
            CurrentMode = ScreenMode.Statement;
            await LoadStatementAsync();
        }

        private async Task LoadStatementAsync()
        {
            if (StatementCustomer == null) return;

            try
            {
                var lines = await _customerService.GetStatementAsync(
                    StatementCustomer.Id, StatementFrom, StatementTo);

                StatementLines.Clear();
                foreach (var line in lines) StatementLines.Add(line);

                StatementTotalDebit = StatementLines.Sum(l => l.DebitAmount);
                StatementTotalCredit = StatementLines.Sum(l => l.CreditAmount);
                StatementFinalBalance = StatementLines.Count > 0
                    ? StatementLines.Last().RunningBalance
                    : 0m;

                // تحديث رصيد العميل في القائمة
                StatementCustomer.CurrentBalance = await _customerService.CalculateBalanceAsync(StatementCustomer.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"خطأ في تحميل كشف الحساب:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // تسجيل الدفعة
        // ══════════════════════════════════════════════════════════════════════

        private void ExecuteShowPayment()
        {
            if (SelectedCustomer == null) return;

            PaymentAmountText = string.Empty;
            PaymentDate = DateTime.Now;
            PaymentMethod = "نقدي";
            PaymentNotes = string.Empty;
            StatusMessage = string.Empty;
            IsStatusError = false;

            CurrentMode = ScreenMode.Payment;
        }

        private async Task ExecuteSavePaymentAsync()
        {
            if (SelectedCustomer == null)
            {
                SetStatus("لم يتم تحديد عميل.", error: true);
                return;
            }

            // ── التحقق من المبلغ ──────────────────────────────────────────────
            if (!decimal.TryParse(PaymentAmountText, out var amount) || amount <= 0)
            {
                SetStatus("يرجى إدخال مبلغ صحيح وموجب (أكبر من صفر).", error: true);
                return;
            }

            // ── تحذير إذا تجاوز الرصيد ──────────────────────────────────────
            var currentBalance = SelectedCustomer.CurrentBalance;
            if (amount > currentBalance && currentBalance > 0)
            {
                var proceed = MessageBox.Show(
                    $"المبلغ المدخل ({amount:N2}) يتجاوز الرصيد الحالي للعميل ({currentBalance:N2}).\n" +
                    "هل تريد تسجيل الدفعة على أي حال؟",
                    "تنبيه: الدفعة تتجاوز الرصيد",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (proceed != MessageBoxResult.Yes) return;
            }

            // ── تأكيد الحفظ ───────────────────────────────────────────────────
            var confirm = MessageBox.Show(
                $"تسجيل دفعة بمبلغ {amount:N2} من العميل '{SelectedCustomer.Name}'؟\n" +
                $"الطريقة: {PaymentMethod} | التاريخ: {PaymentDate:d/M/yyyy}",
                "تأكيد تسجيل الدفعة",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var payment = new CustomerPaymentModel
                {
                    CustomerId = SelectedCustomer.Id,
                    Amount = amount,
                    PaymentDate = PaymentDate,
                    PaymentMethod = PaymentMethod,
                    Notes = PaymentNotes?.Trim() ?? string.Empty
                };

                await _customerService.AddPaymentAsync(payment);

                // تحديث الرصيد في القائمة
                SelectedCustomer.CurrentBalance = await _customerService.CalculateBalanceAsync(SelectedCustomer.Id);

                SetStatus($"تم تسجيل الدفعة بمبلغ {amount:N2} بنجاح.", error: false);
                await LoadCustomersAsync();
                CurrentMode = ScreenMode.List;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل تسجيل الدفعة:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // مساعدات
        // ══════════════════════════════════════════════════════════════════════

        private void SetStatus(string message, bool error)
        {
            IsStatusError = error;
            StatusMessage = message;
        }

        /// <summary>
        /// تحقق شكلي مرن من رقم الهاتف (أرقام فقط، اختياري +، 7-15 رقم)
        /// </summary>
        private static bool IsValidPhoneLoose(string phone)
        {
            var digits = Regex.Replace(phone, @"[\s\-\(\)\+]", "");
            return digits.Length >= 7 && digits.Length <= 15 && digits.All(char.IsDigit);
        }
    }
}
