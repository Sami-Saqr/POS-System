using System.Windows;
using CarPartManager.ViewModels;

namespace CarPartManager.Views
{
    /// <summary>
    /// Interaction logic for ProductDialogWindow.xaml
    /// </summary>
    public partial class ProductDialogWindow : Window
    {
        public ProductDialogWindow(ProductDialogViewModel viewModel)
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
