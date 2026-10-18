using CarPartManager.Models;

namespace CarPartManager.Services
{
    /// <summary>
    /// كائن نقل بيانات مؤشرات وإحصائيات لوحة التحكم المحسوبة من SQLite
    /// </summary>
    public class DashboardSummaryDto
    {
        public decimal TodayPurchasesTotal { get; set; }
        public int TodayPurchasesCount { get; set; }
        public int TotalPurchasesCount { get; set; }
        public decimal TodaySalesTotal { get; set; }
        public int TodaySalesCount { get; set; }
        public int TotalSalesCount { get; set; }
        public int TotalProductsCount { get; set; }
        public int TotalCategoriesCount { get; set; }
        public int TotalPiecesCount { get; set; }
        public int LowStockProductsCount { get; set; }
        public decimal TotalInventoryCost { get; set; }
        public int TotalSuppliersCount { get; set; }
        public int TotalInventoryMovementsCount { get; set; }
        public decimal TodaySalesReturnsTotal { get; set; }
        public int TodaySalesReturnsCount { get; set; }
        public decimal TodayPurchaseReturnsTotal { get; set; }
        public int TodayPurchaseReturnsCount { get; set; }
        public int TotalReturnsCount { get; set; }
        public List<LowStockItem> LowStockItems { get; set; } = new();
        public List<RecentInvoiceSummary> RecentInvoices { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// واجهة خدمة لوحة التحكم واستخراج المؤشرات الحية من قاعدة البيانات
    /// </summary>
    public interface IDashboardService
    {
        Task<DashboardSummaryDto> GetDashboardSummaryAsync();
    }
}
