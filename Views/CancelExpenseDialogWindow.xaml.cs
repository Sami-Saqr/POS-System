using System.Windows;
using CarPartManager.ViewModels;

namespace CarPartManager.Views
{
    /// <summary>
    /// نافذة تأكيد إلغاء مصروف مع سبب إجباري (Interaction logic for CancelExpenseDialogWindow.xaml)
    /// </summary>
    public partial class CancelExpenseDialogWindow : Window
    {
        public CancelExpenseDialogWindow(CancelExpenseDialogViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            viewModel.RequestClose = (result) =>
            {
                DialogResult = result;
                Close();
            };
        }
    }
}
