using CarPartManager.MVVM;

namespace CarPartManager.Models
{
    /// <summary>
    /// يمثل بنداً واحداً داخل مرتجع المبيعات (Sales Return Line Item)
    /// </summary>
    public class SalesReturnItemModel : ViewModelBase
    {
        private int _id;
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private int _salesReturnId;
        public int SalesReturnId
        {
            get => _salesReturnId;
            set => SetProperty(ref _salesReturnId, value);
        }

        private int _salesInvoiceItemId;
        public int SalesInvoiceItemId
        {
            get => _salesInvoiceItemId;
            set => SetProperty(ref _salesInvoiceItemId, value);
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
        /// الكمية الأصلية المسجلة في فاتورة البيع
        /// </summary>
        public int OriginalQuantity
        {
            get => _originalQuantity;
            set => SetProperty(ref _originalQuantity, value);
        }

        private int _previouslyReturnedQuantity;
        /// <summary>
        /// الكمية التي تم إرجاعها مسبقاً في مرتجعات أخرى
        /// </summary>
        public int PreviouslyReturnedQuantity
        {
            get => _previouslyReturnedQuantity;
            set => SetProperty(ref _previouslyReturnedQuantity, value);
        }

        public int MaxReturnableQuantity => Math.Max(0, OriginalQuantity - PreviouslyReturnedQuantity);

        private int _quantity = 1;
        /// <summary>
        /// الكمية المراد إرجاعها في هذه العملية
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

        private string _itemCondition = "صالح للبيع"; // "صالح للبيع" (يعاد للمخزون) أو "تالف" (لا يعاد للمخزون)
        public string ItemCondition
        {
            get => _itemCondition;
            set => SetProperty(ref _itemCondition, value);
        }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string Condition
        {
            get => ItemCondition;
            set => ItemCondition = value;
        }

        public decimal TotalPrice
        {
            get => Quantity * UnitPrice;
            set { }
        }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public SalesReturnModel? SalesReturn { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public ProductModel? Product { get; set; }

        public SalesReturnItemModel()
        {
        }
    }
}
