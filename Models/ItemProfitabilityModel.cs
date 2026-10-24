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
    /// التكلفة تقديرية لأن بنود فواتير البيع لا تُخزّن تكلفة الوحدة وقت البيع (نفس منهج الجزء أ).
    /// </summary>
    public class ItemProfitabilityModel
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEndExclusive { get; set; }

        public decimal TotalSalesValue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalProfit => TotalSalesValue - TotalCost;
        public decimal? TotalMarginPercent => TotalSalesValue == 0m ? null : Math.Round(TotalProfit / TotalSalesValue * 100m, 1);

        /// <summary>هل التكلفة تقديرية؟ (true دائماً حالياً — لا تُخزَّن تكلفة الوحدة في بنود البيع)</summary>
        public bool IsCogsEstimated { get; set; } = true;

        public List<ItemProfitabilityRow> Rows { get; set; } = new();

        /// <summary>أفضل الأصناف ربحاً</summary>
        public List<ItemProfitabilityRow> Best { get; set; } = new();

        /// <summary>أقل/أسوأ الأصناف ربحاً</summary>
        public List<ItemProfitabilityRow> Worst { get; set; } = new();
    }
}
