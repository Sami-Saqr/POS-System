using System.Windows;

namespace CarPartManager.Views
{
    /// <summary>
    /// نافذة منبثقة لإنشاء جلسة جرد جديدة (تجمّع اسم الجلسة والملاحظات فقط).
    /// </summary>
    public partial class CreateStocktakeSessionWindow : Window
    {
        /// <summary>اسم الجلسة الذي أدخله المستخدم (بعد القيمة الصالحة فقط).</summary>
        public string SessionName { get; private set; } = string.Empty;

        /// <summary>ملاحظات الجلسة التي أدخلها المستخدم.</summary>
        public string Notes { get; private set; } = string.Empty;

        public CreateStocktakeSessionWindow()
        {
            InitializeComponent();
        }

        private void Create_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                MessageBox.Show("يرجى إدخال اسم الجلسة.", "خطأ", MessageBoxButton.OK, MessageBoxImage.Warning);
                NameBox.Focus();
                return;
            }

            SessionName = NameBox.Text.Trim();
            Notes = NotesBox.Text;
            DialogResult = true;
        }
    }
}
