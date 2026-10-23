using System.Collections.ObjectModel;
using System.Windows;
using CarPartManager.Models;
using CarPartManager.MVVM;
using CarPartManager.Services;

namespace CarPartManager.ViewModels
{
    /// <summary>
    /// نموذج عرض شاشة المخزون والجرد
    /// </summary>
    public class InventoryViewModel : ViewModelBase
    {
        private readonly IInventoryService _inventoryService;
        private readonly IProductService _productService;

        // Tab selection
        private int _selectedTabIndex = 0;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        // Stock Overview
        private InventorySummary _summary = new();
        public InventorySummary Summary
        {
            get => _summary;
            set => SetProperty(ref _summary, value);
        }

        public ObservableCollection<ProductStockInfo> StockProducts { get; } = new();

        private ProductStockInfo? _selectedStockProduct;
        public ProductStockInfo? SelectedStockProduct
        {
            get => _selectedStockProduct;
            set
            {
                if (SetProperty(ref _selectedStockProduct, value))
                {
                    OnPropertyChanged(nameof(HasSelectedStockProduct));
                }
            }
        }

        public bool HasSelectedStockProduct => _selectedStockProduct != null;

        private string _stockSearchText = string.Empty;
        public string StockSearchText
        {
            get => _stockSearchText;
            set
            {
                if (SetProperty(ref _stockSearchText, value))
                {
                    _ = FilterStockProductsAsync();
                }
            }
        }

        private StockFilter _stockFilter = StockFilter.All;
        public StockFilter StockFilter
        {
            get => _stockFilter;
            set
            {
                if (SetProperty(ref _stockFilter, value))
                {
                    _ = FilterStockProductsAsync();
                }
            }
        }

        // Stock Movements
        public ObservableCollection<InventoryMovementModel> Movements { get; } = new();

        private InventoryMovementModel? _selectedMovement;
        public InventoryMovementModel? SelectedMovement
        {
            get => _selectedMovement;
            set => SetProperty(ref _selectedMovement, value);
        }

        private DateTime? _movementFromDate;
        public DateTime? MovementFromDate
        {
            get => _movementFromDate;
            set
            {
                if (SetProperty(ref _movementFromDate, value))
                {
                    _ = LoadMovementsAsync();
                }
            }
        }

        private DateTime? _movementToDate;
        public DateTime? MovementToDate
        {
            get => _movementToDate;
            set
            {
                if (SetProperty(ref _movementToDate, value))
                {
                    _ = LoadMovementsAsync();
                }
            }
        }

        private int? _movementProductId;
        public int? MovementProductId
        {
            get => _movementProductId;
            set
            {
                if (SetProperty(ref _movementProductId, value))
                {
                    _ = LoadMovementsAsync();
                }
            }
        }

        private string _movementProductSearch = string.Empty;
        public string MovementProductSearch
        {
            get => _movementProductSearch;
            set => SetProperty(ref _movementProductSearch, value);
        }

        private string? _selectedMovementType;
        public string? SelectedMovementType
        {
            get => _selectedMovementType;
            set
            {
                if (SetProperty(ref _selectedMovementType, value))
                {
                    _ = LoadMovementsAsync();
                }
            }
        }

        public ObservableCollection<string> MovementTypes { get; } = new();

        // Manual Adjustment
        private int _adjustmentQuantityChange;
        public int AdjustmentQuantityChange
        {
            get => _adjustmentQuantityChange;
            set => SetProperty(ref _adjustmentQuantityChange, value);
        }

        private string _adjustmentReason = string.Empty;
        public string AdjustmentReason
        {
            get => _adjustmentReason;
            set => SetProperty(ref _adjustmentReason, value);
        }

        private string _adjustmentNote = string.Empty;
        public string AdjustmentNote
        {
            get => _adjustmentNote;
            set => SetProperty(ref _adjustmentNote, value);
        }

        // Consistency Check
        public ObservableCollection<StockMismatch> Mismatches { get; } = new();

        private bool _isCheckingConsistency;
        public bool IsCheckingConsistency
        {
            get => _isCheckingConsistency;
            set => SetProperty(ref _isCheckingConsistency, value);
        }

        // Status
        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // Commands
        public RelayCommand RefreshStockCommand { get; }
        public RelayCommand FilterStockCommand { get; }
        public RelayCommand ClearStockFilterCommand { get; }
        public RelayCommand RefreshMovementsCommand { get; }
        public RelayCommand ApplyMovementFilterCommand { get; }
        public RelayCommand ClearMovementFilterCommand { get; }
        public RelayCommand RecordAdjustmentCommand { get; }
        public RelayCommand CheckConsistencyCommand { get; }

        public InventoryViewModel(IInventoryService? inventoryService = null, IProductService? productService = null)
        {
            _inventoryService = inventoryService ?? new SqliteInventoryService();
            _productService = productService ?? new SqliteProductService();

            RefreshStockCommand = new RelayCommand(async () => await LoadStockOverviewAsync());
            FilterStockCommand = new RelayCommand(async () => await FilterStockProductsAsync());
            ClearStockFilterCommand = new RelayCommand(ClearStockFilter);
            RefreshMovementsCommand = new RelayCommand(async () => await LoadMovementsAsync());
            ApplyMovementFilterCommand = new RelayCommand(async () => await LoadMovementsAsync());
            ClearMovementFilterCommand = new RelayCommand(ClearMovementFilter);
            RecordAdjustmentCommand = new RelayCommand(ExecuteRecordAdjustment, () => HasSelectedStockProduct);
            CheckConsistencyCommand = new RelayCommand(async () => await ExecuteCheckConsistencyAsync());

            _ = LoadAllDataAsync();
        }

        public async Task LoadAllDataAsync()
        {
            await LoadStockOverviewAsync();
            await LoadMovementsAsync();
            await LoadMovementTypesAsync();
        }

        private async Task LoadStockOverviewAsync()
        {
            try
            {
                Summary = await _inventoryService.GetInventorySummaryAsync();
                await FilterStockProductsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ في تحميل بيانات المخزون: {ex.Message}";
                MessageBox.Show($"تعذر تحميل بيانات المخزون:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task FilterStockProductsAsync()
        {
            try
            {
                var products = await _inventoryService.GetProductStockInfoAsync(StockSearchText, StockFilter);
                StockProducts.Clear();
                foreach (var p in products)
                {
                    StockProducts.Add(p);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ في تصفية الأصناف: {ex.Message}";
            }
        }

        private void ClearStockFilter()
        {
            StockSearchText = string.Empty;
            StockFilter = StockFilter.All;
        }

        private async Task LoadMovementsAsync()
        {
            try
            {
                var movements = await _inventoryService.GetMovementsAsync(
                    MovementFromDate,
                    MovementToDate,
                    MovementProductId,
                    SelectedMovementType);

                Movements.Clear();
                foreach (var m in movements)
                {
                    Movements.Add(m);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ في تحميل حركات المخزون: {ex.Message}";
            }
        }

        private async Task LoadMovementTypesAsync()
        {
            try
            {
                var types = await _inventoryService.GetMovementTypesAsync();
                MovementTypes.Clear();
                MovementTypes.Add("الكل");
                foreach (var t in types)
                {
                    MovementTypes.Add(t);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ في تحميل أنواع الحركات: {ex.Message}";
            }
        }

        private void ClearMovementFilter()
        {
            MovementFromDate = null;
            MovementToDate = null;
            MovementProductId = null;
            MovementProductSearch = string.Empty;
            SelectedMovementType = null;
        }

        private async void ExecuteRecordAdjustment()
        {
            if (SelectedStockProduct == null) return;

            if (AdjustmentQuantityChange == 0)
            {
                MessageBox.Show("يجب أن يكون تغيير الكمية مختلفاً عن الصفر.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(AdjustmentReason))
            {
                MessageBox.Show("يجب تحديد سبب التعديل اليدوي.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirmResult = MessageBox.Show(
                $"هل أنت متأكد من إجراء التعديل اليدوي على الصنف '{SelectedStockProduct.Name}'؟\n\n" +
                $"التغيير: {(AdjustmentQuantityChange > 0 ? "+" : "")}{AdjustmentQuantityChange}\n" +
                $"السبب: {AdjustmentReason}",
                "تأكيد التعديل اليدوي",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes) return;

            try
            {
                await _inventoryService.RecordManualAdjustmentAsync(
                    SelectedStockProduct.Id,
                    AdjustmentQuantityChange,
                    AdjustmentReason,
                    AdjustmentNote);

                AdjustmentQuantityChange = 0;
                AdjustmentReason = string.Empty;
                AdjustmentNote = string.Empty;

                await LoadStockOverviewAsync();
                await LoadMovementsAsync();

                StatusMessage = "تم تسجيل التعديل اليدوي بنجاح.";
                MessageBox.Show("تم تسجيل التعديل اليدوي بنجاح.", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = $"فشل تسجيل التعديل: {ex.Message}";
                MessageBox.Show($"فشل تسجيل التعديل:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteCheckConsistencyAsync()
        {
            IsCheckingConsistency = true;
            Mismatches.Clear();

            try
            {
                var mismatches = await _inventoryService.CheckConsistencyAsync();
                foreach (var m in mismatches)
                {
                    Mismatches.Add(m);
                }

                if (mismatches.Any())
                {
                    StatusMessage = $"تم العثور على {mismatches.Count()} عدم تطابق في المخزون.";
                    MessageBox.Show(
                        $"تم العثور على {mismatches.Count()} صنف لا يتطابق رصيده مع مجموع حركات المخزون.\n" +
                        "يرجى مراجعة القائمة أدناه.",
                        "فحص التطابق",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                else
                {
                    StatusMessage = "جميع الأرصدة متطابقة مع حركات المخزون.";
                    MessageBox.Show("جميع الأرصدة متطابقة مع حركات المخزون.", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ في فحص التطابق: {ex.Message}";
                MessageBox.Show($"فشل فحص التطابق:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsCheckingConsistency = false;
            }
        }
    }
}
