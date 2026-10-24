namespace CarPartManager.Models
{
    /// <summary>صف صنف في تقرير ربحية الأصناف</summary>
    public class ItemProfitabilityRow
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;

        /// <summary>الكمية المبيعة (صافي بعد المرتجعات)</summary>
        public int QuantitySold { get; set; }

        /// <summary>صافي قيمة البيع بعد خصم حصة الصنف من خصم الفاتورة وبعد مرتجع البيع</summary>
        public decimal SalesValue { get; set; }

        /// <summary>تكلفة البضاعة المبيعة (صافي بعد مرتجعات البنود الصالحة للبيع)</summary>
        public decimal Cost { get; set; }

        public decimal Profit => SalesValue - Cost;

        public decimal? MarginPercent => SalesValue == 0m ? null : Math.Round(Profit / SalesValue * 100m, 1);

        public string MarginText => MarginPercent.HasValue ? $"{MarginPercent.Value:N1}٪" : "—";
    }

    /// <summary>
    /// ربحية الأصناف في الفترة المحددة.
    /// التكلفة تُحسب من تكلفة الوحدة المخزَّنة في بند البيع وقت البيع (نفس منهج الجزء أ)،
    /// وتكون تقديرية فقط للبنود القديمة التي لا تحتوي على تكلفة مثبَّتة.
    /// </summary>
    public class ItemProfitabilityModel
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEndExclusive { get; set; }

        public decimal TotalSalesValue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalProfit => TotalSalesValue - TotalCost;
        public decimal? TotalMarginPercent => TotalSalesValue == 0m ? null : Math.Round(TotalProfit / TotalSalesValue * 100m, 1);

        /// <summary>
        /// هل التكلفة تقديرية؟ صحيح فقط عند وجود بنود بيع/مرتجعات بلا تكلفة مثبَّتة (بنود قديمة)
        /// خلال الفترة المحددة، وإلا فالتكلفة محسوبة من تكلفة الوحدة المخزَّنة وقت البيع.
        /// </summary>
        public bool IsCogsEstimated { get; set; }

        /// <summary>عدد البنود التي حُسبت تكلفتها تقديرياً (تكلفتها غير مثبَّتة)</summary>
        public int EstimatedLineCount { get; set; }

        /// <summary>عدد الفواتير التي تحتوي على بنود تكلفتها تقديرية</summary>
        public int EstimatedInvoiceCount { get; set; }

        public List<ItemProfitabilityRow> Rows { get; set; } = new();

        /// <summary>أفضل الأصناف ربحاً</summary>
        public List<ItemProfitabilityRow> Best { get; set; } = new();

        /// <summary>أقل/أسوأ الأصناف ربحاً</summary>
        public List<ItemProfitabilityRow> Worst { get; set; } = new();
    }
}
