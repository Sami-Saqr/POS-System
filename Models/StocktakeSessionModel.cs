using CarPartManager.MVVM;

namespace CarPartManager.Models
{
    /// <summary>
    /// نموذج جلسة الجرد (Stocktake Session)
    /// </summary>
    public class StocktakeSessionModel : ViewModelBase
    {
        private int _id;
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private string _sessionNumber = string.Empty;
        public string SessionNumber
        {
            get => _sessionNumber;
            set => SetProperty(ref _sessionNumber, value);
        }

        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _notes = string.Empty;
        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        private string _status = "مسودة";
        public string Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                {
                    OnPropertyChanged(nameof(IsDraft));
                    OnPropertyChanged(nameof(IsCounting));
                    OnPropertyChanged(nameof(IsReview));
                    OnPropertyChanged(nameof(IsApproved));
                    OnPropertyChanged(nameof(IsCancelled));
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        private DateTime _sessionDate = DateTime.Now;
        public DateTime SessionDate
        {
            get => _sessionDate;
            set => SetProperty(ref _sessionDate, value);
        }

        private DateTime? _startDate;
        public DateTime? StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        private DateTime? _approvedDate;
        public DateTime? ApprovedDate
        {
            get => _approvedDate;
            set => SetProperty(ref _approvedDate, value);
        }

        private DateTime? _cancelledDate;
        public DateTime? CancelledDate
        {
            get => _cancelledDate;
            set => SetProperty(ref _cancelledDate, value);
        }

        private string _cancellationReason = string.Empty;
        public string CancellationReason
        {
            get => _cancellationReason;
            set => SetProperty(ref _cancellationReason, value);
        }

        private string _scopeType = "كامل"; // كامل / تصنيف / مخصص
        public string ScopeType
        {
            get => _scopeType;
            set => SetProperty(ref _scopeType, value);
        }

        private string _scopeFilter = string.Empty; // category name or filter criteria
        public string ScopeFilter
        {
            get => _scopeFilter;
            set => SetProperty(ref _scopeFilter, value);
        }

        /// <summary>
        /// بنود الجرد المرتبطة بالجلسة (علاقة رأس بأطراف مع StocktakeLineModel عبر المفتاح SessionId).
        /// </summary>
        public List<StocktakeLineModel> Lines { get; set; } = new();

        // Computed properties
        public bool IsDraft => Status == "مسودة";
        public bool IsCounting => Status == "جاري العد";
        public bool IsReview => Status == "مراجعة";
        public bool IsApproved => Status == "معتمد";
        public bool IsCancelled => Status == "ملغاة";

        public string StatusText => Status switch
        {
            "مسودة" => "مسودة",
            "جاري العد" => "جاري العد",
            "مراجعة" => "مراجعة",
            "معتمد" => "معتمد",
            "ملغاة" => "ملغاة",
            _ => Status
        };

        public string StatusColor => Status switch
        {
            "مسودة" => "#64748B",
            "جاري العد" => "#D97706",
            "مراجعة" => "#0891B2",
            "معتمد" => "#059669",
            "ملغاة" => "#DC2626",
            _ => "#64748B"
        };
    }
}
