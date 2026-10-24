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

        /// <summary>
        /// تقرير المشتريات لفترة (الفواتير المعتمدة فقط): الإجمالي + الجدول اليومي + الإجمالي حسب المورد + أفضل الأصناف المشتراة.
        /// </summary>
        Task<PurchaseReportModel> GetPurchasesReportAsync(DateTime fromInclusive, DateTime toExclusive, bool includePreviousComparison = true, int topProducts = 10);

        /// <summary>
        /// تقييم المخزون الحالي (لا يتأثر بالمرشّح التاريخي): القيمة + النواقص + النفد + الأصناف الراكدة + الأعلى قيمة.
        /// </summary>
        Task<InventoryValuationModel> GetInventoryValuationAsync(int stagnantDays = 60, int topByValue = 10);

        /// <summary>مديونيات العملاء (الحالة الحالية) — يُعاد استخدام خدمة العملاء لحساب الرصيد.</summary>
        Task<CustomerDebtReportModel> GetCustomerDebtsAsync();

        /// <summary>ربحية الأصناف في الفترة (الكمية، قيمة البيع، التكلفة، الربح، نسبة الهامش).</summary>
        Task<ItemProfitabilityModel> GetItemProfitabilityAsync(DateTime fromInclusive, DateTime toExclusive, int topCount = 10);
    }
}
