using System.Collections.ObjectModel;
using System.Windows;
using CarPartManager.Models;
using CarPartManager.MVVM;
using CarPartManager.Services;

namespace CarPartManager.ViewModels
{
    /// <summary>
    /// نموذج عرض صفحة سجل فواتير المشتريات السابقة المستقلة.
    /// يُحمِّل جميع الفواتير من قاعدة البيانات ويدعم البحث والتصفية وعرض التفاصيل.
    /// </summary>
    public class PurchaseInvoicesHistoryViewModel : ViewModelBase
    {
        private readonly IPurchaseInvoiceService _invoiceService;
        private readonly List<PurchaseInvoiceModel> _allInvoices = new();

        // ── قوائم البيانات ──────────────────────────────────────────
        public ObservableCollection<PurchaseInvoiceModel> InvoicesHistory { get; } = new();

        // ── حالة التحميل ────────────────────────────────────────────
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (SetProperty(ref _isLoading, value))
                    OnPropertyChanged(nameof(ShowEmptyState));
            }
        }

        public bool ShowEmptyState => !IsLoading && InvoicesHistory.Count == 0;

        // ── البحث والتصفية ──────────────────────────────────────────
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    FilterInvoicesHistory();
            }
        }

        // ── رسالة الحالة ────────────────────────────────────────────
        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // ── الفاتورة المحددة للمعاينة ───────────────────────────────
        private PurchaseInvoiceModel? _selectedInvoice;
        public PurchaseInvoiceModel? SelectedInvoice
        {
            get => _selectedInvoice;
            set => SetProperty(ref _selectedInvoice, value);
        }

        // ── الأوامر ─────────────────────────────────────────────────
        public RelayCommand RefreshCommand  { get; }
        public RelayCommand ViewInvoiceCommand { get; }

        public PurchaseInvoicesHistoryViewModel(IPurchaseInvoiceService? invoiceService = null)
        {
            _invoiceService = invoiceService ?? new SqlitePurchaseInvoiceService();

            RefreshCommand  = new RelayCommand(async () => await LoadAllDataAsync());
            ViewInvoiceCommand = new RelayCommand(p =>
            {
                if (p is PurchaseInvoiceModel inv)
                    SelectedInvoice = inv;
            });
        }

        // ── تحميل البيانات (يُستدعى من MainViewModel عند كل تنقل) ──
        public async Task LoadAllDataAsync()
        {
            IsLoading = true;
            StatusMessage = "جارٍ تحميل سجل فواتير المشتريات...";
            InvoicesHistory.Clear();
            OnPropertyChanged(nameof(ShowEmptyState));

            try
            {
                var invoices = await _invoiceService.GetAllInvoicesAsync();
                _allInvoices.Clear();
                _allInvoices.AddRange(invoices);
                FilterInvoicesHistory();
                StatusMessage = $"تم تحميل {_allInvoices.Count} فاتورة مشتريات.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ في تحميل سجل فواتير المشتريات: {ex.Message}";
                MessageBox.Show(StatusMessage, "خطأ في التحميل", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                OnPropertyChanged(nameof(ShowEmptyState));
            }
        }

        private void FilterInvoicesHistory()
        {
            InvoicesHistory.Clear();
            var source = string.IsNullOrWhiteSpace(SearchText)
                ? _allInvoices
                : _allInvoices.Where(inv =>
                    inv.InvoiceNumber.ToLower().Contains(SearchText.Trim().ToLower()) ||
                    (inv.SupplierName != null && inv.SupplierName.ToLower().Contains(SearchText.Trim().ToLower())) ||
                    (inv.Notes != null && inv.Notes.ToLower().Contains(SearchText.Trim().ToLower()))
                  ).ToList();

            foreach (var inv in source) InvoicesHistory.Add(inv);
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }
}
