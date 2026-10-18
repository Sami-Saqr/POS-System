using System.Collections.ObjectModel;
using System.Windows;
using CarPartManager.Models;
using CarPartManager.MVVM;
using CarPartManager.Services;
using CarPartManager.Views;

namespace CarPartManager.ViewModels
{
    /// <summary>
    /// نموذج عرض شاشة الأصناف وقطع الغيار (Products ViewModel).
    /// يدير عرض جدول القطع، التصفية والبحث الحي، وإضافة وتعديل وحذف الأصناف.
    /// </summary>
    public class ProductsViewModel : ViewModelBase
    {
        private readonly IProductService _productService;
        private readonly List<ProductModel> _allProducts = new();

        public ObservableCollection<ProductModel> FilteredProducts { get; } = new();

        private ProductModel? _selectedProduct;
        public ProductModel? SelectedProduct
        {
            get => _selectedProduct;
            set
            {
                if (SetProperty(ref _selectedProduct, value))
                {
                    OnPropertyChanged(nameof(HasSelectedProduct));
                }
            }
        }

        public bool HasSelectedProduct => _selectedProduct != null;

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterProducts();
                }
            }
        }

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        private int _lowStockCount;
        public int LowStockCount
        {
            get => _lowStockCount;
            set => SetProperty(ref _lowStockCount, value);
        }

        private decimal _totalInventoryValue;
        public decimal TotalInventoryValue
        {
            get => _totalInventoryValue;
            set => SetProperty(ref _totalInventoryValue, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // الأوامر (Commands)
        public RelayCommand SearchCommand { get; }
        public RelayCommand ClearSearchCommand { get; }
        public RelayCommand AddProductCommand { get; }
        public RelayCommand EditProductCommand { get; }
        public RelayCommand DeleteProductCommand { get; }
        public RelayCommand RefreshCommand { get; }

        public ProductsViewModel(IProductService? productService = null)
        {
            _productService = productService ?? new SqliteProductService();

            SearchCommand = new RelayCommand(FilterProducts);
            ClearSearchCommand = new RelayCommand(ClearSearch);
            AddProductCommand = new RelayCommand(ExecuteAddProduct);
            EditProductCommand = new RelayCommand(ExecuteEditProduct, () => HasSelectedProduct);
            DeleteProductCommand = new RelayCommand(ExecuteDeleteProduct, () => HasSelectedProduct);
            RefreshCommand = new RelayCommand(async () => await LoadProductsAsync());

            _ = LoadProductsAsync();
        }

        /// <summary>
        /// تحميل قائمة قطع الغيار من الخدمة وحساب المؤشرات
        /// </summary>
        public async Task LoadProductsAsync()
        {
            try
            {
                var items = await _productService.GetAllProductsAsync();
                _allProducts.Clear();
                _allProducts.AddRange(items);

                FilterProducts();
                UpdateStatistics();
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ في تحميل الأصناف من قاعدة البيانات: {ex.Message}";
                MessageBox.Show($"تعذر تحميل البيانات من قاعدة بيانات SQLite:\n{ex.Message}", "خطأ في قاعدة البيانات", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// تصفية الأصناف بالبحث السريع بكود القطعة، الاسم، الباركود، أو موديل السيارة
        /// </summary>
        private void FilterProducts()
        {
            FilteredProducts.Clear();

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                foreach (var item in _allProducts)
                {
                    FilteredProducts.Add(item);
                }
            }
            else
            {
                var query = SearchText.Trim().ToLower();
                var matches = _allProducts.Where(p =>
                    (!string.IsNullOrEmpty(p.Name) && p.Name.ToLower().Contains(query)) ||
                    (!string.IsNullOrEmpty(p.PartNumber) && p.PartNumber.ToLower().Contains(query)) ||
                    (!string.IsNullOrEmpty(p.Barcode) && p.Barcode.ToLower().Contains(query)) ||
                    (!string.IsNullOrEmpty(p.CompatibleVehicles) && p.CompatibleVehicles.ToLower().Contains(query)) ||
                    (!string.IsNullOrEmpty(p.Category) && p.Category.ToLower().Contains(query))
                );

                foreach (var item in matches)
                {
                    FilteredProducts.Add(item);
                }
            }

            UpdateStatistics();
        }

        private void ClearSearch()
        {
            SearchText = string.Empty;
        }

        private void UpdateStatistics()
        {
            TotalCount = _allProducts.Count;
            LowStockCount = _allProducts.Count(p => p.IsLowStock);
            TotalInventoryValue = _allProducts.Sum(p => p.PurchasePrice * p.Quantity);
        }

        /// <summary>
        /// فتح نافذة إضافة قطعة جديدة
        /// </summary>
        private void ExecuteAddProduct()
        {
            var dialogVm = new ProductDialogViewModel();
            var dialog = new ProductDialogWindow(dialogVm)
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                _ = SaveNewProductAsync(dialogVm.ResultProduct);
            }
        }

        private async Task SaveNewProductAsync(ProductModel newProduct)
        {
            try
            {
                await _productService.AddProductAsync(newProduct);
                await LoadProductsAsync();
                DataEvents.RaiseInventoryOrInvoicesChanged();
                StatusMessage = $"تمت إضافة القطعة '{newProduct.Name}' بنجاح إلى قاعدة البيانات.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"فشل حفظ الصنف: {ex.Message}";
                MessageBox.Show($"حدث خطأ أثناء حفظ الصنف في قاعدة البيانات:\n{ex.Message}", "خطأ في الحفظ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// فتح نافذة تعديل القطعة المحددة
        /// </summary>
        private void ExecuteEditProduct(object? parameter)
        {
            var productToEdit = parameter as ProductModel ?? SelectedProduct;
            if (productToEdit == null) return;

            var dialogVm = new ProductDialogViewModel(productToEdit);
            var dialog = new ProductDialogWindow(dialogVm)
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                _ = SaveEditedProductAsync(dialogVm.ResultProduct);
            }
        }

        private async Task SaveEditedProductAsync(ProductModel editedProduct)
        {
            try
            {
                await _productService.UpdateProductAsync(editedProduct);
                await LoadProductsAsync();
                DataEvents.RaiseInventoryOrInvoicesChanged();
                StatusMessage = $"تم تحديث بيانات القطعة '{editedProduct.Name}' بنجاح في قاعدة البيانات.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"فشل تحديث الصنف: {ex.Message}";
                MessageBox.Show($"حدث خطأ أثناء تحديث الصنف في قاعدة البيانات:\n{ex.Message}", "خطأ في التعديل", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// حذف القطعة المحددة بعد تأكيد المستخدم
        /// </summary>
        private void ExecuteDeleteProduct(object? parameter)
        {
            var productToDelete = parameter as ProductModel ?? SelectedProduct;
            if (productToDelete == null) return;

            var confirmResult = MessageBox.Show(
                $"هل أنت متأكد من رغبتك في حذف الصنف '{productToDelete.Name}' (كود: {productToDelete.PartNumber}) من النظام؟",
                "تأكيد حذف قطعة غيار",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (confirmResult == MessageBoxResult.Yes)
            {
                _ = DeleteProductConfirmedAsync(productToDelete.Id, productToDelete.Name);
            }
        }

        private async Task DeleteProductConfirmedAsync(int id, string name)
        {
            try
            {
                await _productService.DeleteProductAsync(id);
                await LoadProductsAsync();
                DataEvents.RaiseInventoryOrInvoicesChanged();
                StatusMessage = $"تم حذف الصنف '{name}' من قاعدة البيانات بنجاح.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"فشل حذف الصنف: {ex.Message}";
                MessageBox.Show($"حدث خطأ أثناء حذف الصنف من قاعدة البيانات:\n{ex.Message}", "خطأ في الحذف", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
