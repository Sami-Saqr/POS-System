using CarPartManager.MVVM;

namespace CarPartManager.Models
{
    /// <summary>
    /// يمثل مصروفاً تشغيلياً واحداً (Expense Model).
    /// المصروفات لا تؤثر إطلاقاً على المخزون أو حسابات العملاء أو فواتير البيع.
    /// </summary>
    public class ExpenseModel : ViewModelBase
    {
        private int _id;
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private DateTime _expenseDate = DateTime.Today;
        /// <summary>
        /// تاريخ استحقاق المصروف (يُدخل يدوياً ويفترض أن يكون تاريخ اليوم افتراضياً)
        /// </summary>
        public DateTime ExpenseDate
        {
            get => _expenseDate;
            set
            {
                if (SetProperty(ref _expenseDate, value))
                {
                    OnPropertyChanged(nameof(ExpenseDateText));
                }
            }
        }

        private int _categoryId;
        public int CategoryId
        {
            get => _categoryId;
            set => SetProperty(ref _categoryId, value);
        }

        private string? _categoryName;
        /// <summary>
        /// اسم التصنيف للعرض فقط (يُجلب من جدول التصنيفات، ولا يُخزَّن في جدول المصروفات)
        /// </summary>
        public string? CategoryName
        {
            get => _categoryName;
            set => SetProperty(ref _categoryName, value);
        }

        private string _description = string.Empty;
        /// <summary>
        /// وصف المصروف (اختياري)
        /// </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        private decimal _amount;
        /// <summary>
        /// قيمة المصروف (رقم عشري دقيق)
        /// </summary>
        public decimal Amount
        {
            get => _amount;
            set
            {
                if (SetProperty(ref _amount, value))
                {
                    OnPropertyChanged(nameof(AmountText));
                }
            }
        }

        private string _paymentMethod = "نقدي";
        public string PaymentMethod
        {
            get => _paymentMethod;
            set => SetProperty(ref _paymentMethod, value);
        }

        private string? _referenceNumber;
        /// <summary>
        /// رقم مرجعي اختياري (رقم إيصال أو مستند خارجي)
        /// </summary>
        public string? ReferenceNumber
        {
            get => _referenceNumber;
            set => SetProperty(ref _referenceNumber, value);
        }

        private string? _notes;
        public string? Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        private bool _isCancelled;
        /// <summary>
        /// الإلغاء بديل عن الحذف: السجل يبقى محفوظاً مع سبب الإلغاء
        /// </summary>
        public bool IsCancelled
        {
            get => _isCancelled;
            set
            {
                if (SetProperty(ref _isCancelled, value))
                {
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(StatusBackground));
                }
            }
        }

        private DateTime? _cancellationDate;
        public DateTime? CancellationDate
        {
            get => _cancellationDate;
            set => SetProperty(ref _cancellationDate, value);
        }

        private string? _cancellationReason;
        public string? CancellationReason
        {
            get => _cancellationReason;
            set => SetProperty(ref _cancellationReason, value);
        }

        private DateTime _createdAt = DateTime.Now;
        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }

        private string? _createdBy;
        /// <summary>
        /// المستخدم الذي سجّل المصروف (نص العرض للمستخدم الحالي في التطبيق — لا يوجد نظام صلاحيات بعد)
        /// </summary>
        public string? CreatedBy
        {
            get => _createdBy;
            set
            {
                if (SetProperty(ref _createdBy, value))
                {
                    OnPropertyChanged(nameof(UserDisplayName));
                }
            }
        }

        private DateTime? _modifiedAt;
        /// <summary>
        /// تاريخ ووقت آخر تعديل — يُحفظ دائماً عند أي تعديل لمنع فقدان أثر البيانات الأصلية
        /// </summary>
        public DateTime? ModifiedAt
        {
            get => _modifiedAt;
            set
            {
                if (SetProperty(ref _modifiedAt, value))
                {
                    OnPropertyChanged(nameof(ModifiedAtText));
                }
            }
        }

        private string? _modifiedBy;
        public string? ModifiedBy
        {
            get => _modifiedBy;
            set => SetProperty(ref _modifiedBy, value);
        }

        // ── خصائص عرض محسوبة (لا تُخزَّن في قاعدة البيانات) ──────────────────

        public string StatusText => IsCancelled ? "ملغاة" : "نشطة";

        public string StatusColor => IsCancelled ? "#DC2626" : "#059669";

        public string StatusBackground => IsCancelled ? "#FEF2F2" : "#ECFDF5";

        public string UserDisplayName => string.IsNullOrWhiteSpace(CreatedBy) ? "غير محدد" : CreatedBy!;

        public string ExpenseDateText => ExpenseDate.ToString("yyyy/MM/dd");

        public string AmountText => $"{Amount:N2} ج.م";

        public string ModifiedAtText => ModifiedAt?.ToString("yyyy/MM/dd HH:mm") ?? "—";
    }
}
