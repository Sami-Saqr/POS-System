using CarPartManager.MVVM;

namespace CarPartManager.Models
{
    /// <summary>
    /// نموذج بند الجرد (Stocktake Line)
    /// </summary>
    public class StocktakeLineModel : ViewModelBase
    {
        private int _id;
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private int _sessionId;
        public int SessionId
        {
            get => _sessionId;
            set => SetProperty(ref _sessionId, value);
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

        private string _barcode = string.Empty;
        public string Barcode
        {
            get => _barcode;
            set => SetProperty(ref _barcode, value);
        }

        private string _category = string.Empty;
        public string Category
        {
            get => _category;
            set => SetProperty(ref _category, value);
        }

        private int _bookQuantity; // Book quantity at snapshot time
        public int BookQuantity
        {
            get => _bookQuantity;
            set
            {
                if (SetProperty(ref _bookQuantity, value))
                {
                    OnPropertyChanged(nameof(Difference));
                    OnPropertyChanged(nameof(DifferenceValue));
                    OnPropertyChanged(nameof(HasDifference));
                    OnPropertyChanged(nameof(DifferenceType));
                }
            }
        }

        private int? _countedQuantity; // null = not counted yet
        public int? CountedQuantity
        {
            get => _countedQuantity;
            set
            {
                if (SetProperty(ref _countedQuantity, value))
                {
                    OnPropertyChanged(nameof(IsCounted));
                    OnPropertyChanged(nameof(Difference));
                    OnPropertyChanged(nameof(DifferenceValue));
                    OnPropertyChanged(nameof(HasDifference));
                    OnPropertyChanged(nameof(DifferenceType));
                }
            }
        }

        private decimal _costPrice;
        public decimal CostPrice
        {
            get => _costPrice;
            set
            {
                if (SetProperty(ref _costPrice, value))
                {
                    OnPropertyChanged(nameof(DifferenceValue));
                }
            }
        }

        private string _notes = string.Empty;
        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        // Computed properties
        public bool IsCounted => CountedQuantity.HasValue;

        public int Difference => CountedQuantity.HasValue ? CountedQuantity.Value - BookQuantity : 0;

        public decimal DifferenceValue => Difference * CostPrice;

        public bool HasDifference => IsCounted && Difference != 0;

        public string DifferenceType => !IsCounted ? "غير معدود" :
                                       Difference > 0 ? "فائض" :
                                       Difference < 0 ? "نقص" :
                                       "متطابق";

        public string DifferenceColor => !IsCounted ? "#64748B" :
                                        Difference > 0 ? "#059669" :
                                        Difference < 0 ? "#DC2626" :
                                        "#64748B";
    }
}
