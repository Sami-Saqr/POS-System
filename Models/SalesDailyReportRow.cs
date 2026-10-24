namespace CarPartManager.Models
{
    /// <summary>
    /// صف واحد في تقرير المبيعات اليومي.
    /// </summary>
    public class SalesDailyReportRow
    {
        public DateTime Day { get; set; }

        /// <summary>عدد فواتير المبيعات المعتمدة في هذا اليوم</summary>
        public int InvoiceCount { get; set; }

        /// <summary>إجمالي المبيعات قبل الخصم في هذا اليوم</summary>
        public decimal Total { get; set; }

        /// <summary>إجمالي الخصومات في هذا اليوم</summary>
        public decimal Discount { get; set; }

        /// <summary>قيمة مرتجعات المبيعات المعتمدة في هذا اليوم</summary>
        public decimal ReturnsTotal { get; set; }

        /// <summary>صافي اليوم = الإجمالي − الخصم − المرتجعات</summary>
        public decimal Net => Total - Discount - ReturnsTotal;

        public string DayText => Day.ToString("yyyy-MM-dd");
    }
}
