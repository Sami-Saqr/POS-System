namespace CarPartManager.Models
{
    /// <summary>
    /// مبيعات عميل واحد خلال الفترة (العملاء النقديون يُجمعون تحت "عميل نقدي").
    /// </summary>
    public class CustomerSalesReportRow
    {
        public string CustomerName { get; set; } = "عميل نقدي";

        public int InvoiceCount { get; set; }

        /// <summary>صافي مبيعات العميل (بعد الخصم) خلال الفترة</summary>
        public decimal NetSales { get; set; }

        /// <summary>نسبة مساهمة العميل من إجمالي صافي المبيعات (٪)</summary>
        public decimal SharePercent { get; set; }
    }
}
