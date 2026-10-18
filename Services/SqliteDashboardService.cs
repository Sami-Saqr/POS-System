using Microsoft.EntityFrameworkCore;
using CarPartManager.Data;
using CarPartManager.Models;

namespace CarPartManager.Services
{
    /// <summary>
    /// خدمة استخراج مؤشرات وإحصائيات لوحة التحكم من قاعدة بيانات SQLite المحلية
    /// </summary>
    public class SqliteDashboardService : IDashboardService
    {
        private CarPartsDbContext CreateContext() => new CarPartsDbContext();

        public async Task<DashboardSummaryDto> GetDashboardSummaryAsync()
        {
            try
            {
                using var context = CreateContext();

                var summary = new DashboardSummaryDto();

                // 1. استخراج إحصائيات الأصناف والمخزون
                var products = await context.Products.AsNoTracking().ToListAsync();
                summary.TotalProductsCount = products.Count;
                summary.TotalPiecesCount = products.Sum(p => p.Quantity);
                summary.TotalCategoriesCount = products
                    .Select(p => p.Category)
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Distinct()
                    .Count();
                summary.TotalInventoryCost = products.Sum(p => p.Quantity * p.PurchasePrice);

                // النواقص الحرجة: الأصناف التي وصلت إلى أو قلت عن حد الطلب الأدنى
                var lowStock = products
                    .Where(p => p.Quantity <= p.MinimumStock)
                    .OrderBy(p => p.Quantity)
                    .ThenBy(p => p.MinimumStock - p.Quantity)
                    .ToList();

                summary.LowStockProductsCount = lowStock.Count;

                summary.LowStockItems = lowStock.Select(p =>
                {
                    string urgencyText;
                    string urgencyColor;

                    if (p.Quantity <= 0)
                    {
                        urgencyText = "نفد تماماً";
                        urgencyColor = "#DC2626"; // أحمر غامق
                    }
                    else if (p.Quantity <= Math.Max(1, p.MinimumStock / 2))
                    {
                        urgencyText = "حرج جداً";
                        urgencyColor = "#DC2626";
                    }
                    else
                    {
                        urgencyText = "منخفض";
                        urgencyColor = "#D97706"; // كهرماني
                    }

                    return new LowStockItem
                    {
                        PartNumber = p.PartNumber,
                        PartName = p.Name,
                        VehicleModel = string.IsNullOrWhiteSpace(p.CompatibleVehicles) ? "عام" : p.CompatibleVehicles,
                        CurrentStock = p.Quantity,
                        MinimumRequired = p.MinimumStock,
                        Unit = "قطعة",
                        UnitPrice = p.SellingPrice > 0 ? p.SellingPrice : p.PurchasePrice,
                        UrgencyText = urgencyText,
                        UrgencyColor = urgencyColor
                    };
                }).ToList();

                // 2. إحصائيات فواتير المشتريات والمبيعات المعتمدة اليوم
                var today = DateTime.Today;

                var allPurchaseInvoices = await context.PurchaseInvoices.AsNoTracking().ToListAsync();
                summary.TotalPurchasesCount = allPurchaseInvoices.Count;

                var todayPostedPurchases = allPurchaseInvoices
                    .Where(p => p.Status == "معتمدة" &&
                        ((p.PostedDate.HasValue && p.PostedDate.Value.Date == today) ||
                         (!p.PostedDate.HasValue && p.InvoiceDate.Date == today)))
                    .ToList();

                summary.TodayPurchasesCount = todayPostedPurchases.Count;
                summary.TodayPurchasesTotal = todayPostedPurchases.Sum(p => p.TotalAmount);

                // إحصائيات فواتير المبيعات
                var allSalesInvoices = await context.SalesInvoices.AsNoTracking().ToListAsync();
                summary.TotalSalesCount = allSalesInvoices.Count;

                var todayPostedSales = allSalesInvoices
                    .Where(s => s.Status == "معتمدة" &&
                        ((s.PostedDate.HasValue && s.PostedDate.Value.Date == today) ||
                         (!s.PostedDate.HasValue && s.InvoiceDate.Date == today)))
                    .ToList();

                summary.TodaySalesCount = todayPostedSales.Count;
                summary.TodaySalesTotal = todayPostedSales.Sum(s => s.GrandTotal);

                // إحصائيات المرتجعات المعتمدة
                var allSalesReturns = await context.SalesReturns.AsNoTracking().ToListAsync();
                var allPurchaseReturns = await context.PurchaseReturns.AsNoTracking().ToListAsync();

                var todaySalesReturns = allSalesReturns.Where(r => r.Status == "معتمدة" && r.ReturnDate.Date == today).ToList();
                var todayPurchaseReturns = allPurchaseReturns.Where(r => r.Status == "معتمدة" && r.ReturnDate.Date == today).ToList();

                summary.TodaySalesReturnsCount = todaySalesReturns.Count;
                summary.TodaySalesReturnsTotal = todaySalesReturns.Sum(r => r.TotalAmount);
                summary.TodayPurchaseReturnsCount = todayPurchaseReturns.Count;
                summary.TodayPurchaseReturnsTotal = todayPurchaseReturns.Sum(r => r.TotalAmount);
                summary.TotalReturnsCount = allSalesReturns.Count + allPurchaseReturns.Count;

                // 3. آخر الفواتير والمرتجعات مرتبة من الأحدث للأقدم
                string FormatTime(DateTime dt)
                {
                    if (dt.Date == today) return $"اليوم {dt:hh:mm tt}";
                    if (dt.Date == today.AddDays(-1)) return $"أمس {dt:hh:mm tt}";
                    return dt.ToString("yyyy/MM/dd");
                }

                var purchaseItems = allPurchaseInvoices.Select(inv => new
                {
                    Date = inv.InvoiceDate,
                    inv.Id,
                    Code = inv.InvoiceNumber,
                    Party = string.IsNullOrWhiteSpace(inv.SupplierName) ? "مورد عام" : inv.SupplierName,
                    Type = inv.Status == "معتمدة" ? "فاتورة مشتريات (معتمدة)" : (inv.Status == "ملغاة" ? "فاتورة مشتريات (ملغاة)" : "مسودة مشتريات"),
                    Amount = inv.TotalAmount,
                    Payment = inv.Status == "ملغاة" ? "ملغاة" : inv.Status,
                    BadgeColor = inv.Status == "معتمدة" ? "#059669" : (inv.Status == "ملغاة" ? "#DC2626" : "#D97706")
                });

                var salesItems = allSalesInvoices.Select(inv => new
                {
                    Date = inv.InvoiceDate,
                    inv.Id,
                    Code = inv.InvoiceNumber,
                    Party = string.IsNullOrWhiteSpace(inv.CustomerName) ? "عميل نقدي" : inv.CustomerName,
                    Type = inv.Status == "معتمدة" ? "فاتورة مبيعات (معتمدة)" : (inv.Status == "ملغاة" ? "فاتورة مبيعات (ملغاة)" : "مسودة مبيعات"),
                    Amount = inv.GrandTotal,
                    Payment = inv.Status == "ملغاة" ? "ملغاة" : inv.PaymentMethod,
                    BadgeColor = inv.Status == "معتمدة" ? "#059669" : (inv.Status == "ملغاة" ? "#DC2626" : "#D97706")
                });

                var salesReturnItems = allSalesReturns.Select(ret => new
                {
                    Date = ret.ReturnDate,
                    ret.Id,
                    Code = ret.ReturnNumber,
                    Party = !string.IsNullOrWhiteSpace(ret.CustomerName) ? ret.CustomerName : "عميل نقدي",
                    Type = "مرتجع مبيعات (معتمد)",
                    Amount = ret.TotalAmount,
                    Payment = string.IsNullOrWhiteSpace(ret.Reason) ? "مرتجع" : ret.Reason,
                    BadgeColor = "#2563EB"
                });

                var purchaseReturnItems = allPurchaseReturns.Select(ret => new
                {
                    Date = ret.ReturnDate,
                    ret.Id,
                    Code = ret.ReturnNumber,
                    Party = !string.IsNullOrWhiteSpace(ret.SupplierName) ? ret.SupplierName : "مورد",
                    Type = "مرتجع مشتريات (معتمد)",
                    Amount = ret.TotalAmount,
                    Payment = string.IsNullOrWhiteSpace(ret.Reason) ? "مرتجع" : ret.Reason,
                    BadgeColor = "#7C3AED"
                });

                summary.RecentInvoices = purchaseItems.Concat(salesItems).Concat(salesReturnItems).Concat(purchaseReturnItems)
                    .OrderByDescending(x => x.Date)
                    .ThenByDescending(x => x.Id)
                    .Take(7)
                    .Select(inv => new RecentInvoiceSummary
                    {
                        InvoiceCode = inv.Code,
                        PartyName = inv.Party,
                        TypeName = inv.Type,
                        TimeFormatted = FormatTime(inv.Date),
                        TotalAmount = inv.Amount,
                        PaymentMethod = inv.Payment,
                        BadgeColor = inv.BadgeColor
                    }).ToList();

                // 4. إحصائيات الموردين وحركات المخزون
                summary.TotalSuppliersCount = await context.Suppliers.CountAsync();
                summary.TotalInventoryMovementsCount = await context.InventoryMovements.CountAsync();
                summary.LastUpdated = DateTime.Now;

                return summary;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر تحميل مؤشرات لوحة التحكم من قاعدة البيانات: {ex.Message}", ex);
            }
        }
    }
}
