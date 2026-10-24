namespace CarPartManager.Models
{
    /// <summary>صف يومي في تقرير المشتريات (قراءة فقط)</summary>
    public class PurchaseDailyReportRow
    {
        public DateTime Day { get; set; }
        public int InvoiceCount { get; set; }
        public decimal Total { get; set; }
        public string DayText => Day.ToString("yyyy-MM-dd");
    }

    /// <summary>إجمالي المشتريات حسب المورد</summary>
    public class SupplierPurchaseReportRow
    {
        public string SupplierName { get; set; } = string.Empty;
        public int InvoiceCount { get; set; }
        public decimal Total { get; set; }
        public decimal SharePercent { get; set; }
    }

    /// <summary>
    /// تقرير المشتريات لفترة — الفواتير المعتمدة فقط (المسودة والملغاة مستبعدة).
    /// </summary>
    public class PurchaseReportModel
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEndExclusive { get; set; }
        public decimal Total { get; set; }
        public int InvoiceCount { get; set; }

        public List<PurchaseDailyReportRow> Daily { get; set; } = new();
        public List<SupplierPurchaseReportRow> BySupplier { get; set; } = new();
        public List<TopProductReportRow> TopProducts { get; set; } = new();

        /// <summary>هل توجد بيانات موردين مرتبطة بفواتير الفترة؟</summary>
        public bool HasSupplierData { get; set; }

        public decimal? PreviousTotal { get; set; }
        public decimal Change => PreviousTotal.HasValue ? Total - PreviousTotal.Value : 0m;
        public decimal? ChangePercent => ProfitSummaryModel.PercentChange(Total, PreviousTotal);
    }
}
