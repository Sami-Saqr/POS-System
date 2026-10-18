using System.Collections.ObjectModel;
using CarPartManager.MVVM;

namespace CarPartManager.Models
{
    /// <summary>
    /// يمثل إشعار / فاتورة مرتجع مشتريات إلى المورد (Purchase Return Model)
    /// </summary>
    public class PurchaseReturnModel : ViewModelBase
    {
        private int _id;
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private string _returnNumber = string.Empty;
        /// <summary>
        /// رقم المرتجع الفريد (مثل PRT-2026-0001)
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

        private int _purchaseInvoiceId;
        public int PurchaseInvoiceId
        {
            get => _purchaseInvoiceId;
            set => SetProperty(ref _purchaseInvoiceId, value);
        }

        private string _purchaseInvoiceNumber = string.Empty;
        public string PurchaseInvoiceNumber
        {
            get => _purchaseInvoiceNumber;
            set => SetProperty(ref _purchaseInvoiceNumber, value);
        }

        public PurchaseInvoiceModel? PurchaseInvoice { get; set; }

        private int? _supplierId;
        public int? SupplierId
        {
            get => _supplierId;
            set => SetProperty(ref _supplierId, value);
        }

        private string _supplierName = string.Empty;
        public string SupplierName
        {
            get => _supplierName;
            set => SetProperty(ref _supplierName, value);
        }

        private decimal _totalAmount;
        /// <summary>
        /// إجمالي قيمة المرتجع المستردة من المورد
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

        private string _reason = "عيب تصنيع";
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

        public List<PurchaseReturnItemModel> Items { get; set; } = new();

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
