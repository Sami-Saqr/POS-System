using CarPartManager.MVVM;

namespace CarPartManager.Models
{
    /// <summary>
    /// نموذج بيانات العميل (Customer Model)
    /// يحتوي على البيانات الأساسية للعميل والرصيد المحسوب من السجلات.
    /// </summary>
    public class CustomerModel : ViewModelBase
    {
        private int _id;
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private string _name = string.Empty;
        /// <summary>
        /// اسم العميل (مطلوب)
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _phone = string.Empty;
        /// <summary>
        /// رقم الهاتف (اختياري)
        /// </summary>
        public string Phone
        {
            get => _phone;
            set => SetProperty(ref _phone, value);
        }

        private string _address = string.Empty;
        /// <summary>
        /// العنوان (اختياري)
        /// </summary>
        public string Address
        {
            get => _address;
            set => SetProperty(ref _address, value);
        }

        private string _notes = string.Empty;
        /// <summary>
        /// ملاحظات عامة (اختياري)
        /// </summary>
        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        private decimal _openingBalance;
        /// <summary>
        /// الرصيد الافتتاحي: المبلغ الذي كان مدينًا به العميل قبل بدء التتبع في النظام
        /// </summary>
        public decimal OpeningBalance
        {
            get => _openingBalance;
            set => SetProperty(ref _openingBalance, value);
        }

        private bool _isActive = true;
        /// <summary>
        /// هل العميل نشط؟ (لا يُحذف؛ يُعطَّل بدلاً من الحذف)
        /// </summary>
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
        /// <summary>
        /// تاريخ إنشاء سجل العميل
        /// </summary>
        public DateTime CreatedDate
        {
            get => _createdDate;
            set => SetProperty(ref _createdDate, value);
        }

        // ──────────────────────────────────────────────────────────────────────
        // الخصائص المحسوبة (تُحدَّث من الخدمة، لا تُحفظ في قاعدة البيانات)
        // ──────────────────────────────────────────────────────────────────────

        private decimal _currentBalance;
        /// <summary>
        /// الرصيد الحالي المستحق على العميل (محسوب من السجلات):
        /// OpeningBalance + Sum(SalesInvoice.RemainingAmount linked) + Sum(InvoiceLinked.GrandTotal - PaidAmount) - Sum(CustomerPayment.Amount)
        /// تفصيل: الرصيد الافتتاحي + إجمالي الفواتير المرتبطة غير المدفوعة - إجمالي الدفعات المسجلة
        /// </summary>
        public decimal CurrentBalance
        {
            get => _currentBalance;
            set
            {
                if (SetProperty(ref _currentBalance, value))
                {
                    OnPropertyChanged(nameof(HasBalance));
                    OnPropertyChanged(nameof(BalanceColor));
                }
            }
        }

        /// <summary>
        /// هل لديه رصيد مستحق؟
        /// </summary>
        public bool HasBalance => CurrentBalance > 0;

        /// <summary>
        /// لون عرض الرصيد: أحمر إذا كان هناك مبلغ مستحق، وإلا أخضر
        /// </summary>
        public string BalanceColor => HasBalance ? "#DC2626" : "#059669";

        // خصائص الحالة للعرض
        public string StatusText => IsActive ? "نشط" : "غير نشط";
        public string StatusColor => IsActive ? "#166534" : "#991B1B";
        public string StatusBackground => IsActive ? "#DCFCE7" : "#FEE2E2";
    }
}
