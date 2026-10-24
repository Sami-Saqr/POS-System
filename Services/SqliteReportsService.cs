using CarPartManager.Data;
using CarPartManager.Models;
using Microsoft.EntityFrameworkCore;

namespace CarPartManager.Services
{
    /// <summary>
    /// تنفيذ تقارير التقارير المحاسبية على SQLite — للقراءة فقط (AsNoTracking) ولا يُعدّل أي بيانات.
    ///
    /// ملاحظتان مهمتان:
    /// 1) SQLite لا يدعم تجميع (SUM) على نوع decimal، لذا يتم التجميع في قاعدة البيانات للمفاتيح
    ///    والأعداد والكميات (int)، أما مبالغ الـ decimal فتُجمَع في الذاكرة بعد جلب أعمدة مسقطة فقط
    ///    (بدون كيانات متعقَّبة) — بنفس نمط SqliteDashboardService.
    /// 2) تُخزَّن تكلفة الوحدة في بند البيع وقت اعتماد الفاتورة، وتُحتسب تكلفة البضاعة المبيعة (COGS)
    ///    منها. أما البنود القديمة التي لا تحتوي على تكلفة مثبَّتة (NULL) فتُحتسب بسعر التكلفة الحالي
    ///    للصنف وتُعلَن "تقديرية" مع بيان عدد البنود/الفواتير التقديرية.
    /// </summary>
    public class SqliteReportsService : IReportsService
    {
        private const string PostedStatus = "معتمدة";
        private const string SellableCondition = "صالح للبيع";
        private const string SellableConditionAlt = "إرجاع إلى المخزون المتاح للبيع";
        private const string WalkInCustomer = "عميل نقدي";
        private const string UnknownProduct = "صنف غير معروف";
        private const string UnknownCategory = "تصنيف غير معروف";
        private const string UnknownSupplier = "مورد غير معروف";

        private static CarPartsDbContext CreateContext() => new CarPartsDbContext();

        // ── ملخص الأرباح ──────────────────────────────────────────────────────

        public async Task<ProfitSummaryModel> GetProfitSummaryAsync(DateTime fromInclusive, DateTime toExclusive, bool includePreviousComparison = true)
        {
            try
            {
                var summary = await BuildProfitSummaryAsync(fromInclusive, toExclusive);

                if (includePreviousComparison)
                {
                    var length = toExclusive - fromInclusive;
                    if (length > TimeSpan.Zero)
                    {
                        summary.Previous = await BuildProfitSummaryAsync(fromInclusive - length, fromInclusive);
                    }
                }

                return summary;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر إنشاء ملخص الأرباح: {ex.Message}", ex);
            }
        }

        private static async Task<ProfitSummaryModel> BuildProfitSummaryAsync(DateTime periodStart, DateTime periodEndExclusive)
        {
            using var context = CreateContext();

            var invoiceRows = await context.SalesInvoices.AsNoTracking()
                .Where(s => s.Status == PostedStatus && s.InvoiceDate >= periodStart && s.InvoiceDate < periodEndExclusive)
                .Select(s => new { s.SubTotal, s.Discount })
                .ToListAsync();

            var returnAmounts = await context.SalesReturns.AsNoTracking()
                .Where(r => r.Status == PostedStatus && r.ReturnDate >= periodStart && r.ReturnDate < periodEndExclusive)
                .Select(r => r.TotalAmount)
                .ToListAsync();

            // بنود المبيعات المعتمدة خلال الفترة مع تكلفة الوحدة المثبَّتة وقت البيع (إن وُجدت)
            var soldLines = await (from item in context.SalesInvoiceItems
                                   join invoice in context.SalesInvoices on item.SalesInvoiceId equals invoice.Id
                                   where invoice.Status == PostedStatus
                                         && invoice.InvoiceDate >= periodStart && invoice.InvoiceDate < periodEndExclusive
                                   select new
                                   {
                                       InvoiceId = invoice.Id,
                                       item.ProductId,
                                       item.Quantity,
                                       item.UnitCostAtSale
                                   })
                                  .AsNoTracking()
                                  .ToListAsync();

            // بنود المرتجعات الصالحة للبيع فقط (تُعاد للمخزون المتاح) — مطابق لمنطق خدمة المرتجعات
            var returnLines = await (from item in context.SalesReturnItems
                                     join ret in context.SalesReturns on item.SalesReturnId equals ret.Id
                                     where ret.Status == PostedStatus
                                           && ret.ReturnDate >= periodStart && ret.ReturnDate < periodEndExclusive
                                           && (item.ItemCondition == SellableCondition
                                               || item.ItemCondition == SellableConditionAlt)
                                     select new
                                     {
                                         InvoiceId = ret.SalesInvoiceId,
                                         item.ProductId,
                                         item.Quantity,
                                         item.SalesInvoiceItemId
                                     })
                                    .AsNoTracking()
                                    .ToListAsync();

            // تكلفة الوحدة المخزَّنة في بند البيع الأصلي لكل مرتجع (لعكس التكلفة التاريخية نفسها)
            var originalLineIds = returnLines.Select(r => r.SalesInvoiceItemId)
                                             .Where(id => id > 0)
                                             .Distinct()
                                             .ToList();
            var originalLineCosts = originalLineIds.Count == 0
                ? new Dictionary<int, decimal?>()
                : (await context.SalesInvoiceItems.AsNoTracking()
                        .Where(si => originalLineIds.Contains(si.Id))
                        .Select(si => new { si.Id, si.UnitCostAtSale })
                        .ToListAsync())
                   .ToDictionary(x => x.Id, x => x.UnitCostAtSale);

            var productIds = soldLines.Select(x => x.ProductId)
                                      .Concat(returnLines.Select(x => x.ProductId))
                                      .Distinct()
                                      .ToList();

            var costPrices = await GetCostPricesAsync(context, productIds);

            decimal CurrentCost(int productId) => costPrices.TryGetValue(productId, out var price) ? price : 0m;

            // التكلفة المثبَّتة وقت البيع، أو السعر الحالي كتقدير للبنود القديمة فقط
            decimal? StoredCostOf(int salesInvoiceItemId) =>
                salesInvoiceItemId > 0 && originalLineCosts.TryGetValue(salesInvoiceItemId, out var stored) ? stored : null;

            decimal LineCost(int productId, int quantity, decimal? unitCostAtSale) =>
                quantity * (unitCostAtSale ?? CurrentCost(productId));

            // إحصاء البنود التقديرية (تكلفتها غير مثبَّتة) لإظهار التنبيه بعددها فقط عند وجودها
            var estimatedInvoiceIds = new HashSet<int>();
            var estimatedLineCount = 0;

            foreach (var line in soldLines.Where(l => l.UnitCostAtSale == null))
            {
                estimatedLineCount++;
                estimatedInvoiceIds.Add(line.InvoiceId);
            }

            foreach (var line in returnLines.Where(l => StoredCostOf(l.SalesInvoiceItemId) == null))
            {
                estimatedLineCount++;
                estimatedInvoiceIds.Add(line.InvoiceId);
            }

            return new ProfitSummaryModel
            {
                PeriodStart = periodStart,
                PeriodEndExclusive = periodEndExclusive,
                GrossSales = invoiceRows.Sum(x => x.SubTotal),
                Discounts = invoiceRows.Sum(x => x.Discount),
                SalesReturns = returnAmounts.Sum(),
                CogsBeforeReturns = soldLines.Sum(l => LineCost(l.ProductId, l.Quantity, l.UnitCostAtSale)),
                ReturnedCogsReduction = returnLines.Sum(l => LineCost(l.ProductId, l.Quantity, StoredCostOf(l.SalesInvoiceItemId))),
                Expenses = await GetActiveExpensesTotalAsync(context, periodStart, periodEndExclusive),
                InvoiceCount = invoiceRows.Count,
                IsCogsEstimated = estimatedLineCount > 0,
                EstimatedLineCount = estimatedLineCount,
                EstimatedInvoiceCount = estimatedInvoiceIds.Count
            };
        }

        private static async Task<Dictionary<int, decimal>> GetCostPricesAsync(CarPartsDbContext context, List<int> productIds)
        {
            if (productIds.Count == 0) return new Dictionary<int, decimal>();

            var rows = await context.Products.AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .Select(p => new { p.Id, p.PurchasePrice })
                .ToListAsync();

            return rows.ToDictionary(p => p.Id, p => p.PurchasePrice);
        }

        private static async Task<decimal> GetActiveExpensesTotalAsync(CarPartsDbContext context, DateTime from, DateTime to)
        {
            var amounts = await context.Expenses.AsNoTracking()
                .Where(e => !e.IsCancelled && e.ExpenseDate >= from && e.ExpenseDate < to)
                .Select(e => e.Amount)
                .ToListAsync();

            return amounts.Sum();
        }

        // ── تقرير المبيعات اليومي ─────────────────────────────────────────────

        public async Task<List<SalesDailyReportRow>> GetSalesDailyReportAsync(DateTime fromInclusive, DateTime toExclusive)
        {
            try
            {
                using var context = CreateContext();

                var invoices = await context.SalesInvoices.AsNoTracking()
                    .Where(s => s.Status == PostedStatus && s.InvoiceDate >= fromInclusive && s.InvoiceDate < toExclusive)
                    .Select(s => new { s.InvoiceDate, s.SubTotal, s.Discount })
                    .ToListAsync();

                var returns = await context.SalesReturns.AsNoTracking()
                    .Where(r => r.Status == PostedStatus && r.ReturnDate >= fromInclusive && r.ReturnDate < toExclusive)
                    .Select(r => new { r.ReturnDate, r.TotalAmount })
                    .ToListAsync();

                var rows = new Dictionary<DateTime, SalesDailyReportRow>();

                SalesDailyReportRow RowOf(DateTime day)
                {
                    if (!rows.TryGetValue(day, out var row))
                    {
                        row = new SalesDailyReportRow { Day = day };
                        rows[day] = row;
                    }
                    return row;
                }

                foreach (var invoice in invoices)
                {
                    var row = RowOf(invoice.InvoiceDate.Date);
                    row.InvoiceCount++;
                    row.Total += invoice.SubTotal;
                    row.Discount += invoice.Discount;
                }

                foreach (var ret in returns)
                {
                    RowOf(ret.ReturnDate.Date).ReturnsTotal += ret.TotalAmount;
                }

                return rows.Values.OrderBy(r => r.Day).ToList();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر إنشاء تقرير المبيعات اليومي: {ex.Message}", ex);
            }
        }

        // ── أفضل الأصناف ──────────────────────────────────────────────────────

        public async Task<TopProductsModel> GetTopProductsAsync(DateTime fromInclusive, DateTime toExclusive, int take = 10)
        {
            try
            {
                if (take <= 0) take = 10;

                using var context = CreateContext();

                // الكميات مُجمَّعة في قاعدة البيانات
                var soldQuantities = await (from item in context.SalesInvoiceItems
                                            join invoice in context.SalesInvoices on item.SalesInvoiceId equals invoice.Id
                                            where invoice.Status == PostedStatus
                                                  && invoice.InvoiceDate >= fromInclusive && invoice.InvoiceDate < toExclusive
                                            group item by item.ProductId into grouped
                                            select new ProductQuantity
                                            {
                                                ProductId = grouped.Key,
                                                Quantity = grouped.Sum(x => x.Quantity)
                                            })
                                           .AsNoTracking()
                                           .ToListAsync();

                // القيمة تحتاج ضرب decimal (غير مدعوم في SQLite) — تُحسب في الذاكرة على أعمدة مسقطة
                var valueLines = await (from item in context.SalesInvoiceItems
                                        join invoice in context.SalesInvoices on item.SalesInvoiceId equals invoice.Id
                                        where invoice.Status == PostedStatus
                                              && invoice.InvoiceDate >= fromInclusive && invoice.InvoiceDate < toExclusive
                                        select new { item.ProductId, item.Quantity, item.UnitPrice })
                                       .AsNoTracking()
                                       .ToListAsync();

                var valueByProduct = valueLines
                    .GroupBy(x => x.ProductId)
                    .Select(g => new ProductQuantity
                    {
                        ProductId = g.Key,
                        Quantity = g.Sum(x => x.Quantity),
                        SalesValue = g.Sum(x => x.Quantity * x.UnitPrice)
                    })
                    .ToList();

                var quantityByProduct = soldQuantities.ToDictionary(x => x.ProductId, x => x.Quantity);

                var topByQuantity = soldQuantities
                    .OrderByDescending(x => x.Quantity)
                    .ThenBy(x => x.ProductId)
                    .Take(take)
                    .ToList();

                var topByValue = valueByProduct
                    .OrderByDescending(x => x.SalesValue)
                    .ThenBy(x => x.ProductId)
                    .Take(take)
                    .ToList();

                var productIds = topByQuantity.Select(x => x.ProductId)
                                              .Concat(topByValue.Select(x => x.ProductId))
                                              .Distinct()
                                              .ToList();

                var names = new Dictionary<int, string>();
                if (productIds.Count > 0)
                {
                    var products = await context.Products.AsNoTracking()
                        .Where(p => productIds.Contains(p.Id))
                        .Select(p => new { p.Id, p.Name })
                        .ToListAsync();

                    names = products.ToDictionary(p => p.Id, p => p.Name);
                }

                string NameOf(int productId) => names.TryGetValue(productId, out var name) ? name : UnknownProduct;

                return new TopProductsModel
                {
                    ByQuantity = topByQuantity.Select(x => new TopProductReportRow
                    {
                        ProductId = x.ProductId,
                        ProductName = NameOf(x.ProductId),
                        QuantitySold = x.Quantity,
                        SalesValue = valueByProduct.FirstOrDefault(v => v.ProductId == x.ProductId)?.SalesValue ?? 0m
                    }).ToList(),

                    ByValue = topByValue.Select(x => new TopProductReportRow
                    {
                        ProductId = x.ProductId,
                        ProductName = NameOf(x.ProductId),
                        QuantitySold = quantityByProduct.TryGetValue(x.ProductId, out var quantity) ? quantity : x.Quantity,
                        SalesValue = x.SalesValue
                    }).ToList()
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر إنشاء تقرير أفضل الأصناف: {ex.Message}", ex);
            }
        }

        // ── المبيعات حسب العميل ───────────────────────────────────────────────

        public async Task<List<CustomerSalesReportRow>> GetSalesByCustomerAsync(DateTime fromInclusive, DateTime toExclusive, int take = 20)
        {
            try
            {
                if (take <= 0) take = 20;

                using var context = CreateContext();

                var invoices = await context.SalesInvoices.AsNoTracking()
                    .Where(s => s.Status == PostedStatus && s.InvoiceDate >= fromInclusive && s.InvoiceDate < toExclusive)
                    .Select(s => new { s.CustomerName, s.GrandTotal })
                    .ToListAsync();

                var grandTotal = invoices.Sum(x => x.GrandTotal);

                var rows = invoices
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.CustomerName) ? WalkInCustomer : x.CustomerName!.Trim())
                    .Select(g => new CustomerSalesReportRow
                    {
                        CustomerName = g.Key,
                        InvoiceCount = g.Count(),
                        NetSales = g.Sum(x => x.GrandTotal)
                    })
                    .OrderByDescending(r => r.NetSales)
                    .ThenBy(r => r.CustomerName)
                    .Take(take)
                    .ToList();

                foreach (var row in rows)
                {
                    row.SharePercent = grandTotal == 0m
                        ? 0m
                        : Math.Round(row.NetSales / grandTotal * 100m, 1);
                }

                return rows;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر إنشاء تقرير المبيعات حسب العميل: {ex.Message}", ex);
            }
        }

        // ── تقرير المصروفات ───────────────────────────────────────────────────

        public async Task<ExpenseReportModel> GetExpensesReportAsync(DateTime fromInclusive, DateTime toExclusive, bool includePreviousComparison = true)
        {
            try
            {
                using var context = CreateContext();

                // المصروفات الملغاة مستبعدة تماماً من كل الأرقام
                var rows = await context.Expenses.AsNoTracking()
                    .Where(e => !e.IsCancelled && e.ExpenseDate >= fromInclusive && e.ExpenseDate < toExclusive)
                    .Select(e => new { e.ExpenseDate, e.CategoryId, e.Description, e.Amount, e.PaymentMethod, e.ReferenceNumber, e.CreatedBy })
                    .ToListAsync();

                var categoryNames = await context.ExpenseCategories.AsNoTracking()
                    .Select(c => new { c.Id, c.Name })
                    .ToDictionaryAsync(c => c.Id, c => c.Name);

                var total = rows.Sum(x => x.Amount);

                var categories = rows
                    .GroupBy(x => x.CategoryId)
                    .Select(g =>
                    {
                        var categoryTotal = g.Sum(x => x.Amount);
                        return new ExpenseCategoryReportRow
                        {
                            CategoryId = g.Key,
                            CategoryName = categoryNames.TryGetValue(g.Key, out var name) ? name : UnknownCategory,
                            Total = categoryTotal,
                            Count = g.Count(),
                            SharePercent = total == 0m ? 0m : Math.Round(categoryTotal / total * 100m, 1)
                        };
                    })
                    .OrderByDescending(c => c.Total)
                    .ToList();

                var details = rows
                    .OrderByDescending(x => x.ExpenseDate)
                    .Select(x => new ExpenseReportDetailRow
                    {
                        ExpenseDate = x.ExpenseDate,
                        CategoryName = categoryNames.TryGetValue(x.CategoryId, out var name) ? name : UnknownCategory,
                        Description = x.Description,
                        Amount = x.Amount,
                        PaymentMethod = x.PaymentMethod,
                        ReferenceNumber = x.ReferenceNumber,
                        CreatedBy = x.CreatedBy
                    })
                    .ToList();

                decimal? previousTotal = null;
                if (includePreviousComparison)
                {
                    var length = toExclusive - fromInclusive;
                    if (length > TimeSpan.Zero)
                    {
                        previousTotal = await GetActiveExpensesTotalAsync(context, fromInclusive - length, fromInclusive);
                    }
                }

                return new ExpenseReportModel
                {
                    PeriodStart = fromInclusive,
                    PeriodEndExclusive = toExclusive,
                    Total = total,
                    Count = rows.Count,
                    Categories = categories,
                    Details = details,
                    PreviousTotal = previousTotal
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر إنشاء تقرير المصروفات: {ex.Message}", ex);
            }
        }

        // ── تقرير المشتريات ───────────────────────────────────────────────────

        public async Task<PurchaseReportModel> GetPurchasesReportAsync(DateTime fromInclusive, DateTime toExclusive, bool includePreviousComparison = true, int topProducts = 10)
        {
            try
            {
                if (topProducts <= 0) topProducts = 10;

                using var context = CreateContext();

                // الفواتير المعتمدة فقط — المسودة والملغاة مستبعدة من كل الأرقام
                var invoices = await context.PurchaseInvoices.AsNoTracking()
                    .Where(p => p.Status == PostedStatus && p.InvoiceDate >= fromInclusive && p.InvoiceDate < toExclusive)
                    .Select(p => new { p.InvoiceDate, p.SupplierId, p.SupplierName, p.TotalAmount })
                    .ToListAsync();

                var total = invoices.Sum(x => x.TotalAmount);

                var daily = invoices
                    .GroupBy(x => x.InvoiceDate.Date)
                    .Select(g => new PurchaseDailyReportRow
                    {
                        Day = g.Key,
                        InvoiceCount = g.Count(),
                        Total = g.Sum(x => x.TotalAmount)
                    })
                    .OrderBy(r => r.Day)
                    .ToList();

                var bySupplier = invoices
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.SupplierName) ? UnknownSupplier : x.SupplierName!.Trim())
                    .Select(g =>
                    {
                        var supplierTotal = g.Sum(x => x.TotalAmount);
                        return new SupplierPurchaseReportRow
                        {
                            SupplierName = g.Key,
                            InvoiceCount = g.Count(),
                            Total = supplierTotal,
                            SharePercent = total == 0m ? 0m : Math.Round(supplierTotal / total * 100m, 1)
                        };
                    })
                    .OrderByDescending(r => r.Total)
                    .ThenBy(r => r.SupplierName)
                    .ToList();

                var lines = await (from item in context.PurchaseInvoiceItems
                                   join invoice in context.PurchaseInvoices on item.PurchaseInvoiceId equals invoice.Id
                                   where invoice.Status == PostedStatus
                                         && invoice.InvoiceDate >= fromInclusive && invoice.InvoiceDate < toExclusive
                                   select new { item.ProductId, item.ProductName, item.Quantity, item.UnitPrice })
                                  .AsNoTracking()
                                  .ToListAsync();

                var top = lines
                    .GroupBy(x => x.ProductId)
                    .Select(g => new TopProductReportRow
                    {
                        ProductId = g.Key,
                        ProductName = g.Select(x => x.ProductName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? UnknownProduct,
                        QuantitySold = g.Sum(x => x.Quantity),
                        SalesValue = g.Sum(x => x.Quantity * x.UnitPrice)
                    })
                    .OrderByDescending(r => r.SalesValue)
                    .ThenBy(r => r.ProductId)
                    .Take(topProducts)
                    .ToList();

                decimal? previousTotal = null;
                if (includePreviousComparison)
                {
                    var length = toExclusive - fromInclusive;
                    if (length > TimeSpan.Zero)
                    {
                        var previousAmounts = await context.PurchaseInvoices.AsNoTracking()
                            .Where(p => p.Status == PostedStatus && p.InvoiceDate >= fromInclusive - length && p.InvoiceDate < fromInclusive)
                            .Select(p => p.TotalAmount)
                            .ToListAsync();

                        previousTotal = previousAmounts.Sum();
                    }
                }

                return new PurchaseReportModel
                {
                    PeriodStart = fromInclusive,
                    PeriodEndExclusive = toExclusive,
                    Total = total,
                    InvoiceCount = invoices.Count,
                    Daily = daily,
                    BySupplier = bySupplier,
                    TopProducts = top,
                    HasSupplierData = invoices.Any(x => x.SupplierId.HasValue || !string.IsNullOrWhiteSpace(x.SupplierName)),
                    PreviousTotal = previousTotal
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر إنشاء تقرير المشتريات: {ex.Message}", ex);
            }
        }

        // ── تقييم المخزون (الحالة الحالية) ────────────────────────────────────

        public async Task<InventoryValuationModel> GetInventoryValuationAsync(int stagnantDays = 60, int topByValue = 10)
        {
            try
            {
                if (stagnantDays <= 0) stagnantDays = 60;
                if (topByValue <= 0) topByValue = 10;

                using var context = CreateContext();

                // أعمدة مسقطة فقط — بدون تحميل كيانات متعقَّبة
                var products = await context.Products.AsNoTracking()
                    .Select(p => new { p.Id, p.Name, p.Barcode, p.Category, p.Quantity, p.DamagedQuantity, p.MinimumStock, p.PurchasePrice })
                    .ToListAsync();

                // آخر تاريخ بيع معتمد لكل صنف — التجميع داخل قاعدة البيانات
                var lastSales = await context.SalesInvoiceItems.AsNoTracking()
                    .Join(context.SalesInvoices.AsNoTracking(),
                          item => item.SalesInvoiceId,
                          invoice => invoice.Id,
                          (item, invoice) => new { item.ProductId, invoice.InvoiceDate, invoice.Status })
                    .Where(x => x.Status == PostedStatus)
                    .GroupBy(x => x.ProductId)
                    .Select(g => new { ProductId = g.Key, LastSale = g.Max(x => x.InvoiceDate) })
                    .ToListAsync();

                var lastSaleById = lastSales.ToDictionary(x => x.ProductId, x => x.LastSale);
                var cutoff = DateTime.Today.AddDays(-stagnantDays);

                var rows = products
                    .Select(p =>
                    {
                        var hasLastSale = lastSaleById.TryGetValue(p.Id, out var lastSale);
                        return new InventoryValuationRow
                        {
                            ProductId = p.Id,
                            ProductName = p.Name,
                            Barcode = p.Barcode,
                            Category = p.Category,
                            Quantity = p.Quantity,
                            DamagedQuantity = p.DamagedQuantity,
                            MinimumStock = p.MinimumStock,
                            CostPrice = p.PurchasePrice,
                            LastSaleDate = hasLastSale ? lastSale : null
                        };
                    })
                    .OrderByDescending(r => r.StockValue)
                    .ThenBy(r => r.ProductId)
                    .ToList();

                var lowStock = rows.Where(r => r.Quantity > 0 && r.Quantity <= r.MinimumStock).ToList();
                var outOfStock = rows.Where(r => r.Quantity <= 0).ToList();

                // راكد = عليه رصيد متاح ولا مبيعات معتمدة خلال عدد الأيام المحدد
                var stagnant = rows
                    .Where(r => r.Quantity > 0 && (!r.LastSaleDate.HasValue || r.LastSaleDate.Value < cutoff))
                    .OrderBy(r => r.LastSaleDate ?? DateTime.MinValue)
                    .ThenBy(r => r.ProductId)
                    .ToList();

                return new InventoryValuationModel
                {
                    GeneratedAt = DateTime.Now,
                    StagnantDays = stagnantDays,
                    // قيمة المخزون = مجموع (الكمية المتاحة × سعر التكلفة الحالي) — تُحسب هنا وبالضبط كما في بطاقة الإجمالي
                    TotalValue = rows.Sum(r => r.StockValue),
                    DamagedValue = rows.Sum(r => r.DamagedQuantity * r.CostPrice),
                    ProductCount = rows.Count,
                    LowStockCount = lowStock.Count,
                    OutOfStockCount = outOfStock.Count,
                    StagnantCount = stagnant.Count,
                    Rows = rows,
                    LowStock = lowStock,
                    OutOfStock = outOfStock,
                    Stagnant = stagnant,
                    TopByValue = rows.Take(topByValue).ToList()
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر إنشاء تقييم المخزون: {ex.Message}", ex);
            }
        }

        // ── مديونيات العملاء (الحالة الحالية) ─────────────────────────────────

        public async Task<CustomerDebtReportModel> GetCustomerDebtsAsync()
        {
            try
            {
                // إعادة استخدام خدمة العملاء — نفس صيغة الرصيد المستخدمة في شاشة العملاء (بدون إعادة تنفيذها)
                var customerService = new SqliteCustomerService();
                var customers = (await customerService.GetAllCustomersAsync()).ToList();

                var debtors = customers.Where(c => c.CurrentBalance > 0m).ToList();
                var debtorIds = debtors.Select(c => c.Id).ToList();

                var lastPayments = new Dictionary<int, DateTime>();
                var oldestUnpaid = new Dictionary<int, DateTime>();

                if (debtorIds.Count > 0)
                {
                    using var context = CreateContext();

                    var paymentRows = await context.CustomerPayments.AsNoTracking()
                        .Where(p => !p.IsCancelled && debtorIds.Contains(p.CustomerId))
                        .GroupBy(p => p.CustomerId)
                        .Select(g => new { CustomerId = g.Key, LastPayment = g.Max(x => x.PaymentDate) })
                        .ToListAsync();

                    lastPayments = paymentRows.ToDictionary(x => x.CustomerId, x => x.LastPayment);

                    // تُجلب الأعمدة فقط ثم تُقارن المبالغ في الذاكرة، لأن SQLite يخزّن decimal كنص
                    // فمقارنة الأرقام داخل استعلام SQL قد تكون غير دقيقة.
                    var invoiceRows = await context.SalesInvoices.AsNoTracking()
                        .Where(i => i.Status == PostedStatus && i.CustomerId != null && debtorIds.Contains(i.CustomerId.Value))
                        .Select(i => new { CustomerId = i.CustomerId!.Value, i.InvoiceDate, i.GrandTotal, i.PaidAmount })
                        .ToListAsync();

                    oldestUnpaid = invoiceRows
                        .Where(i => i.GrandTotal > i.PaidAmount)
                        .GroupBy(i => i.CustomerId)
                        .ToDictionary(g => g.Key, g => g.Min(i => i.InvoiceDate));
                }

                var rows = debtors
                    .Select(c => new CustomerDebtRow
                    {
                        CustomerId = c.Id,
                        CustomerName = c.Name,
                        Phone = c.Phone,
                        Balance = c.CurrentBalance,
                        LastPaymentDate = lastPayments.TryGetValue(c.Id, out var lastPayment) ? lastPayment : null,
                        OldestUnpaidInvoiceDate = oldestUnpaid.TryGetValue(c.Id, out var oldest) ? oldest : null
                    })
                    .OrderByDescending(r => r.Balance)
                    .ThenBy(r => r.CustomerName)
                    .ToList();

                return new CustomerDebtReportModel
                {
                    TotalDebt = rows.Sum(r => r.Balance),
                    CustomerCount = rows.Count,
                    TotalCustomers = customers.Count,
                    Rows = rows
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر إنشاء تقرير مديونيات العملاء: {ex.Message}", ex);
            }
        }

        // ── ربحية الأصناف ─────────────────────────────────────────────────────

        public async Task<ItemProfitabilityModel> GetItemProfitabilityAsync(DateTime fromInclusive, DateTime toExclusive, int topCount = 10)
        {
            try
            {
                if (topCount <= 0) topCount = 10;

                using var context = CreateContext();

                // بنود البيع مع حقول الفاتورة اللازمة لتوزيع خصم الفاتورة على بنودها بنسبة قيمة البند
                var soldLines = await (from item in context.SalesInvoiceItems
                                       join invoice in context.SalesInvoices on item.SalesInvoiceId equals invoice.Id
                                       where invoice.Status == PostedStatus
                                             && invoice.InvoiceDate >= fromInclusive && invoice.InvoiceDate < toExclusive
                                       select new
                                       {
                                           invoice.Id,
                                           item.ProductId,
                                           item.ProductName,
                                           item.Quantity,
                                           item.UnitPrice,
                                           item.UnitCostAtSale,
                                           invoice.SubTotal,
                                           invoice.Discount
                                       })
                                      .AsNoTracking()
                                      .ToListAsync();

                // معامل الصافي لكل فاتورة = (الإجمالي قبل الخصم − خصم الفاتورة) ÷ الإجمالي قبل الخصم
                var netFactors = soldLines
                    .GroupBy(x => x.Id)
                    .ToDictionary(
                        g => g.Key,
                        g =>
                        {
                            var subTotal = g.First().SubTotal;
                            var discount = g.First().Discount;
                            return subTotal == 0m ? 1m : (subTotal - discount) / subTotal;
                        });

                var returnLines = await (from item in context.SalesReturnItems
                                         join ret in context.SalesReturns on item.SalesReturnId equals ret.Id
                                         where ret.Status == PostedStatus
                                               && ret.ReturnDate >= fromInclusive && ret.ReturnDate < toExclusive
                                         select new
                                         {
                                             item.ProductId,
                                             item.ProductName,
                                             item.Quantity,
                                             item.UnitPrice,
                                             item.ItemCondition,
                                             item.SalesInvoiceItemId
                                         })
                                        .AsNoTracking()
                                        .ToListAsync();

                // تكلفة الوحدة المخزَّنة في بند البيع الأصلي لكل مرتجع (لعكس التكلفة التاريخية نفسها)
                var originalLineIds = returnLines.Select(r => r.SalesInvoiceItemId)
                                                 .Where(id => id > 0)
                                                 .Distinct()
                                                 .ToList();
                var originalLineCosts = originalLineIds.Count == 0
                    ? new Dictionary<int, decimal?>()
                    : (await context.SalesInvoiceItems.AsNoTracking()
                            .Where(si => originalLineIds.Contains(si.Id))
                            .Select(si => new { si.Id, si.UnitCostAtSale })
                            .ToListAsync())
                       .ToDictionary(x => x.Id, x => x.UnitCostAtSale);

                var productIds = soldLines.Select(x => x.ProductId)
                                          .Concat(returnLines.Select(x => x.ProductId))
                                          .Distinct()
                                          .ToList();

                var costPrices = await GetCostPricesAsync(context, productIds);

                // التكلفة المثبَّتة وقت البيع، أو السعر الحالي كتقدير للبنود القديمة فقط
                decimal? StoredCostOf(int salesInvoiceItemId) =>
                    salesInvoiceItemId > 0 && originalLineCosts.TryGetValue(salesInvoiceItemId, out var stored) ? stored : null;

                decimal LineCost(int productId, int quantity, decimal? unitCostAtSale) =>
                    quantity * (unitCostAtSale ?? CostOf(costPrices, productId));

                // إحصاء البنود التقديرية لإظهار التنبيه بعددها فقط عند وجودها
                var estimatedInvoiceIds = new HashSet<int>();
                var estimatedLineCount = 0;

                foreach (var line in soldLines.Where(l => l.UnitCostAtSale == null))
                {
                    estimatedLineCount++;
                    estimatedInvoiceIds.Add(line.Id);
                }

                foreach (var line in returnLines.Where(l => StoredCostOf(l.SalesInvoiceItemId) == null))
                {
                    estimatedLineCount++;
                }

                var accumulator = new Dictionary<int, ItemProfitabilityAccumulator>();

                ItemProfitabilityAccumulator Accumulate(int productId, string? productName)
                {
                    if (!accumulator.TryGetValue(productId, out var acc))
                    {
                        acc = new ItemProfitabilityAccumulator { ProductId = productId };
                        accumulator[productId] = acc;
                    }

                    if (!string.IsNullOrWhiteSpace(productName)) acc.ProductName = productName.Trim();
                    return acc;
                }

                foreach (var line in soldLines)
                {
                    var acc = Accumulate(line.ProductId, line.ProductName);
                    var factor = netFactors.TryGetValue(line.Id, out var netFactor) ? netFactor : 1m;

                    acc.QuantitySold += line.Quantity;
                    acc.SalesValue += line.Quantity * line.UnitPrice * factor;
                    acc.Cost += LineCost(line.ProductId, line.Quantity, line.UnitCostAtSale);
                }

                foreach (var line in returnLines)
                {
                    var acc = Accumulate(line.ProductId, line.ProductName);

                    // كل المرتجعات المعتمدة تُخصم من صافي المبيعات (نفس منهج الجزء أ)،
                    // أما التكلفة فلا تُخفض إلا لبنود "صالح للبيع" التي تُعاد للمخزون.
                    acc.QuantitySold -= line.Quantity;
                    acc.SalesValue -= line.Quantity * line.UnitPrice;

                    if (line.ItemCondition == SellableCondition || line.ItemCondition == SellableConditionAlt)
                        acc.Cost -= LineCost(line.ProductId, line.Quantity, StoredCostOf(line.SalesInvoiceItemId));
                }

                var rows = accumulator.Values
                    .Where(a => a.QuantitySold != 0 || a.SalesValue != 0m || a.Cost != 0m)
                    .Select(a => new ItemProfitabilityRow
                    {
                        ProductId = a.ProductId,
                        ProductName = string.IsNullOrWhiteSpace(a.ProductName) ? UnknownProduct : a.ProductName,
                        QuantitySold = a.QuantitySold,
                        SalesValue = a.SalesValue,
                        Cost = a.Cost
                    })
                    .OrderByDescending(r => r.Profit)
                    .ThenBy(r => r.ProductId)
                    .ToList();

                return new ItemProfitabilityModel
                {
                    PeriodStart = fromInclusive,
                    PeriodEndExclusive = toExclusive,
                    TotalSalesValue = rows.Sum(r => r.SalesValue),
                    TotalCost = rows.Sum(r => r.Cost),
                    IsCogsEstimated = estimatedLineCount > 0,
                    EstimatedLineCount = estimatedLineCount,
                    EstimatedInvoiceCount = estimatedInvoiceIds.Count,
                    Rows = rows,
                    Best = rows.Take(topCount).ToList(),
                    Worst = rows.OrderBy(r => r.Profit).ThenBy(r => r.ProductId).Take(topCount).ToList()
                };
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر إنشاء تقرير ربحية الأصناف: {ex.Message}", ex);
            }
        }

        private static decimal CostOf(Dictionary<int, decimal> costPrices, int productId)
            => costPrices.TryGetValue(productId, out var price) ? price : 0m;

        /// <summary>مُجمِّع مساعد لربحية الأصناف (لا يُخزَّن ولا يُتعقَّب).</summary>
        private sealed class ItemProfitabilityAccumulator
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; } = string.Empty;
            public int QuantitySold { get; set; }
            public decimal SalesValue { get; set; }
            public decimal Cost { get; set; }
        }

        /// <summary>نوع مساعد للتجميع (كمية و/أو قيمة) بدون كيانات متعقَّبة.</summary>
        private sealed class ProductQuantity
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
            public decimal SalesValue { get; set; }
        }
    }
}
