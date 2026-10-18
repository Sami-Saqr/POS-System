using CarPartManager.MVVM;

namespace CarPartManager.Models
{
    /// <summary>
    /// يمثل بند واحد داخل فاتورة المبيعات (Sales Invoice Line Item)
    /// </summary>
    public class SalesInvoiceItemModel : ViewModelBase
    {
        private int _id;
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private int _salesInvoiceId;
        public int SalesInvoiceId
        {
            get => _salesInvoiceId;
            set => SetProperty(ref _salesInvoiceId, value);
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

        private string _barcode = string.Empty;
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string Barcode
        {
            get => _barcode;
            set => SetProperty(ref _barcode, value);
        }

        private string _productName = string.Empty;
        public string ProductName
        {
            get => _productName;
            set => SetProperty(ref _productName, value);
        }

        private int _availableQuantity;
        /// <summary>
        /// الرصيد المتاح بالمستودع وقت الإضافة للاسترشاد
        /// </summary>
        public int AvailableQuantity
        {
            get => _availableQuantity;
            set => SetProperty(ref _availableQuantity, value);
        }

        private int _quantity = 1;
        public int Quantity
        {
            get => _quantity;
            set
            {
                var clamped = value;
                if (clamped < 1) clamped = 1;
                if (AvailableQuantity > 0 && clamped > AvailableQuantity)
                {
                    clamped = AvailableQuantity;
                }

                if (SetProperty(ref _quantity, clamped))
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

        public decimal TotalPrice => Quantity * UnitPrice;

        public SalesInvoiceItemModel()
        {
        }

        public SalesInvoiceItemModel(ProductModel product, int quantity, decimal unitPrice)
        {
            ProductId = product.Id;
            PartNumber = product.PartNumber;
            Barcode = product.Barcode ?? string.Empty;
            ProductName = product.Name;
            AvailableQuantity = product.Quantity;
            Quantity = quantity;
            UnitPrice = unitPrice;
        }
    }
}
