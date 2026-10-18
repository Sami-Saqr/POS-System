using System.Collections.ObjectModel;
using System.Windows;
using CarPartManager.Models;
using CarPartManager.MVVM;
using CarPartManager.Services;

namespace CarPartManager.ViewModels
{
    /// <summary>
    /// نموذج عرض صفحة سجل فواتير المبيعات السابقة المستقلة.
    /// يُحمِّل جميع الفواتير من قاعدة البيانات ويدعم البحث والتصفية وعرض التفاصيل.
    /// </summary>
    public class SalesInvoicesHistoryViewModel : ViewModelBase
    {
        private readonly ISalesInvoiceService _invoiceService;
        private readonly List<SalesInvoiceModel> _allInvoices = new();

        // ── قوائم البيانات ──────────────────────────────────────────
        public ObservableCollection<SalesInvoiceModel> InvoicesHistory { get; } = new();

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
        private SalesInvoiceModel? _selectedInvoice;
        public SalesInvoiceModel? SelectedInvoice
        {
            get => _selectedInvoice;
            set => SetProperty(ref _selectedInvoice, value);
        }

        // ── الأوامر ─────────────────────────────────────────────────
        public RelayCommand RefreshCommand { get; }
        public RelayCommand ViewInvoiceCommand { get; }

        public SalesInvoicesHistoryViewModel(ISalesInvoiceService? invoiceService = null)
        {
            _invoiceService = invoiceService ?? new SqliteSalesInvoiceService();

            RefreshCommand  = new RelayCommand(async () => await LoadAllDataAsync());
            ViewInvoiceCommand = new RelayCommand(p =>
            {
                if (p is SalesInvoiceModel inv)
                    SelectedInvoice = inv;
            });
        }

        // ── تحميل البيانات (يُستدعى من MainViewModel عند كل تنقل) ──
        public async Task LoadAllDataAsync()
        {
            IsLoading = true;
            StatusMessage = "جارٍ تحميل سجل فواتير المبيعات...";
            InvoicesHistory.Clear();
            OnPropertyChanged(nameof(ShowEmptyState));

            try
            {
                var invoices = await _invoiceService.GetAllInvoicesAsync();
                _allInvoices.Clear();
                _allInvoices.AddRange(invoices);
                FilterInvoicesHistory();
                StatusMessage = $"تم تحميل {_allInvoices.Count} فاتورة مبيعات.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ في تحميل سجل فواتير المبيعات: {ex.Message}";
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
                    (inv.CustomerName != null && inv.CustomerName.ToLower().Contains(SearchText.Trim().ToLower())) ||
                    (inv.CustomerPhone != null && inv.CustomerPhone.ToLower().Contains(SearchText.Trim().ToLower())) ||
                    (inv.Notes != null && inv.Notes.ToLower().Contains(SearchText.Trim().ToLower()))
                  ).ToList();

            foreach (var inv in source) InvoicesHistory.Add(inv);
            OnPropertyChanged(nameof(ShowEmptyState));
        }
    }
}
