using System.Collections.ObjectModel;
using CarPartManager.MVVM;

namespace CarPartManager.Models
{
    /// <summary>
    /// يمثل فاتورة مبيعات صادرة للعميل (Sales Invoice Model)
    /// </summary>
    public class SalesInvoiceModel : ViewModelBase
    {
        private int _id;
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private string _invoiceNumber = string.Empty;
        /// <summary>
        /// رقم الفاتورة الفريد وغير المكرر (مثل INV-2026-0001)
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

        private int? _customerId;
        /// <summary>
        /// معرف العميل المرتبط (nullable — الفواتير القديمة وفواتير العملاء النقديين تبقى صالحة بـ null)
        /// </summary>
        public int? CustomerId
        {
            get => _customerId;
            set => SetProperty(ref _customerId, value);
        }

        private string? _customerName = "عميل نقدي";
        public string? CustomerName
        {
            get => _customerName;
            set => SetProperty(ref _customerName, value);
        }

        private string? _customerPhone;
        public string? CustomerPhone
        {
            get => _customerPhone;
            set => SetProperty(ref _customerPhone, value);
        }

        private string? _customerAddress;
        public string? CustomerAddress
        {
            get => _customerAddress;
            set => SetProperty(ref _customerAddress, value);
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

        private decimal _subTotal;
        /// <summary>
        /// إجمالي بنود الفاتورة قبل الخصم
        /// </summary>
        public decimal SubTotal
        {
            get => _subTotal;
            set => SetProperty(ref _subTotal, value);
        }

        private decimal _discount;
        /// <summary>
        /// قيمة الخصم النقدي
        /// </summary>
        public decimal Discount
        {
            get => _discount;
            set
            {
                if (SetProperty(ref _discount, value))
                {
                    RecalculateTotals();
                }
            }
        }

        private decimal _grandTotal;
        /// <summary>
        /// الصافي النهائي بعد الخصم
        /// </summary>
        public decimal GrandTotal
        {
            get => _grandTotal;
            set => SetProperty(ref _grandTotal, value);
        }

        private string _paymentMethod = "نقدي"; // "نقدي", "شبكة / فيزا", "آجل"
        public string PaymentMethod
        {
            get => _paymentMethod;
            set => SetProperty(ref _paymentMethod, value);
        }

        private decimal _paidAmount;
        public decimal PaidAmount
        {
            get => _paidAmount;
            set
            {
                if (SetProperty(ref _paidAmount, value))
                {
                    OnPropertyChanged(nameof(RemainingAmount));
                }
            }
        }

        public decimal RemainingAmount => Math.Max(0, GrandTotal - PaidAmount);

        private string? _notes;
        public string? Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        private DateTime? _postedDate;
        /// <summary>
        /// تاريخ وتوقيت اعتماد الفاتورة وخصمها من المخزون
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

        public List<SalesInvoiceItemModel> Items { get; set; } = new();

        public bool IsDraft => Status == "مسودة";
        public bool IsPosted => Status == "معتمدة";
        public bool IsCancelled => Status == "ملغاة";

        public string StatusColor => Status switch
        {
            "معتمدة" => "#059669", // أخضر
            "ملغاة" => "#DC2626",  // أحمر
            _ => "#D97706"         // كهرماني / برتقالي للمسودة
        };

        public void RecalculateTotals()
        {
            SubTotal = Items?.Sum(i => i.TotalPrice) ?? 0m;

            if (Discount < 0) Discount = 0m;
            if (Discount > SubTotal) Discount = SubTotal;

            GrandTotal = Math.Max(0, SubTotal - Discount);

            if (PaymentMethod == "نقدي" && (PaidAmount == 0 || PaidAmount == SubTotal))
            {
                PaidAmount = GrandTotal;
            }
            OnPropertyChanged(nameof(RemainingAmount));
        }
    }
}
