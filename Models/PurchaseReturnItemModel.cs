using CarPartManager.MVVM;

namespace CarPartManager.Models
{
    /// <summary>
    /// يمثل بنداً واحداً داخل مرتجع المشتريات (Purchase Return Line Item)
    /// </summary>
    public class PurchaseReturnItemModel : ViewModelBase
    {
        private int _id;
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private int _purchaseReturnId;
        public int PurchaseReturnId
        {
            get => _purchaseReturnId;
            set => SetProperty(ref _purchaseReturnId, value);
        }

        private int _purchaseInvoiceItemId;
        public int PurchaseInvoiceItemId
        {
            get => _purchaseInvoiceItemId;
            set => SetProperty(ref _purchaseInvoiceItemId, value);
        }

        private int _productId;
        public int ProductId
        {
            get => _productId;
            set => SetProperty(ref _productId, value);
        }

        private string _partNumber = string.Empty;
        public string PartNumber
        {
            get => _partNumber;
            set => SetProperty(ref _partNumber, value);
        }

        private string _productName = string.Empty;
        public string ProductName
        {
            get => _productName;
            set => SetProperty(ref _productName, value);
        }

        private int _originalQuantity;
        /// <summary>
        /// الكمية المشتراة الأصلية في فاتورة الشراء
        /// </summary>
        public int OriginalQuantity
        {
            get => _originalQuantity;
            set => SetProperty(ref _originalQuantity, value);
        }

        private int _previouslyReturnedQuantity;
        /// <summary>
        /// الكمية التي تم إرجاعها مسبقاً للمورد
        /// </summary>
        public int PreviouslyReturnedQuantity
        {
            get => _previouslyReturnedQuantity;
            set => SetProperty(ref _previouslyReturnedQuantity, value);
        }

        public int MaxReturnableQuantity => Math.Max(0, OriginalQuantity - PreviouslyReturnedQuantity);

        private int _quantity = 1;
        /// <summary>
        /// الكمية المراد إرجاعها للمورد
        /// </summary>
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, value))
                {
                    OnPropertyChanged(nameof(TotalPrice));
                }
            }
        }

        private decimal _unitPrice;
        /// <summary>
        /// سعر الشراء الأصلي للوحدة
        /// </summary>
        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (SetProperty(ref _unitPrice, value))
                {
                    OnPropertyChanged(nameof(TotalPrice));
                }
            }
        }

        public decimal TotalPrice
        {
            get => Quantity * UnitPrice;
            set { }
        }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public PurchaseReturnModel? PurchaseReturn { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public ProductModel? Product { get; set; }

        public PurchaseReturnItemModel()
        {
        }
    }
}
