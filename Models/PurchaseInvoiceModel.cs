using System.Collections.ObjectModel;
using CarPartManager.MVVM;

namespace CarPartManager.Models
{
    /// <summary>
    /// يمثل فاتورة مشتريات من المورد (Purchase Invoice Model)
    /// </summary>
    public class PurchaseInvoiceModel : ViewModelBase
    {
        private int _id;
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private string _invoiceNumber = string.Empty;
        /// <summary>
        /// رقم الفاتورة الفريد وغير المكرر (مثل PUR-2026-0001)
        /// </summary>
        public string InvoiceNumber
        {
            get => _invoiceNumber;
            set => SetProperty(ref _invoiceNumber, value);
        }

        private DateTime _invoiceDate = DateTime.Now;
        public DateTime InvoiceDate
        {
            get => _invoiceDate;
            set => SetProperty(ref _invoiceDate, value);
        }

        private int? _supplierId;
        public int? SupplierId
        {
            get => _supplierId;
            set => SetProperty(ref _supplierId, value);
        }

        private string? _supplierName;
        public string? SupplierName
        {
            get => _supplierName;
            set => SetProperty(ref _supplierName, value);
        }

        private string _status = "مسودة"; // "مسودة" (Draft) أو "معتمدة" (Posted)
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

        private decimal _totalAmount;
        public decimal TotalAmount
        {
            get => _totalAmount;
            set => SetProperty(ref _totalAmount, value);
        }

        private string? _notes;
        public string? Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        public List<PurchaseInvoiceItemModel> Items { get; set; } = new();

        private DateTime? _postedDate;
        /// <summary>
        /// تاريخ وتوقيت اعتماد الفاتورة وترحيلها إلى المخزون
        /// </summary>
        public DateTime? PostedDate
        {
            get => _postedDate;
            set => SetProperty(ref _postedDate, value);
        }

        private DateTime? _cancellationDate;
        /// <summary>
        /// تاريخ وتوقيت إلغاء الفاتورة وعكس حركتها المخزنية
        /// </summary>
        public DateTime? CancellationDate
        {
            get => _cancellationDate;
            set => SetProperty(ref _cancellationDate, value);
        }

        private string? _cancellationReason;
        /// <summary>
        /// سبب إلغاء الفاتورة المعتمدة
        /// </summary>
        public string? CancellationReason
        {
            get => _cancellationReason;
            set => SetProperty(ref _cancellationReason, value);
        }

        public bool IsDraft => Status == "مسودة";
        public bool IsPosted => Status == "معتمدة";
        public bool IsCancelled => Status == "ملغاة";

        public string StatusColor => Status switch
        {
            "معتمدة" => "#059669", // أخضر
            "ملغاة" => "#DC2626",  // أحمر
            _ => "#D97706"         // كهرماني / برتقالي للمسودة
        };

        public void RecalculateTotal()
        {
            TotalAmount = Items?.Sum(i => i.TotalPrice) ?? 0m;
        }
    }
}
