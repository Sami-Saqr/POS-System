using CarPartManager.MVVM;

namespace CarPartManager.Models
{
    /// <summary>
    /// يمثل عنصر تنقل في القائمة الجانبية (Sidebar Navigation Item)
    /// </summary>
    public class NavItem : ViewModelBase
    {
        public string Tag { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string IconPathData { get; set; } = string.Empty;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public NavItem(string tag, string title, string iconPathData, bool isSelected = false)
        {
            Tag = tag;
            Title = title;
            IconPathData = iconPathData;
            _isSelected = isSelected;
        }
    }
}
