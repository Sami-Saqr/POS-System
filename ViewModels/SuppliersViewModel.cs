using System.Collections.ObjectModel;
using System.Windows;
using CarPartManager.Models;
using CarPartManager.MVVM;
using CarPartManager.Services;

namespace CarPartManager.ViewModels
{
    /// <summary>
    /// نموذج عرض شاشة إدارة الموردين (Suppliers ViewModel)
    /// </summary>
    public class SuppliersViewModel : ViewModelBase
    {
        private readonly ISupplierService _supplierService;
        private readonly List<SupplierModel> _allSuppliers = new();

        public ObservableCollection<SupplierModel> FilteredSuppliers { get; } = new();

        private SupplierModel? _selectedSupplier;
        public SupplierModel? SelectedSupplier
        {
            get => _selectedSupplier;
            set
            {
                if (SetProperty(ref _selectedSupplier, value))
                {
                    OnPropertyChanged(nameof(HasSelectedSupplier));
                    if (value != null)
                    {
                        // تعبئة نموذج التعديل بالبيانات المحددة
                        FormName = value.Name;
                        FormPhone = value.Phone;
                        FormAddress = value.Address;
                        FormCompany = value.CompanyName;
                        FormNotes = value.Notes;
                    }
                }
            }
        }

        public bool HasSelectedSupplier => _selectedSupplier != null;

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterSuppliers();
                }
            }
        }

        // حقول النموذج السريع للإضافة والتعديل
        private string _formName = string.Empty;
        public string FormName
        {
            get => _formName;
            set => SetProperty(ref _formName, value);
        }

        private string _formPhone = string.Empty;
        public string FormPhone
        {
            get => _formPhone;
            set => SetProperty(ref _formPhone, value);
        }

        private string _formAddress = string.Empty;
        public string FormAddress
        {
            get => _formAddress;
            set => SetProperty(ref _formAddress, value);
        }

        private string _formCompany = string.Empty;
        public string FormCompany
        {
            get => _formCompany;
            set => SetProperty(ref _formCompany, value);
        }

        private string _formNotes = string.Empty;
        public string FormNotes
        {
            get => _formNotes;
            set => SetProperty(ref _formNotes, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public RelayCommand SaveSupplierCommand { get; }
        public RelayCommand ClearFormCommand { get; }
        public RelayCommand DeleteSupplierCommand { get; }
        public RelayCommand RefreshCommand { get; }

        public SuppliersViewModel(ISupplierService? supplierService = null)
        {
            _supplierService = supplierService ?? new SqliteSupplierService();

            SaveSupplierCommand = new RelayCommand(async () => await ExecuteSaveSupplierAsync());
            ClearFormCommand = new RelayCommand(ClearForm);
            DeleteSupplierCommand = new RelayCommand(async () => await ExecuteDeleteSupplierAsync(), () => HasSelectedSupplier);
            RefreshCommand = new RelayCommand(async () => await LoadSuppliersAsync());

            _ = LoadSuppliersAsync();
        }

        public async Task LoadSuppliersAsync()
        {
            try
            {
                var suppliers = await _supplierService.GetAllSuppliersAsync();
                _allSuppliers.Clear();
                _allSuppliers.AddRange(suppliers);
                FilterSuppliers();
            }
            catch (Exception ex)
            {
                StatusMessage = $"خطأ في تحميل الموردين: {ex.Message}";
            }
        }

        private void FilterSuppliers()
        {
            FilteredSuppliers.Clear();
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                foreach (var s in _allSuppliers) FilteredSuppliers.Add(s);
            }
            else
            {
                var q = SearchText.Trim().ToLower();
                var matches = _allSuppliers.Where(s =>
                    s.Name.ToLower().Contains(q) ||
                    s.Phone.ToLower().Contains(q) ||
                    s.CompanyName.ToLower().Contains(q)
                );
                foreach (var s in matches) FilteredSuppliers.Add(s);
            }
        }

        private void ClearForm()
        {
            SelectedSupplier = null;
            FormName = string.Empty;
            FormPhone = string.Empty;
            FormAddress = string.Empty;
            FormCompany = string.Empty;
            FormNotes = string.Empty;
            StatusMessage = string.Empty;
        }

        private async Task ExecuteSaveSupplierAsync()
        {
            if (string.IsNullOrWhiteSpace(FormName))
            {
                MessageBox.Show("يرجى إدخال اسم المورد أو جهة التوريد.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (SelectedSupplier == null)
                {
                    // إضافة جديد
                    var newSupplier = new SupplierModel
                    {
                        Name = FormName.Trim(),
                        Phone = FormPhone.Trim(),
                        Address = FormAddress.Trim(),
                        CompanyName = FormCompany.Trim(),
                        Notes = FormNotes.Trim(),
                        IsActive = true
                    };
                    await _supplierService.AddSupplierAsync(newSupplier);
                    StatusMessage = $"تم حفظ المورد '{newSupplier.Name}' بنجاح.";
                }
                else
                {
                    // تعديل حالي
                    SelectedSupplier.Name = FormName.Trim();
                    SelectedSupplier.Phone = FormPhone.Trim();
                    SelectedSupplier.Address = FormAddress.Trim();
                    SelectedSupplier.CompanyName = FormCompany.Trim();
                    SelectedSupplier.Notes = FormNotes.Trim();

                    await _supplierService.UpdateSupplierAsync(SelectedSupplier);
                    StatusMessage = $"تم تحديث بيانات المورد '{SelectedSupplier.Name}' بنجاح.";
                }

                await LoadSuppliersAsync();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"حدث خطأ أثناء حفظ بيانات المورد:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ExecuteDeleteSupplierAsync()
        {
            if (SelectedSupplier == null) return;

            var confirm = MessageBox.Show(
                $"هل أنت متأكد من حذف المورد '{SelectedSupplier.Name}'؟",
                "تأكيد حذف المورد",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                try
                {
                    await _supplierService.DeleteSupplierAsync(SelectedSupplier.Id);
                    StatusMessage = $"تم حذف المورد بنجاح.";
                    await LoadSuppliersAsync();
                    ClearForm();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"حدث خطأ أثناء الحذف:\n{ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
