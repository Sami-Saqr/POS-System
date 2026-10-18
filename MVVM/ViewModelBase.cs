using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CarPartManager.MVVM
{
    /// <summary>
    /// كلاس أساسي لجميع الـ ViewModels يطبق واجهة INotifyPropertyChanged
    /// ليتم إشعار واجهة المستخدم (XAML) تلقائياً عند تغيير أي خاصية (Property).
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// استدعاء حدث التغيير لخاصية معينة.
        /// CallerMemberName يسمح باستدعاء الدالة بدون كتابة اسم الخاصية يدوياً.
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// تحديث قيمة الحقل وإشعار الواجهة فقط إذا تغيرت القيمة فعلياً.
        /// </summary>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
