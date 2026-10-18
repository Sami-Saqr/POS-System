namespace CarPartManager.Models
{
    /// <summary>
    /// نموذج بيانات لقطعة الغيار ذات المخزون المنخفض التي تتطلب إعادة طلب
    /// </summary>
    public class LowStockItem
    {
        public string PartNumber { get; set; } = string.Empty;
        public string PartName { get; set; } = string.Empty;
        public string VehicleModel { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int MinimumRequired { get; set; }
        public string Unit { get; set; } = "قطعة";
        public decimal UnitPrice { get; set; }
        public string UrgencyText { get; set; } = "منخفض";
        public string UrgencyColor { get; set; } = "#EF4444"; // أحمر أو برتقالي
    }
}
