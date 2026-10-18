namespace CarPartManager.Models
{
    /// <summary>
    /// نموذج بيانات مختصر لآخر الفواتير والعمليات اليومية
    /// </summary>
    public class RecentInvoiceSummary
    {
        public string InvoiceCode { get; set; } = string.Empty;
        public string PartyName { get; set; } = string.Empty; // اسم العميل أو المورد
        public string TypeName { get; set; } = "مبيعات"; // مبيعات أو مشتريات
        public string TimeFormatted { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string PaymentMethod { get; set; } = "نقدي";
        public string BadgeColor { get; set; } = "#10B981"; // أخضر للمبيعات، أزرق للمشتريات
    }
}
