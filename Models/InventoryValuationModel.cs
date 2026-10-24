namespace CarPartManager.Models
{
    /// <summary>صف صنف في تقييم المخزون (الحالة الحالية)</summary>
    public class InventoryValuationRow
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;

        /// <summary>الكمية المتاحة للبيع</summary>
        public int Quantity { get; set; }

        /// <summary>كمية التالف (لا تُحتسب ضمن قيمة المخزون المتاح)</summary>
        public int DamagedQuantity { get; set; }

        public int MinimumStock { get; set; }

        /// <summary>سعر التكلفة الحالي للصنف</summary>
        public decimal CostPrice { get; set; }

        public decimal StockValue => Quantity * CostPrice;

        public DateTime? LastSaleDate { get; set; }

        public string LastSaleDateText => LastSaleDate.HasValue ? LastSaleDate.Value.ToString("yyyy-MM-dd") : "لا يوجد";
    }

    /// <summary>
    /// تقييم المخزون الحالي — لا يتأثر بالمرشّح التاريخي (يعرض لحظة التوليد).
    /// قيمة المخزون = الكمية المتاحة × سعر التكلفة الحالي.
    /// </summary>
    public class InventoryValuationModel
    {
        public DateTime GeneratedAt { get; set; } = DateTime.Now;

        /// <summary>عدد الأيام المستخدمة لتعريف الصنف الراكد (بلا مبيعات خلالها)</summary>
        public int StagnantDays { get; set; } = 60;

        public decimal TotalValue { get; set; }

        /// <summary>قيمة التالف للعرض فقط (غير محتسبة في قيمة المخزون المتاح)</summary>
        public decimal DamagedValue { get; set; }

        public int ProductCount { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public int StagnantCount { get; set; }

        public List<InventoryValuationRow> Rows { get; set; } = new();
        public List<InventoryValuationRow> LowStock { get; set; } = new();
        public List<InventoryValuationRow> OutOfStock { get; set; } = new();
        public List<InventoryValuationRow> Stagnant { get; set; } = new();
        public List<InventoryValuationRow> TopByValue { get; set; } = new();
    }
}
