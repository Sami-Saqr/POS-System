using CarPartManager.MVVM;

namespace CarPartManager.ViewModels
{
    /// <summary>
    /// نموذج عرض مؤقت للأقسام التي سيتم برمجتها وتطويرها في المراحل اللاحقة.
    /// يسمح باختبار وتجربة التنقل (Navigation) في التطبيق بشكل تفاعلي.
    /// </summary>
    public class PlaceholderViewModel : ViewModelBase
    {
        private string _pageTitle = string.Empty;
        public string PageTitle
        {
            get => _pageTitle;
            set => SetProperty(ref _pageTitle, value);
        }

        private string _pageDescription = string.Empty;
        public string PageDescription
        {
            get => _pageDescription;
            set => SetProperty(ref _pageDescription, value);
        }

        private string _badgeText = "قيد الإعداد للمرحلة القادمة";
        public string BadgeText
        {
            get => _badgeText;
            set => SetProperty(ref _badgeText, value);
        }

        public PlaceholderViewModel(string title, string description)
        {
            _pageTitle = title;
            _pageDescription = description;
        }
    }
}
