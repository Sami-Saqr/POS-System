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
    /// 2) لا تُخزَّن تكلفة الوحدة في بنود فواتير البيع، لذا تُحتسب تكلفة البضاعة المبيعة (COGS)
    ///    بسعر التكلفة الحالي للصنف وتكون "تقديرية" (IsCogsEstimated = true).
    /// </summary>
    public class SqliteReportsService : IReportsService
    {
        private const string PostedStatus = "معتمدة";
        private const string SellableCondition = "صالح للبيع";
        private const string SellableConditionAlt = "إرجاع إلى المخزون المتاح للبيع";
        private const string WalkInCustomer = "عميل نقدي";
        private const string UnknownProduct = "صنف غير معروف";
        private const string UnknownCategory = "تصنيف غير معروف";

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

            // كميات البيع مُجمَّعة في قاعدة البيانات (int)
            var soldQuantities = await (from item in context.SalesInvoiceItems
                                        join invoice in context.SalesInvoices on item.SalesInvoiceId equals invoice.Id
                                        where invoice.Status == PostedStatus
                                              && invoice.InvoiceDate >= periodStart && invoice.InvoiceDate < periodEndExclusive
                                        group item by item.ProductId into grouped
                                        select new ProductQuantity
                                        {
                                            ProductId = grouped.Key,
                                            Quantity = grouped.Sum(x => x.Quantity)
                                        })
                                       .AsNoTracking()
                                       .ToListAsync();

            // كميات المرتجعات الصالحة للبيع فقط (تُعاد للمخزون المتاح) — مطابق لمنطق خدمة المرتجعات
            var returnedQuantities = await (from item in context.SalesReturnItems
                                            join ret in context.SalesReturns on item.SalesReturnId equals ret.Id
                                            where ret.Status == PostedStatus
                                                  && ret.ReturnDate >= periodStart && ret.ReturnDate < periodEndExclusive
                                                  && (item.ItemCondition == SellableCondition
                                                      || item.ItemCondition == SellableConditionAlt)
                                            group item by item.ProductId into grouped
                                            select new ProductQuantity
                                            {
                                                ProductId = grouped.Key,
                                                Quantity = grouped.Sum(x => x.Quantity)
                                            })
                                           .AsNoTracking()
                                           .ToListAsync();

            var productIds = soldQuantities.Select(x => x.ProductId)
                                           .Concat(returnedQuantities.Select(x => x.ProductId))
                                           .Distinct()
                                           .ToList();

            var costPrices = await GetCostPricesAsync(context, productIds);

            decimal ProductCost(IEnumerable<ProductQuantity> rows) =>
                rows.Sum(r => r.Quantity * (costPrices.TryGetValue(r.ProductId, out var price) ? price : 0m));

            return new ProfitSummaryModel
            {
                PeriodStart = periodStart,
                PeriodEndExclusive = periodEndExclusive,
                GrossSales = invoiceRows.Sum(x => x.SubTotal),
                Discounts = invoiceRows.Sum(x => x.Discount),
                SalesReturns = returnAmounts.Sum(),
                CogsBeforeReturns = ProductCost(soldQuantities),
                ReturnedCogsReduction = ProductCost(returnedQuantities),
                Expenses = await GetActiveExpensesTotalAsync(context, periodStart, periodEndExclusive),
                InvoiceCount = invoiceRows.Count,
                IsCogsEstimated = true
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

        /// <summary>نوع مساعد للتجميع (كمية و/أو قيمة) بدون كيانات متعقَّبة.</summary>
        private sealed class ProductQuantity
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
            public decimal SalesValue { get; set; }
        }
    }
}
