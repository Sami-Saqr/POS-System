namespace CarPartManager.Models
{
    /// <summary>
    /// ملخص الأرباح لفترة محددة، مع إمكانية إرفاق الفترة السابقة المماثلة للمقارنة.
    /// كل المبالغ محسوبة بنوع decimal، ومرتجع المبيعات المعتمد يُخصم من صافي المبيعات.
    /// </summary>
    public class ProfitSummaryModel
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEndExclusive { get; set; }

        /// <summary>إجمالي المبيعات قبل الخصم (مجموع SubTotal للفواتير المعتمدة)</summary>
        public decimal GrossSales { get; set; }

        /// <summary>إجمالي الخصومات على الفواتير المعتمدة</summary>
        public decimal Discounts { get; set; }

        /// <summary>قيمة مرتجعات المبيعات المعتمدة خلال الفترة</summary>
        public decimal SalesReturns { get; set; }

        /// <summary>صافي المبيعات = إجمالي المبيعات − الخصومات − المرتجعات</summary>
        public decimal NetSales => GrossSales - Discounts - SalesReturns;

        /// <summary>تكلفة البضاعة المبيعة قبل تعديل المرتجعات (تقديرية وفق سعر التكلفة الحالي للأصناف)</summary>
        public decimal CogsBeforeReturns { get; set; }

        /// <summary>تكلفة بنود المرتجعات الصالحة للبيع (تُعاد للمخزون) — تُخصم من تكلفة البضاعة المبيعة</summary>
        public decimal ReturnedCogsReduction { get; set; }

        /// <summary>تكلفة البضاعة المبيعة الصافية</summary>
        public decimal Cogs => CogsBeforeReturns - ReturnedCogsReduction;

        /// <summary>الربح الإجمالي = صافي المبيعات − تكلفة البضاعة المبيعة</summary>
        public decimal GrossProfit => NetSales - Cogs;

        /// <summary>المصروفات النشطة (غير الملغاة) خلال الفترة</summary>
        public decimal Expenses { get; set; }

        /// <summary>صافي الربح = الربح الإجمالي − المصروفات</summary>
        public decimal NetProfit => GrossProfit - Expenses;

        /// <summary>عدد فواتير المبيعات المعتمدة خلال الفترة</summary>
        public int InvoiceCount { get; set; }

        /// <summary>هل التكلفة تقديرية؟ (نعم دائماً حالياً لعدم تخزين تكلفة الوحدة في بنود البيع)</summary>
        public bool IsCogsEstimated { get; set; } = true;

        /// <summary>الفترة السابقة المماثلة (بنفس الطول) — null إذا لم تُطلب المقارنة</summary>
        public ProfitSummaryModel? Previous { get; set; }

        // ── مقارنات مع الفترة السابقة ─────────────────────────────────────────
        public decimal NetSalesChange => NetSales - (Previous?.NetSales ?? 0m);
        public decimal GrossProfitChange => GrossProfit - (Previous?.GrossProfit ?? 0m);
        public decimal ExpensesChange => Expenses - (Previous?.Expenses ?? 0m);
        public decimal NetProfitChange => NetProfit - (Previous?.NetProfit ?? 0m);

        public decimal? NetSalesChangePercent => PercentChange(NetSales, Previous?.NetSales);
        public decimal? GrossProfitChangePercent => PercentChange(GrossProfit, Previous?.GrossProfit);
        public decimal? ExpensesChangePercent => PercentChange(Expenses, Previous?.Expenses);
        public decimal? NetProfitChangePercent => PercentChange(NetProfit, Previous?.NetProfit);

        /// <summary>نسبة التغير المئوية — null عندما تكون فترة الأساس صفراً (لا مقارنة ممكنة)</summary>
        public static decimal? PercentChange(decimal current, decimal? previous)
        {
            if (previous is null || previous.Value == 0m) return null;
            return (current - previous.Value) / Math.Abs(previous.Value) * 100m;
        }
    }
}
