using System.Collections.ObjectModel;
using CarPartManager.MVVM;

namespace CarPartManager.Models
{
    /// <summary>
    /// يمثل إشعار / فاتورة مرتجع مبيعات من العميل (Sales Return Model)
    /// </summary>
    public class SalesReturnModel : ViewModelBase
    {
        private int _id;
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private string _returnNumber = string.Empty;
        /// <summary>
        /// رقم المرتجع الفريد (مثل SRT-2026-0001)
        /// </summary>
        public string ReturnNumber
        {
            get => _returnNumber;
            set => SetProperty(ref _returnNumber, value);
        }

        private DateTime _returnDate = DateTime.Now;
        public DateTime ReturnDate
        {
            get => _returnDate;
            set => SetProperty(ref _returnDate, value);
        }

        private int _salesInvoiceId;
        public int SalesInvoiceId
        {
            get => _salesInvoiceId;
            set => SetProperty(ref _salesInvoiceId, value);
        }

        private string _salesInvoiceNumber = string.Empty;
        public string SalesInvoiceNumber
        {
            get => _salesInvoiceNumber;
            set => SetProperty(ref _salesInvoiceNumber, value);
        }

        public SalesInvoiceModel? SalesInvoice { get; set; }

        private string _customerName = "عميل نقدي";
        public string CustomerName
        {
            get => _customerName;
            set => SetProperty(ref _customerName, value);
        }

        private decimal _totalAmount;
        /// <summary>
        /// إجمالي قيمة المرتجع المستحقة للعميل
        /// </summary>
        public decimal TotalAmount
        {
            get => _totalAmount;
            set => SetProperty(ref _totalAmount, value);
        }

        private string _status = "معتمدة"; // "مسودة" أو "معتمدة"
        public string Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    OnPropertyChanged(nameof(IsDraft));
                    OnPropertyChanged(nameof(IsPosted));
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        private DateTime? _postedDate;
        public DateTime? PostedDate
        {
            get => _postedDate;
            set => SetProperty(ref _postedDate, value);
        }

        private string _reason = "طلب العميل";
        public string Reason
        {
            get => _reason;
            set => SetProperty(ref _reason, value);
        }

        private string _notes = string.Empty;
        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public List<SalesReturnItemModel> Items { get; set; } = new();

        public bool IsDraft => Status == "مسودة";
        public bool IsPosted => Status == "معتمدة";

        public string StatusColor => Status switch
        {
            "معتمدة" => "#059669",
            "ملغاة" => "#DC2626",
            _ => "#D97706"
        };

        public void RecalculateTotal()
        {
            TotalAmount = Items?.Sum(i => i.TotalPrice) ?? 0m;
        }
    }
}
