using System.Windows;
using CarPartManager.ViewModels;

namespace CarPartManager.Views
{
    /// <summary>
    /// نافذة إضافة أو تعديل مصروف (Interaction logic for ExpenseDialogWindow.xaml)
    /// </summary>
    public partial class ExpenseDialogWindow : Window
    {
        public ExpenseDialogWindow(ExpenseDialogViewModel viewModel)
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
