namespace CarPartManager.Models
{
    /// <summary>
    /// صف في تقرير أفضل الأصناف (بالكمية أو بالقيمة).
    /// </summary>
    public class TopProductReportRow
    {
        public int ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        /// <summary>الكمية المبيعة (بالوحدات)</summary>
        public int QuantitySold { get; set; }

        /// <summary>قيمة المبيعات (الكمية × سعر الوحدة قبل أي خصم على مستوى الفاتورة)</summary>
        public decimal SalesValue { get; set; }
    }

    /// <summary>
    /// أفضل الأصناف مبيعاً في الفترة — مرة حسب الكمية ومرة حسب القيمة.
    /// </summary>
    public class TopProductsModel
    {
        public List<TopProductReportRow> ByQuantity { get; set; } = new();
        public List<TopProductReportRow> ByValue { get; set; } = new();
    }
}
