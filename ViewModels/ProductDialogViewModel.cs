using CarPartManager.Models;
using CarPartManager.MVVM;

namespace CarPartManager.ViewModels
{
    /// <summary>
    /// نموذج عرض نافذة إضافة أو تعديل قطعة الغيار (Product Dialog ViewModel).
    /// يدير عمليات التحقق من صحة المدخلات والرسائل التحذيرية باللغة العربية.
    /// </summary>
    public class ProductDialogViewModel : ViewModelBase
    {
        private readonly bool _isEditMode;
        private readonly ProductModel _originalProduct;

        // العنوان في أعلى النافذة
        public string DialogTitle => _isEditMode ? "تعديل بيانات قطعة الغيار" : "إضافة قطعة غيار جديدة للمستودع";
        public string SaveButtonText => _isEditMode ? "حفظ التعديلات" : "إضافة إلى المستودع";

        // الحقول القابلة للإدخال
        private string _partNumber = string.Empty;
        public string PartNumber
        {
            get => _partNumber;
            set { SetProperty(ref _partNumber, value); ClearError(); }
        }

        private string _barcode = string.Empty;
        public string Barcode
        {
            get => _barcode;
            set { SetProperty(ref _barcode, value); ClearError(); }
        }

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set { SetProperty(ref _name, value); ClearError(); }
        }

        private string _compatibleVehicles = string.Empty;
        public string CompatibleVehicles
        {
            get => _compatibleVehicles;
            set { SetProperty(ref _compatibleVehicles, value); ClearError(); }
        }

        private string _category = "عام";
        public string Category
        {
            get => _category;
            set { SetProperty(ref _category, value); ClearError(); }
        }

        private string _purchasePriceText = "0";
        public string PurchasePriceText
        {
            get => _purchasePriceText;
            set { SetProperty(ref _purchasePriceText, value); ClearError(); }
        }

        private string _sellingPriceText = "0";
        public string SellingPriceText
        {
            get => _sellingPriceText;
            set { SetProperty(ref _sellingPriceText, value); ClearError(); }
        }

        private string _quantityText = "1";
        public string QuantityText
        {
            get => _quantityText;
            set { SetProperty(ref _quantityText, value); ClearError(); }
        }

        private string _minimumStockText = "5";
        public string MinimumStockText
        {
            get => _minimumStockText;
            set { SetProperty(ref _minimumStockText, value); ClearError(); }
        }

        private string _notes = string.Empty;
        public string Notes
        {
            get => _notes;
            set { SetProperty(ref _notes, value); ClearError(); }
        }

        // إدارة أخطاء التحقق
        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (SetProperty(ref _errorMessage, value))
                {
                    OnPropertyChanged(nameof(HasError));
                }
            }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(_errorMessage);

        // قائمة التصنيفات المقترحة لقطع السيارات
        public List<string> AvailableCategories { get; } = new()
        {
            "مكابح وفرامل",
            "فلاتر وزيوت",
            "كهرباء وإشعال",
            "مساعدات ونظام تعليق",
            "سيور وبكرات",
            "تبريد وتكييف",
            "كلتش وجيربوكس",
            "أجزاء المحرك",
            "إكسسوارات وبودي",
            "عام"
        };

        // أحداث وإجراءات الإغلاق
        public Action<bool>? RequestClose { get; set; }

        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }

        public ProductModel ResultProduct { get; private set; }

        /// <summary>
        /// منشئ مخصص للإضافة الجديدة أو التعديل
        /// </summary>
        public ProductDialogViewModel(ProductModel? productToEdit = null)
        {
            _isEditMode = productToEdit != null;
            _originalProduct = productToEdit ?? new ProductModel();
            ResultProduct = _originalProduct.Clone();

            if (_isEditMode)
            {
                _partNumber = _originalProduct.PartNumber;
                _barcode = _originalProduct.Barcode;
                _name = _originalProduct.Name;
                _compatibleVehicles = _originalProduct.CompatibleVehicles;
                _category = string.IsNullOrWhiteSpace(_originalProduct.Category) ? "عام" : _originalProduct.Category;
                _purchasePriceText = _originalProduct.PurchasePrice.ToString("0.##");
                _sellingPriceText = _originalProduct.SellingPrice.ToString("0.##");
                _quantityText = _originalProduct.Quantity.ToString();
                _minimumStockText = _originalProduct.MinimumStock.ToString();
                _notes = _originalProduct.Notes;
            }

            SaveCommand = new RelayCommand(ExecuteSave);
            CancelCommand = new RelayCommand(ExecuteCancel);
        }

        private void ClearError()
        {
            if (HasError)
            {
                ErrorMessage = string.Empty;
            }
        }

        private void ExecuteSave()
        {
            // 1. التحقق من كود القطعة
            if (string.IsNullOrWhiteSpace(PartNumber))
            {
                ErrorMessage = "يرجى إدخال كود القطعة أو رقم OEM الأصلي.";
                return;
            }

            // 2. التحقق من اسم القطعة
            if (string.IsNullOrWhiteSpace(Name))
            {
                ErrorMessage = "يرجى إدخال اسم الصنف أو قطعة الغيار.";
                return;
            }

            // 3. التحقق من سعر الشراء
            if (!decimal.TryParse(PurchasePriceText, out var purchasePrice))
            {
                ErrorMessage = "قيمة سعر الشراء غير صالحة. يرجى إدخال رقم صحيح.";
                return;
            }

            if (purchasePrice < 0)
            {
                ErrorMessage = "سعر الشراء (التكلفة) لا يمكن أن يكون سالباً.";
                return;
            }

            // 4. التحقق من سعر البيع
            if (!decimal.TryParse(SellingPriceText, out var sellingPrice))
            {
                ErrorMessage = "قيمة سعر البيع غير صالحة. يرجى إدخال رقم صحيح.";
                return;
            }

            if (sellingPrice < 0)
            {
                ErrorMessage = "سعر البيع للزبون لا يمكن أن يكون سالباً.";
                return;
            }

            // 5. التحقق من الكمية المتوفرة
            if (!int.TryParse(QuantityText, out var quantity))
            {
                ErrorMessage = "الكمية المدخلة غير صالحة. يجب أن تكون عدداً صحيحاً.";
                return;
            }

            if (quantity < 0)
            {
                ErrorMessage = "الكمية المتوفرة لا يمكن أن تكون أقل من الصفر.";
                return;
            }

            // 6. التحقق من حد الطلب الأدنى
            if (!int.TryParse(MinimumStockText, out var minimumStock))
            {
                ErrorMessage = "قيمة حد الطلب الأدنى غير صالحة. يجب أن تكون عدداً صحيحاً.";
                return;
            }

            if (minimumStock < 0)
            {
                ErrorMessage = "حد الطلب الأدنى لا يمكن أن يكون سالباً.";
                return;
            }

            // تحديث بيانات الكائن المخرج
            ResultProduct.PartNumber = PartNumber.Trim();
            ResultProduct.Barcode = string.IsNullOrWhiteSpace(Barcode) ? string.Empty : Barcode.Trim();
            ResultProduct.Name = Name.Trim();
            ResultProduct.CompatibleVehicles = CompatibleVehicles.Trim();
            ResultProduct.Category = Category;
            ResultProduct.PurchasePrice = purchasePrice;
            ResultProduct.SellingPrice = sellingPrice;
            ResultProduct.Quantity = quantity;
            ResultProduct.MinimumStock = minimumStock;
            ResultProduct.Notes = Notes.Trim();

            // إغلاق النافذة بنجاح
            RequestClose?.Invoke(true);
        }

        private void ExecuteCancel()
        {
            RequestClose?.Invoke(false);
        }
    }
}
