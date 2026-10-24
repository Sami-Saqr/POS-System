namespace CarPartManager.Models
{
    /// <summary>
    /// ملخص مصروفات الشاشة (Expense Summary Model) — يُحسب دائماً من المصروفات النشطة فقط،
    /// بمعنى أن المصروفات الملغاة لا تدخل في أي إجمالي.
    /// </summary>
    public class ExpenseSummaryModel
    {
        /// <summary>
        /// إجمالي مصروفات اليوم
        /// </summary>
        public decimal TodayTotal { get; set; }

        /// <summary>
        /// إجمالي مصروفات الشهر الحالي
        /// </summary>
        public decimal CurrentMonthTotal { get; set; }

        /// <summary>
        /// إجمالي مصروفات الشهر السابق
        /// </summary>
        public decimal PreviousMonthTotal { get; set; }

        /// <summary>
        /// أعلى تصنيف من حيث قيمة المصروفات في الشهر الحالي
        /// </summary>
        public string TopCategoryName { get; set; } = "—";

        /// <summary>
        /// قيمة مصروفات أعلى تصنيف في الشهر الحالي
        /// </summary>
        public decimal TopCategoryTotal { get; set; }

        /// <summary>
        /// عدد المصروفات النشطة في الشهر الحالي
        /// </summary>
        public int CurrentMonthCount { get; set; }
    }
}
