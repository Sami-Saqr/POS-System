using System.Collections.ObjectModel;
using System.Globalization;
using CarPartManager.Models;
using CarPartManager.MVVM;
using CarPartManager.Services;

namespace CarPartManager.ViewModels
{
    /// <summary>
    /// نموذج عرض نافذة إضافة أو تعديل مصروف (Expense Dialog ViewModel).
    /// زر الحفظ يُفعَّل فوراً بمجرد اكتمال الحقول الإجبارية (التصنيف والمبلغ)،
    /// ولا تعطّله الحقول الاختيارية إطلاقاً.
    /// </summary>
    public class ExpenseDialogViewModel : ViewModelBase
    {
        private readonly bool _isEditMode;
        private readonly string? _currentUser;

        public string DialogTitle => _isEditMode ? "تعديل بيانات المصروف" : "تسجيل مصروف جديد";
        public string SaveButtonText => _isEditMode ? "حفظ التعديلات" : "حفظ المصروف";
        public string HeaderHint => _isEditMode
            ? "سيتم الاحتفاظ بالقيم الأصلية في سجل التدقيق عند تغيير المبلغ أو التصنيف أو التاريخ"
            : "المصروفات لا تؤثر على المخزون أو حسابات العملاء أو فواتير البيع";

        // ── الحقول ─────────────────────────────────────────────────────────────

        private DateTime? _expenseDate = DateTime.Today;
        public DateTime? ExpenseDate
        {
            get => _expenseDate;
            set { if (SetProperty(ref _expenseDate, value)) ClearError(); }
        }

        private string _amountText = string.Empty;
        public string AmountText
        {
            get => _amountText;
            set
            {
                if (SetProperty(ref _amountText, value))
                {
                    ClearError();
                    OnPropertyChanged(nameof(CanSave));
                    SaveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private int? _selectedCategoryId;
        public int? SelectedCategoryId
        {
            get => _selectedCategoryId;
            set
            {
                if (SetProperty(ref _selectedCategoryId, value))
                {
                    ClearError();
                    OnPropertyChanged(nameof(CanSave));
                    SaveCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _description = string.Empty;
        public string Description
        {
            get => _description;
            set { if (SetProperty(ref _description, value)) ClearError(); }
        }

        private string _paymentMethod = SqliteExpenseService.DefaultPaymentMethod;
        public string PaymentMethod
        {
            get => _paymentMethod;
            set { if (SetProperty(ref _paymentMethod, value)) ClearError(); }
        }

        private string _referenceNumber = string.Empty;
        public string ReferenceNumber
        {
            get => _referenceNumber;
            set { if (SetProperty(ref _referenceNumber, value)) ClearError(); }
        }

        private string _notes = string.Empty;
        public string Notes
        {
            get => _notes;
            set { if (SetProperty(ref _notes, value)) ClearError(); }
        }

        // ── القوائم ────────────────────────────────────────────────────────────

        public ObservableCollection<ExpenseCategoryModel> Categories { get; } = new();

        public List<string> PaymentMethods { get; } = new() { "نقدي", "شبكة / فيزا", "تحويل بنكي", "شيك" };

        // ── التحقق ─────────────────────────────────────────────────────────────

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (SetProperty(ref _errorMessage, value))
                    OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(_errorMessage);

        private void ClearError()
        {
            if (HasError) ErrorMessage = string.Empty;
        }

        /// <summary>
        /// زر الحفظ مفعّل طالما التصنيف مُحدد والمبلغ رقم موجب صحيح.
        /// </summary>
        public bool CanSave =>
            SelectedCategoryId.HasValue && SelectedCategoryId.Value > 0 &&
            TryParseAmount(AmountText, out var amount) && amount > 0m;

        // ── الأوامر ────────────────────────────────────────────────────────────

        public Action<bool>? RequestClose { get; set; }

        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }

        public ExpenseModel ResultExpense { get; private set; }

        public ExpenseDialogViewModel(
            IEnumerable<ExpenseCategoryModel> availableCategories,
            ExpenseModel? expenseToEdit = null,
            string? currentUser = null)
        {
            _isEditMode = expenseToEdit != null;
            _currentUser = currentUser;
            ResultExpense = expenseToEdit ?? new ExpenseModel();

            foreach (var category in availableCategories)
                Categories.Add(category);

            if (_isEditMode && expenseToEdit != null)
            {
                _expenseDate = expenseToEdit.ExpenseDate;
                _amountText = expenseToEdit.Amount.ToString("0.##", CultureInfo.InvariantCulture);
                _selectedCategoryId = expenseToEdit.CategoryId > 0 ? expenseToEdit.CategoryId : null;
                _description = expenseToEdit.Description;
                _paymentMethod = string.IsNullOrWhiteSpace(expenseToEdit.PaymentMethod)
                    ? SqliteExpenseService.DefaultPaymentMethod
                    : expenseToEdit.PaymentMethod;
                _referenceNumber = expenseToEdit.ReferenceNumber ?? string.Empty;
                _notes = expenseToEdit.Notes ?? string.Empty;

                // ضمان ظهور تصنيف المصروف الحالي حتى لو كان معطلاً
                if (_selectedCategoryId.HasValue &&
                    !Categories.Any(c => c.Id == _selectedCategoryId.Value))
                {
                    Categories.Add(new ExpenseCategoryModel
                    {
                        Id = _selectedCategoryId.Value,
                        Name = string.IsNullOrWhiteSpace(expenseToEdit.CategoryName)
                            ? "تصنيف غير متاح"
                            : expenseToEdit.CategoryName!,
                        IsActive = false
                    });
                }
            }

            SaveCommand = new RelayCommand(ExecuteSave, () => CanSave);
            CancelCommand = new RelayCommand(() => RequestClose?.Invoke(false));
        }

        /// <summary>
        /// تحويل نص المبلغ إلى رقم عشري مع قبول الأرقام العربية-الهندية والفواصل العربية.
        /// </summary>
        public static bool TryParseAmount(string? text, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var normalized = PhoneHelper.NormalizeDigits(text.Trim())
                .Replace('٫', '.')
                .Replace(',', '.')
                .Replace(" ", string.Empty);

            return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        private void ExecuteSave()
        {
            if (!SelectedCategoryId.HasValue || SelectedCategoryId.Value <= 0)
            {
                ErrorMessage = "يرجى اختيار تصنيف المصروف — الحقل إجباري.";
                return;
            }

            if (!TryParseAmount(AmountText, out var amount))
            {
                ErrorMessage = "قيمة المبلغ غير صالحة. يرجى إدخال رقم صحيح (يمكن استخدام الأرقام العربية).";
                return;
            }

            if (amount <= 0m)
            {
                ErrorMessage = "قيمة المصروف يجب أن تكون أكبر من صفر.";
                return;
            }

            ResultExpense.ExpenseDate = (ExpenseDate ?? DateTime.Today).Date;
            ResultExpense.Amount = amount;
            ResultExpense.CategoryId = SelectedCategoryId.Value;
            ResultExpense.Description = Description?.Trim() ?? string.Empty;
            ResultExpense.PaymentMethod = string.IsNullOrWhiteSpace(PaymentMethod)
                ? SqliteExpenseService.DefaultPaymentMethod
                : PaymentMethod.Trim();
            ResultExpense.ReferenceNumber = string.IsNullOrWhiteSpace(ReferenceNumber) ? null : ReferenceNumber.Trim();
            ResultExpense.Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim();

            if (_isEditMode)
                ResultExpense.ModifiedBy = _currentUser;
            else
                ResultExpense.CreatedBy = _currentUser;

            RequestClose?.Invoke(true);
        }
    }
}
