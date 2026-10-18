using CarPartManager.MVVM;

namespace CarPartManager.Models
{
    /// <summary>
    /// نموذج بيانات قطعة الغيار (Product / Spare Part Model).
    /// يرث من ViewModelBase ليدعم التحديث الفوري في الواجهة عند التعديل.
    /// </summary>
    public class ProductModel : ViewModelBase
    {
        private int _id;
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private string _partNumber = string.Empty;
        /// <summary>
        /// كود القطعة أو رقم OEM الأصلي
        /// </summary>
        public string PartNumber
        {
            get => _partNumber;
            set => SetProperty(ref _partNumber, value);
        }

        private string _barcode = string.Empty;
        /// <summary>
        /// الباركود المسحي للقطعة
        /// </summary>
        public string Barcode
        {
            get => _barcode;
            set => SetProperty(ref _barcode, value);
        }

        private string _name = string.Empty;
        /// <summary>
        /// اسم الصنف أو قطعة الغيار
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _compatibleVehicles = string.Empty;
        /// <summary>
        /// ماركة وموديل السيارات المتوافقة
        /// </summary>
        public string CompatibleVehicles
        {
            get => _compatibleVehicles;
            set => SetProperty(ref _compatibleVehicles, value);
        }

        private string _category = "عام";
        /// <summary>
        /// تصنيف القطعة (مكابح، فلاتر، كهرباء، نظام تعليق، سيور، إلخ)
        /// </summary>
        public string Category
        {
            get => _category;
            set => SetProperty(ref _category, value);
        }

        private decimal _purchasePrice;
        /// <summary>
        /// سعر الشراء / التكلفة
        /// </summary>
        public decimal PurchasePrice
        {
            get => _purchasePrice;
            set
            {
                if (SetProperty(ref _purchasePrice, value))
                {
                    OnPropertyChanged(nameof(ProfitMargin));
                }
            }
        }

        private decimal _sellingPrice;
        /// <summary>
        /// سعر البيع للزبون
        /// </summary>
        public decimal SellingPrice
        {
            get => _sellingPrice;
            set
            {
                if (SetProperty(ref _sellingPrice, value))
                {
                    OnPropertyChanged(nameof(ProfitMargin));
                }
            }
        }

        private int _quantity;
        /// <summary>
        /// الكمية المتوفرة حالياً في المستودع
        /// </summary>
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, value))
                {
                    OnPropertyChanged(nameof(IsLowStock));
                    OnPropertyChanged(nameof(StockStatusText));
                    OnPropertyChanged(nameof(StockStatusColor));
                }
            }
        }

        private int _minimumStock = 5;
        /// <summary>
        /// حد الطلب الأدنى للتنبيه عند النقص
        /// </summary>
        public int MinimumStock
        {
            get => _minimumStock;
            set
            {
                if (SetProperty(ref _minimumStock, value))
                {
                    OnPropertyChanged(nameof(IsLowStock));
                    OnPropertyChanged(nameof(StockStatusText));
                    OnPropertyChanged(nameof(StockStatusColor));
                }
            }
        }

        private int _damagedQuantity;
        /// <summary>
        /// كمية القطع التالفة المحتفظ بها في المستودع
        /// </summary>
        public int DamagedQuantity
        {
            get => _damagedQuantity;
            set
            {
                if (SetProperty(ref _damagedQuantity, value))
                {
                    OnPropertyChanged(nameof(TotalStockQuantity));
                }
            }
        }

        /// <summary>
        /// إجمالي المخزون في المستودع (الرصيد المتاح للبيع + رصيد التالف)
        /// </summary>
        public int TotalStockQuantity => Quantity + DamagedQuantity;

        private string _notes = string.Empty;
        /// <summary>
        /// ملاحظات إضافية عن موقع القطعة في الرف أو الشركة الصانعة
        /// </summary>
        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        // خصائص مساعدة للعرض (UI Helpers)
        public bool IsLowStock => Quantity <= MinimumStock;

        public bool IsOutOfStock => Quantity <= 0;

        public decimal ProfitMargin => SellingPrice - PurchasePrice;

        public string StockStatusText
        {
            get
            {
                if (Quantity <= 0) return "نفد من المخزن";
                if (Quantity <= MinimumStock) return "منخفض (حرج)";
                return "متوفر";
            }
        }

        public string StockStatusColor
        {
            get
            {
                if (Quantity <= 0) return "#DC2626"; // أحمر
                if (Quantity <= MinimumStock) return "#D97706"; // برتقالي
                return "#059669"; // أخضر
            }
        }

        /// <summary>
        /// إنشاء نسخة مطابقة للتعديل الآمن في النوافذ المنبثقة
        /// </summary>
        public ProductModel Clone()
        {
            return new ProductModel
            {
                Id = this.Id,
                PartNumber = this.PartNumber,
                Barcode = this.Barcode,
                Name = this.Name,
                CompatibleVehicles = this.CompatibleVehicles,
                Category = this.Category,
                PurchasePrice = this.PurchasePrice,
                SellingPrice = this.SellingPrice,
                Quantity = this.Quantity,
                DamagedQuantity = this.DamagedQuantity,
                MinimumStock = this.MinimumStock,
                Notes = this.Notes
            };
        }

        /// <summary>
        /// نسخ القيم من كائن آخر
        /// </summary>
        public void CopyFrom(ProductModel source)
        {
            Id = source.Id;
            PartNumber = source.PartNumber;
            Barcode = source.Barcode;
            Name = source.Name;
            CompatibleVehicles = source.CompatibleVehicles;
            Category = source.Category;
            PurchasePrice = source.PurchasePrice;
            SellingPrice = source.SellingPrice;
            Quantity = source.Quantity;
            DamagedQuantity = source.DamagedQuantity;
            MinimumStock = source.MinimumStock;
            Notes = source.Notes;
        }
    }
}
