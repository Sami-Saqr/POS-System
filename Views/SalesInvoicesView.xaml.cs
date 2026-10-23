using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CarPartManager.ViewModels;

namespace CarPartManager.Views
{
    /// <summary>
    /// كود شاشة نقطة البيع (الكاشير) مع إدارة قارئ الباركود والتركيز التلقائي
    /// </summary>
    public partial class SalesInvoicesView : UserControl
    {
        public SalesInvoicesView()
        {
            InitializeComponent();
            DataContextChanged += SalesInvoicesView_DataContextChanged;
            Loaded += (s, e) => FocusBarcodeInput();
        }

        private void SalesInvoicesView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is SalesInvoicesViewModel vm)
            {
                vm.FocusBarcodeInput = FocusBarcodeInput;
            }
        }

        private void FocusBarcodeInput()
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (BarcodeTextBox != null && BarcodeTextBox.IsEnabled && BarcodeTextBox.IsVisible)
                {
                    BarcodeTextBox.Focus();
                    BarcodeTextBox.SelectAll();
                }
            });
        }

        private void BarcodeTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not SalesInvoicesViewModel vm) return;

            if (e.Key == Key.Down)
            {
                if (vm.IsBarcodePopupOpen && vm.BarcodeSuggestions.Count > 0)
                {
                    vm.SelectNextBarcodeSuggestion();
                    if (vm.SelectedBarcodeSuggestion != null)
                    {
                        BarcodeSuggestionsListBox?.ScrollIntoView(vm.SelectedBarcodeSuggestion);
                    }
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Up)
            {
                if (vm.IsBarcodePopupOpen && vm.BarcodeSuggestions.Count > 0)
                {
                    vm.SelectPreviousBarcodeSuggestion();
                    if (vm.SelectedBarcodeSuggestion != null)
                    {
                        BarcodeSuggestionsListBox?.ScrollIntoView(vm.SelectedBarcodeSuggestion);
                    }
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                if (vm.IsBarcodePopupOpen)
                {
                    vm.CloseBarcodeSuggestions();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Enter)
            {
                e.Handled = true;

                // تحديث فوري لمصدر الـ Binding قبل تنفيذ الأمر لضمان التقاط كامل كود الباركود
                var binding = BarcodeTextBox.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();

                // 1. إذا كان هناك صنف مقترح محدد بالأسهم (Up/Down) يتم اختياره
                if (vm.IsBarcodePopupOpen && vm.SelectedBarcodeSuggestion != null)
                {
                    vm.SelectBarcodeSuggestionCommand.Execute(vm.SelectedBarcodeSuggestion);
                    return;
                }

                // 2. قارئ الباركود USB أو الضغط على Enter دون تحديد مقترح: مطابقة تامة فورية
                vm.CloseBarcodeSuggestions();

                if (vm.AddItemByBarcodeCommand.CanExecute(null))
                {
                    vm.AddItemByBarcodeCommand.Execute(null);
                }
            }
        }

        private void BarcodeSuggestionItem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Models.ProductModel product)
            {
                if (DataContext is SalesInvoicesViewModel vm)
                {
                    vm.SelectBarcodeSuggestionCommand.Execute(product);
                    e.Handled = true;
                }
            }
        }

        private void CustomerNameTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not SalesInvoicesViewModel vm) return;

            if (e.Key == Key.Down)
            {
                if (vm.IsCustomerPopupOpen && vm.CustomerSuggestions.Count > 0)
                {
                    vm.SelectNextCustomerSuggestion();
                    if (vm.SelectedCustomerSuggestion != null)
                    {
                        CustomerSuggestionsListBox?.ScrollIntoView(vm.SelectedCustomerSuggestion);
                    }
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Up)
            {
                if (vm.IsCustomerPopupOpen && vm.CustomerSuggestions.Count > 0)
                {
                    vm.SelectPreviousCustomerSuggestion();
                    if (vm.SelectedCustomerSuggestion != null)
                    {
                        CustomerSuggestionsListBox?.ScrollIntoView(vm.SelectedCustomerSuggestion);
                    }
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                if (vm.IsCustomerPopupOpen)
                {
                    vm.CloseCustomerSuggestions();
                    e.Handled = true;
                    FocusBarcodeInput();
                }
            }
            else if (e.Key == Key.Enter)
            {
                e.Handled = true;

                var binding = CustomerNameTextBox.GetBindingExpression(TextBox.TextProperty);
                binding?.UpdateSource();

                if (vm.IsCustomerPopupOpen && vm.SelectedCustomerSuggestion != null)
                {
                    vm.SelectCustomerSuggestionCommand.Execute(vm.SelectedCustomerSuggestion);
                }
                else
                {
                    vm.CloseCustomerSuggestions();
                }

                FocusBarcodeInput();
            }
        }

        private void CustomerSuggestionItem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is Models.CustomerModel customer)
            {
                if (DataContext is SalesInvoicesViewModel vm)
                {
                    vm.SelectCustomerSuggestionCommand.Execute(customer);
                    FocusBarcodeInput();
                    e.Handled = true;
                }
            }
        }
    }
}

