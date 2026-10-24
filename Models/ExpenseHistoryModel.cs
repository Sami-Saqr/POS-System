using CarPartManager.MVVM;

namespace CarPartManager.Models
{
    /// <summary>
    /// سجل تدقيق مبسط لتعديلات وإلغاءات المصروفات (Expense History / Audit Model).
    /// يحفظ القيم القديمة (المبلغ، التصنيف، التاريخ) قبل التعديل حتى لا تُفقد البيانات الأصلية أبداً.
    /// </summary>
    public class ExpenseHistoryModel : ViewModelBase
    {
        private int _id;
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private int _expenseId;
        public int ExpenseId
        {
            get => _expenseId;
            set => SetProperty(ref _expenseId, value);
        }

        private DateTime _changedAt = DateTime.Now;
        public DateTime ChangedAt
        {
            get => _changedAt;
            set
            {
                if (SetProperty(ref _changedAt, value))
                {
                    OnPropertyChanged(nameof(ChangedAtText));
                }
            }
        }

        private string? _changedBy;
        public string? ChangedBy
        {
            get => _changedBy;
            set
            {
                if (SetProperty(ref _changedBy, value))
                {
                    OnPropertyChanged(nameof(ChangedByText));
                }
            }
        }

        private string _changeType = "تعديل";
        /// <summary>
        /// نوع التغيير: "تعديل" أو "إلغاء"
        /// </summary>
        public string ChangeType
        {
            get => _changeType;
            set
            {
                if (SetProperty(ref _changeType, value))
                {
                    OnPropertyChanged(nameof(ChangeTypeColor));
                }
            }
        }

        private decimal? _previousAmount;
        public decimal? PreviousAmount
        {
            get => _previousAmount;
            set
            {
                if (SetProperty(ref _previousAmount, value))
                {
                    OnPropertyChanged(nameof(PreviousAmountText));
                }
            }
        }

        private decimal? _newAmount;
        public decimal? NewAmount
        {
            get => _newAmount;
            set
            {
                if (SetProperty(ref _newAmount, value))
                {
                    OnPropertyChanged(nameof(NewAmountText));
                    OnPropertyChanged(nameof(AmountChangeText));
                }
            }
        }

        private string? _previousCategoryName;
        public string? PreviousCategoryName
        {
            get => _previousCategoryName;
            set
            {
                if (SetProperty(ref _previousCategoryName, value))
                {
                    OnPropertyChanged(nameof(CategoryChangeText));
                }
            }
        }

        private string? _newCategoryName;
        public string? NewCategoryName
        {
            get => _newCategoryName;
            set
            {
                if (SetProperty(ref _newCategoryName, value))
                {
                    OnPropertyChanged(nameof(CategoryChangeText));
                }
            }
        }

        private DateTime? _previousExpenseDate;
        public DateTime? PreviousExpenseDate
        {
            get => _previousExpenseDate;
            set
            {
                if (SetProperty(ref _previousExpenseDate, value))
                {
                    OnPropertyChanged(nameof(DateChangeText));
                }
            }
        }

        private DateTime? _newExpenseDate;
        public DateTime? NewExpenseDate
        {
            get => _newExpenseDate;
            set
            {
                if (SetProperty(ref _newExpenseDate, value))
                {
                    OnPropertyChanged(nameof(DateChangeText));
                }
            }
        }

        private string? _reason;
        public string? Reason
        {
            get => _reason;
            set => SetProperty(ref _reason, value);
        }

        // ── خصائص عرض محسوبة (لا تُخزَّن في قاعدة البيانات) ──────────────────

        public string ChangedAtText => ChangedAt.ToString("yyyy/MM/dd HH:mm");

        public string ChangedByText => string.IsNullOrWhiteSpace(ChangedBy) ? "غير محدد" : ChangedBy!;

        public string ChangeTypeColor => ChangeType == "إلغاء" ? "#DC2626" : "#D97706";

        public string PreviousAmountText => PreviousAmount.HasValue ? $"{PreviousAmount.Value:N2}" : "—";

        public string NewAmountText => NewAmount.HasValue ? $"{NewAmount.Value:N2}" : "—";

        public string AmountChangeText => PreviousAmount.HasValue && NewAmount.HasValue && PreviousAmount.Value != NewAmount.Value
            ? $"{PreviousAmount.Value:N2} ← {NewAmount.Value:N2}"
            : "بدون تغيير";

        public string CategoryChangeText => PreviousCategoryName == NewCategoryName
            ? "بدون تغيير"
            : $"{PreviousCategoryName ?? "—"} ← {NewCategoryName ?? "—"}";

        public string DateChangeText => PreviousExpenseDate?.Date == NewExpenseDate?.Date
            ? "بدون تغيير"
            : $"{(PreviousExpenseDate.HasValue ? PreviousExpenseDate.Value.ToString("yyyy/MM/dd") : "—")} ← {(NewExpenseDate.HasValue ? NewExpenseDate.Value.ToString("yyyy/MM/dd") : "—")}";
    }
}
