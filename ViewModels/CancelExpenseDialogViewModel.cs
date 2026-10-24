using CarPartManager.MVVM;

namespace CarPartManager.ViewModels
{
    /// <summary>
    /// نموذج عرض نافذة إلغاء مصروف (Cancel Expense Dialog ViewModel).
    /// الإلغاء بديل عن الحذف: يتطلب سبباً إجبارياً ويحتفظ بالسجل كاملاً.
    /// </summary>
    public class CancelExpenseDialogViewModel : ViewModelBase
    {
        public string DialogTitle => "إلغاء المصروف";

        public string ExpenseSummary { get; }

        public string HeaderHint { get; } =
            "سيتم الاحتفاظ بالمصروف في السجل مع حالته (ملغاة) ولن يدخل في أي إجمالي بعد ذلك — لا يوجد حذف نهائي.";

        private string _reason = string.Empty;
        public string Reason
        {
            get => _reason;
            set
            {
                if (SetProperty(ref _reason, value))
                {
                    OnPropertyChanged(nameof(CanSave));
                    SaveCommand.RaiseCanExecuteChanged();
                    if (HasError) ErrorMessage = string.Empty;
                }
            }
        }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (SetProperty(ref _errorMessage, value))
                    OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => !string.IsNullOrWhiteSpace(_errorMessage);

        public bool CanSave => !string.IsNullOrWhiteSpace(Reason) && Reason.Trim().Length >= 3;

        public Action<bool>? RequestClose { get; set; }

        public RelayCommand SaveCommand { get; }
        public RelayCommand CancelCommand { get; }

        public string ResultReason { get; private set; } = string.Empty;

        public CancelExpenseDialogViewModel(string expenseSummary)
        {
            ExpenseSummary = expenseSummary;
            SaveCommand = new RelayCommand(ExecuteSave, () => CanSave);
            CancelCommand = new RelayCommand(() => RequestClose?.Invoke(false));
        }

        private void ExecuteSave()
        {
            if (string.IsNullOrWhiteSpace(Reason))
            {
                ErrorMessage = "سبب الإلغاء إجباري — يرجى كتابة سبب واضح.";
                return;
            }

            if (Reason.Trim().Length < 3)
            {
                ErrorMessage = "يرجى كتابة سبب إلغاء أوضح (3 أحرف على الأقل).";
                return;
            }

            ResultReason = Reason.Trim();
            RequestClose?.Invoke(true);
        }
    }
}
