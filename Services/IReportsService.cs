using CarPartManager.Models;

namespace CarPartManager.Services
{
    /// <summary>
    /// خدمة التقارير المحاسبية — قراءة فقط. لا تُعدّل أي بيانات إطلاقاً.
    /// كل الحدود الزمنية تُمرَّر كـ (بداية شاملة) و (نهاية حصرية) لتجنّب قطع أي يوم.
    /// </summary>
    public interface IReportsService
    {
        /// <summary>ملخص الأرباح لفترة (مع مقارنة بالفترة السابقة المماثلة إن طُلب ذلك)</summary>
        Task<ProfitSummaryModel> GetProfitSummaryAsync(DateTime fromInclusive, DateTime toExclusive, bool includePreviousComparison = true);

        /// <summary>تقرير المبيعات اليومي (فواتير + خصومات + مرتجعات + صافي) مرتب تصاعدياً بالتاريخ</summary>
        Task<List<SalesDailyReportRow>> GetSalesDailyReportAsync(DateTime fromInclusive, DateTime toExclusive);

        /// <summary>أفضل الأصناف مبيعاً بالكمية وبالقيمة</summary>
        Task<TopProductsModel> GetTopProductsAsync(DateTime fromInclusive, DateTime toExclusive, int take = 10);

        /// <summary>المبيعات حسب العميل (العملاء النقديون تحت "عميل نقدي")</summary>
        Task<List<CustomerSalesReportRow>> GetSalesByCustomerAsync(DateTime fromInclusive, DateTime toExclusive, int take = 20);

        /// <summary>تقرير المصروفات (الإجمالي + التفصيل حسب التصنيف + القائمة التفصيلية + مقارنة بالفترة السابقة)</summary>
        Task<ExpenseReportModel> GetExpensesReportAsync(DateTime fromInclusive, DateTime toExclusive, bool includePreviousComparison = true);
    }
}
