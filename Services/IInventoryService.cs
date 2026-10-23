using CarPartManager.Models;

namespace CarPartManager.Services
{
    /// <summary>
    /// واجهة خدمة إدارة المخزون وحركات الصنف
    /// </summary>
    public interface IInventoryService
    {
        // Stock Overview
        Task<InventorySummary> GetInventorySummaryAsync();
        Task<IEnumerable<ProductStockInfo>> GetProductStockInfoAsync(string? searchQuery = null, StockFilter filter = StockFilter.All);

        // Stock Movements
        Task<IEnumerable<InventoryMovementModel>> GetMovementsAsync(DateTime? fromDate = null, DateTime? toDate = null, int? productId = null, string? movementType = null);
        Task<IEnumerable<string>> GetMovementTypesAsync();

        // Manual Adjustment
        Task<InventoryMovementModel> RecordManualAdjustmentAsync(int productId, int quantityChange, string reason, string? note = null);

        // Consistency Check
        Task<IEnumerable<StockMismatch>> CheckConsistencyAsync();
    }

    /// <summary>
    /// ملخص المخزون
    /// </summary>
    public class InventorySummary
    {
        public int ProductCount { get; set; }
        public decimal TotalStockValue { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
    }

    /// <summary>
    /// معلومات مخزون الصنف للعرض
    /// </summary>
    public class ProductStockInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int MinimumStock { get; set; }
        public decimal CostPrice { get; set; }
        public decimal StockValue => Quantity * CostPrice;
        public string Status => Quantity <= 0 ? "نفد من المخزن" : Quantity <= MinimumStock ? "منخفض (حرج)" : "متوفر";
        public string StatusColor => Quantity <= 0 ? "#DC2626" : Quantity <= MinimumStock ? "#D97706" : "#059669";
    }

    /// <summary>
    /// فلاتر حالة المخزون
    /// </summary>
    public enum StockFilter
    {
        All,
        LowStock,
        OutOfStock
    }

    /// <summary>
    /// عدم تطابق المخزون
    /// </summary>
    public class StockMismatch
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string PartNumber { get; set; } = string.Empty;
        public int ExpectedQuantity { get; set; }
        public int ActualQuantity { get; set; }
        public int Difference => ActualQuantity - ExpectedQuantity;
    }
}
