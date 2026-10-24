using CarPartManager.MVVM;

namespace CarPartManager.Models
{
    /// <summary>
    /// يمثل تصنيف مصروف (Expense Category Model).
    /// لا يتم حذف أي تصنيف نهائياً — يتم تعطيله فقط للحفاظ على سلامة السجلات التاريخية.
    /// </summary>
    public class ExpenseCategoryModel : ViewModelBase
    {
        private int _id;
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value))
                {
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (SetProperty(ref _isActive, value))
                {
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(StatusBackground));
                }
            }
        }

        private DateTime _createdDate = DateTime.Now;
        public DateTime CreatedDate
        {
            get => _createdDate;
            set => SetProperty(ref _createdDate, value);
        }

        /// <summary>
        /// عدد المصروفات المرتبطة بالتصنيف (للقراءة فقط، لا يُخزَّن في قاعدة البيانات)
        /// </summary>
        private int _expensesCount;
        public int ExpensesCount
        {
            get => _expensesCount;
            set
            {
                if (SetProperty(ref _expensesCount, value))
                {
                    OnPropertyChanged(nameof(IsUsed));
                    OnPropertyChanged(nameof(UsageText));
                }
            }
        }

        public bool IsUsed => ExpensesCount > 0;

        public string UsageText => ExpensesCount > 0
            ? $"مستخدم في {ExpensesCount} مصروف"
            : "غير مستخدم";

        public string StatusText => IsActive ? "مفعل" : "معطل";

        public string StatusColor => IsActive ? "#059669" : "#94A3B8";

        public string StatusBackground => IsActive ? "#ECFDF5" : "#F1F5F9";
    }
}
