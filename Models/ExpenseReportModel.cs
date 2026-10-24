namespace CarPartManager.Models
{
    /// <summary>
    /// إجمالي مصروفات تصنيف واحد خلال الفترة.
    /// </summary>
    public class ExpenseCategoryReportRow
    {
        public int CategoryId { get; set; }

        public string CategoryName { get; set; } = string.Empty;

        /// <summary>إجمالي المصروفات النشطة للتصنيف</summary>
        public decimal Total { get; set; }

        public int Count { get; set; }

        /// <summary>نسبة التصنيف من إجمالي مصروفات الفترة (٪)</summary>
        public decimal SharePercent { get; set; }
    }

    /// <summary>
    /// سطر تفصيلي في تقرير المصروفات (المصروفات الملغاة لا تظهر ولا تدخل في أي إجمالي).
    /// </summary>
    public class ExpenseReportDetailRow
    {
        public DateTime ExpenseDate { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? ReferenceNumber { get; set; }
        public string? CreatedBy { get; set; }

        public string ExpenseDateText => ExpenseDate.ToString("yyyy-MM-dd");
    }

    /// <summary>
    /// تقرير المصروفات الكامل: الإجمالي، التفصيل حسب التصنيف، القائمة التفصيلية، ومقارنة بالفترة السابقة.
    /// </summary>
    public class ExpenseReportModel
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEndExclusive { get; set; }

        public decimal Total { get; set; }
        public int Count { get; set; }

        public List<ExpenseCategoryReportRow> Categories { get; set; } = new();
        public List<ExpenseReportDetailRow> Details { get; set; } = new();

        /// <summary>إجمالي مصروفات الفترة السابقة المماثلة</summary>
        public decimal? PreviousTotal { get; set; }

        public decimal Change => Total - (PreviousTotal ?? 0m);

        public decimal? ChangePercent => ProfitSummaryModel.PercentChange(Total, PreviousTotal);
    }
}
