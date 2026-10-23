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
        private readonly IStocktakeService _stocktakeService;

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

        public InventoryViewModel(IInventoryService? inventoryService = null, IProductService? productService = null, IStocktakeService? stocktakeService = null)
        {
            _inventoryService = inventoryService ?? new SqliteInventoryService();
            _productService = productService ?? new SqliteProductService();
            _stocktakeService = stocktakeService ?? new SqliteStocktakeService();

            RefreshStockCommand = new RelayCommand(async () => await LoadStockOverviewAsync());
            FilterStockCommand = new RelayCommand(async () => await FilterStockProductsAsync());
            ClearStockFilterCommand = new RelayCommand(ClearStockFilter);
            RefreshMovementsCommand = new RelayCommand(async () => await LoadMovementsAsync());
            ApplyMovementFilterCommand = new RelayCommand(async () => await LoadMovementsAsync());
            ClearMovementFilterCommand = new RelayCommand(ClearMovementFilter);
            RecordAdjustmentCommand = new RelayCommand(ExecuteRecordAdjustment, () => HasSelectedStockProduct);
            CheckConsistencyCommand = new RelayCommand(async () => await ExecuteCheckConsistencyAsync());

            // Stocktake commands
            CreateNewSessionCommand = new RelayCommand(ExecuteCreateNewSession);
            LoadSessionsCommand = new RelayCommand(async () => await LoadStocktakeHistoryAsync());
            StartCountingCommand = new RelayCommand(async () => await ExecuteStartCountingAsync(), () => HasCurrentSession && CurrentSession.IsDraft);
            SubmitReviewCommand = new RelayCommand(async () => await ExecuteSubmitReviewAsync(), () => HasCurrentSession && CurrentSession.IsCounting);
            ApproveSessionCommand = new RelayCommand(async () => await ExecuteApproveSessionAsync(), () => HasCurrentSession && CurrentSession.IsReview);
            CancelSessionCommand = new RelayCommand(async () => await ExecuteCancelSessionAsync(), () => HasCurrentSession && !CurrentSession.IsApproved);
            CountProductCommand = new RelayCommand(async () => await ExecuteCountProductAsync());
            CountByBarcodeCommand = new RelayCommand(async () => await ExecuteCountByBarcodeAsync());
            RefreshStocktakeCommand = new RelayCommand(async () => await RefreshStocktakeAsync());

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

        // Stocktake Session Management
        private StocktakeSessionModel? _currentSession;
        public StocktakeSessionModel? CurrentSession
        {
            get => _currentSession;
            set
            {
                if (SetProperty(ref _currentSession, value))
                {
                    OnPropertyChanged(nameof(HasCurrentSession));
                    OnPropertyChanged(nameof(CanCreateNewSession));
                    OnPropertyChanged(nameof(SessionStatusText));
                    OnPropertyChanged(nameof(SessionStatusColor));
                }
            }
        }

        public bool HasCurrentSession => _currentSession != null;
        public bool CanCreateNewSession => _currentSession == null;

        public string SessionStatusText => CurrentSession?.StatusText ?? string.Empty;
        public string SessionStatusColor => CurrentSession?.StatusColor ?? "#64748B";

        public ObservableCollection<StocktakeLineModel> StocktakeLines { get; } = new();
        public ObservableCollection<StocktakeSessionModel> StocktakeHistory { get; } = new();

        private StocktakeLineModel? _selectedStocktakeLine;
        public StocktakeLineModel? SelectedStocktakeLine
        {
            get => _selectedStocktakeLine;
            set => SetProperty(ref _selectedStocktakeLine, value);
        }

        private string _stocktakeSearch = string.Empty;
        public string StocktakeSearch
        {
            get => _stocktakeSearch;
            set => SetProperty(ref _stocktakeSearch, value);
        }

        private int _stocktakeCountQuantity;
        public int StocktakeCountQuantity
        {
            get => _stocktakeCountQuantity;
            set => SetProperty(ref _stocktakeCountQuantity, value);
        }

        private string _stocktakeCountNotes = string.Empty;
        public string StocktakeCountNotes
        {
            get => _stocktakeCountNotes;
            set => SetProperty(ref _stocktakeCountNotes, value);
        }

        // Stocktake Commands
        public RelayCommand CreateNewSessionCommand { get; }
        public RelayCommand LoadSessionsCommand { get; }
        public RelayCommand StartCountingCommand { get; }
        public RelayCommand SubmitReviewCommand { get; }
        public RelayCommand ApproveSessionCommand { get; }
        public RelayCommand CancelSessionCommand { get; }
        public RelayCommand CountProductCommand { get; }
        public RelayCommand CountByBarcodeCommand { get; }
        public RelayCommand RefreshStocktakeCommand { get; }

        // Add these to the constructor after existing commands
        // For now, I'll add the command initialization in a separate edit

        private void ExecuteCreateNewSession()
        {
            var dialog = new Window
            {
                Title = "إنشاء جلسة جرد جديدة",
                Width = 400,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow
            };

            var grid = new Grid { Margin = 20 };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var nameBox = new TextBox { Margin = new Thickness(0, 10, 0, 10) };
            var notesBox = new TextBox { Margin = new Thickness(0, 10, 0, 10), AcceptsReturn = true, Height = 60 };

            grid.Children.Add(new TextBlock { Text = "اسم الجلسة:" }, 0, 0);
            grid.Children.Add(nameBox, 1, 0);
            grid.Children.Add(new TextBlock { Text = "ملاحظات:" }, 2, 0);
            grid.Children.Add(notesBox, 3, 0);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okBtn = new Button { Content = "إنشاء", Width = 100, Margin = new Thickness(5, 0, 5, 0) };
            var cancelBtn = new Button { Content = "إلغاء", Width = 100, Margin = new Thickness(5, 0, 5, 0) };
            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);
            grid.Children.Add(btnPanel, 4, 0);

            dialog.Content = grid;

            okBtn.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(nameBox.Text))
                {
                    MessageBox.Show("يرجى إدخال اسم الجلسة.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                try
                {
                    var session = await _stocktakeService.CreateSessionAsync(nameBox.Text, "كامل", null, notesBox.Text);
                    CurrentSession = session;
                    await LoadStocktakeLinesAsync();
                    dialog.Close();
                    StatusMessage = $"تم إنشاء جلسة الجرد {session.SessionNumber}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"فشل إنشاء الجلسة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            cancelBtn.Click += (s, e) => dialog.Close();

            dialog.ShowDialog();
        }

        private async Task LoadStocktakeHistoryAsync()
        {
            try
            {
                var sessions = await _stocktakeService.GetAllSessionsAsync();
                StocktakeHistory.Clear();
                foreach (var s in sessions)
                {
                    StocktakeHistory.Add(s);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ في تحميل سجل الجرد: {ex.Message}";
            }
        }

        private async Task LoadStocktakeLinesAsync()
        {
            if (CurrentSession == null) return;

            try
            {
                var lines = await _stocktakeService.GetLinesAsync(CurrentSession.Id);
                StocktakeLines.Clear();
                foreach (var l in lines)
                {
                    StocktakeLines.Add(l);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ في تحميل بنود الجرد: {ex.Message}";
            }
        }

        private async Task ExecuteStartCountingAsync()
        {
            if (CurrentSession == null) return;

            try
            {
                await _stocktakeService.StartCountingAsync(CurrentSession.Id);
                CurrentSession = await _stocktakeService.GetSessionByIdAsync(CurrentSession.Id);
                StatusMessage = "بدء العد - يمكنك الآن إدخال الكميات المعدودة";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل بدء العد: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteSubmitReviewAsync()
        {
            if (CurrentSession == null) return;

            try
            {
                await _stocktakeService.SubmitForReviewAsync(CurrentSession.Id);
                CurrentSession = await _stocktakeService.GetSessionByIdAsync(CurrentSession.Id);
                StatusMessage = "تم تقديم الجرد للمراجعة";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل التقديم للمراجعة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteApproveSessionAsync()
        {
            if (CurrentSession == null) return;

            var summary = await _stocktakeService.GetSessionSummaryAsync(CurrentSession.Id);

            var message = $"هل أنت متأكد من اعتماد جلسة الجرد {CurrentSession.SessionNumber}؟\n\n" +
                         $"البنود المعدودة: {summary.CountedLines}/{summary.TotalLines}\n" +
                         $"بنود مع اختلاف: {summary.LinesWithDifference}\n" +
                         $"إجمالي النقص: {summary.TotalShortage} (قيمة: {summary.ShortageValue:N2})\n" +
                         $"إجمالي الفائض: {summary.TotalSurplus} (قيمة: {summary.SurplusValue:N2})";

            var result = MessageBox.Show(message, "تأكيد الاعتماد", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _stocktakeService.ApproveSessionAsync(CurrentSession.Id);
                CurrentSession = await _stocktakeService.GetSessionByIdAsync(CurrentSession.Id);
                await LoadStockOverviewAsync(); // Refresh stock
                StatusMessage = "تم اعتماد جلسة الجرد بنجاح";
                MessageBox.Show("تم اعتماد جلسة الجرد بنجاح.", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل الاعتماد: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteCancelSessionAsync()
        {
            if (CurrentSession == null) return;

            var result = MessageBox.Show("هل أنت متأكد من إلغاء جلسة الجرد؟", "تأكيد الإلغاء", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _stocktakeService.CancelSessionAsync(CurrentSession.Id, "إلغاء من قبل المستخدم");
                CurrentSession = null;
                StocktakeLines.Clear();
                StatusMessage = "تم إلغاء جلسة الجرد";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل الإلغاء: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteCountProductAsync()
        {
            if (CurrentSession == null || SelectedStocktakeLine == null) return;

            if (StocktakeCountQuantity < 0)
            {
                MessageBox.Show("الكمية لا يمكن أن تكون سالبة.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                await _stocktakeService.CountProductAsync(CurrentSession.Id, SelectedStocktakeLine.ProductId, StocktakeCountQuantity, StocktakeCountNotes);
                await LoadStocktakeLinesAsync();
                StocktakeCountQuantity = 0;
                StocktakeCountNotes = string.Empty;
                StatusMessage = "تم تسجيل العد";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل تسجيل العد: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteCountByBarcodeAsync()
        {
            if (CurrentSession == null || string.IsNullOrWhiteSpace(StocktakeSearch)) return;

            if (StocktakeCountQuantity < 0)
            {
                MessageBox.Show("الكمية لا يمكن أن تكون سالبة.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var success = await _stocktakeService.CountByBarcodeAsync(CurrentSession.Id, StocktakeSearch, StocktakeCountQuantity, StocktakeCountNotes);
                if (!success)
                {
                    MessageBox.Show("الباركود غير موجود في هذه الجلسة.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await LoadStocktakeLinesAsync();
                StocktakeCountQuantity = 0;
                StocktakeCountNotes = string.Empty;
                StocktakeSearch = string.Empty;
                StatusMessage = "تم تسجيل العد بالباركود";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل تسجيل العد: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task RefreshStocktakeAsync()
        {
            if (CurrentSession != null)
            {
                CurrentSession = await _stocktakeService.GetSessionByIdAsync(CurrentSession.Id);
                await LoadStocktakeLinesAsync();
            }
            await LoadStocktakeHistoryAsync();
        }
    }
}
