using CarPartManager.MVVM;

namespace CarPartManager.Models
{
    /// <summary>
    /// يمثل بند واحد داخل فاتورة المشتريات (Purchase Invoice Line Item)
    /// </summary>
    public class PurchaseInvoiceItemModel : ViewModelBase
    {
        private int _id;
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private int _purchaseInvoiceId;
        public int PurchaseInvoiceId
        {
            get => _purchaseInvoiceId;
            set => SetProperty(ref _purchaseInvoiceId, value);
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

        private int _quantity = 1;
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

        public decimal TotalPrice => Quantity * UnitPrice;

        public PurchaseInvoiceItemModel()
        {
        }

        public PurchaseInvoiceItemModel(ProductModel product, int quantity, decimal unitPrice)
        {
            ProductId = product.Id;
            PartNumber = product.PartNumber;
            ProductName = product.Name;
            Quantity = quantity;
            UnitPrice = unitPrice;
        }
    }
}
