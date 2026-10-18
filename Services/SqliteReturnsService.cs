using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CarPartManager.Data;
using CarPartManager.Models;

namespace CarPartManager.Services
{
    /// <summary>
    /// خدمة معالجة مرتجعات المبيعات والمشتريات وتحديث المخزون بحركات ذرية آمنة
    /// </summary>
    public class SqliteReturnsService : IReturnsService
    {
        private CarPartsDbContext CreateContext() => new CarPartsDbContext();

        /// <summary>
        /// توليد رقم مرتجع مبيعات تسلسلي فريد (مثل SRT-2026-0001)
        /// </summary>
        public async Task<string> GenerateSalesReturnNumberAsync()
        {
            using var context = CreateContext();
            var year = DateTime.Now.Year;
            var prefix = $"SRT-{year}-";

            var currentReturns = await context.SalesReturns
                                              .AsNoTracking()
                                              .Where(r => r.ReturnNumber.StartsWith(prefix))
                                              .Select(r => r.ReturnNumber)
                                              .ToListAsync();

            int maxIndex = 0;
            foreach (var ret in currentReturns)
            {
                var suffix = ret.Replace(prefix, "");
                if (int.TryParse(suffix, out int parsedIndex) && parsedIndex > maxIndex)
                {
                    maxIndex = parsedIndex;
                }
            }

            return $"{prefix}{(maxIndex + 1):D4}";
        }

        /// <summary>
        /// توليد رقم مرتجع مشتريات تسلسلي فريد (مثل PRT-2026-0001)
        /// </summary>
        public async Task<string> GeneratePurchaseReturnNumberAsync()
        {
            using var context = CreateContext();
            var year = DateTime.Now.Year;
            var prefix = $"PRT-{year}-";

            var currentReturns = await context.PurchaseReturns
                                              .AsNoTracking()
                                              .Where(r => r.ReturnNumber.StartsWith(prefix))
                                              .Select(r => r.ReturnNumber)
                                              .ToListAsync();

            int maxIndex = 0;
            foreach (var ret in currentReturns)
            {
                var suffix = ret.Replace(prefix, "");
                if (int.TryParse(suffix, out int parsedIndex) && parsedIndex > maxIndex)
                {
                    maxIndex = parsedIndex;
                }
            }

            return $"{prefix}{(maxIndex + 1):D4}";
        }

        public async Task<List<SalesReturnModel>> GetAllSalesReturnsAsync()
        {
            using var context = CreateContext();
            return await context.SalesReturns
                                .Include(r => r.Items)
                                .OrderByDescending(r => r.ReturnDate)
                                .AsNoTracking()
                                .ToListAsync();
        }

        public async Task<List<PurchaseReturnModel>> GetAllPurchaseReturnsAsync()
        {
            using var context = CreateContext();
            return await context.PurchaseReturns
                                .Include(r => r.Items)
                                .OrderByDescending(r => r.ReturnDate)
                                .AsNoTracking()
                                .ToListAsync();
        }

        public async Task<Dictionary<int, int>> GetSalesInvoiceReturnedQuantitiesAsync(int salesInvoiceId)
        {
            using var context = CreateContext();
            var items = await (from ret in context.SalesReturns
                               where ret.SalesInvoiceId == salesInvoiceId && ret.Status == "معتمدة"
                               from item in ret.Items
                               group item by item.SalesInvoiceItemId into g
                               select new { SalesInvoiceItemId = g.Key, TotalReturned = g.Sum(x => x.Quantity) })
                              .ToListAsync();

            return items.ToDictionary(x => x.SalesInvoiceItemId, x => x.TotalReturned);
        }

        public async Task<Dictionary<int, int>> GetPurchaseInvoiceReturnedQuantitiesAsync(int purchaseInvoiceId)
        {
            using var context = CreateContext();
            var items = await (from ret in context.PurchaseReturns
                               where ret.PurchaseInvoiceId == purchaseInvoiceId && ret.Status == "معتمدة"
                               from item in ret.Items
                               group item by item.PurchaseInvoiceItemId into g
                               select new { PurchaseInvoiceItemId = g.Key, TotalReturned = g.Sum(x => x.Quantity) })
                              .ToListAsync();

            return items.ToDictionary(x => x.PurchaseInvoiceItemId, x => x.TotalReturned);
        }

        public async Task<List<SalesInvoiceModel>> GetEligibleSalesInvoicesAsync()
        {
            using var context = CreateContext();
            return await context.SalesInvoices
                                .Include(s => s.Items)
                                .Where(s => s.Status == "معتمدة")
                                .OrderByDescending(s => s.InvoiceDate)
                                .AsNoTracking()
                                .ToListAsync();
        }

        public async Task<List<PurchaseInvoiceModel>> GetEligiblePurchaseInvoicesAsync()
        {
            using var context = CreateContext();
            return await context.PurchaseInvoices
                                .Include(p => p.Items)
                                .Where(p => p.Status == "معتمدة")
                                .OrderByDescending(p => p.InvoiceDate)
                                .AsNoTracking()
                                .ToListAsync();
        }

        /// <summary>
        /// اعتماد ومعالجة مرتجع مبيعات مع تحديث آمن للمخزون داخل Transaction واحدة
        /// </summary>
        public async Task<SalesReturnModel> ProcessSalesReturnAsync(SalesReturnModel returnModel)
        {
            if (returnModel == null)
                throw new ArgumentNullException(nameof(returnModel));

            if (returnModel.Items == null || returnModel.Items.Count == 0)
                throw new InvalidOperationException("لا يمكن اعتماد مرتجع مبيعات فارغ بدون أي بنود.");

            using var context = CreateContext();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // التحقق من وجود الفاتورة الأصلية وأنها معتمدة
                var invoice = await context.SalesInvoices
                                           .Include(s => s.Items)
                                           .FirstOrDefaultAsync(s => s.Id == returnModel.SalesInvoiceId)
                                           ?? throw new InvalidOperationException($"فاتورة المبيعات الأصلية (المعرف: {returnModel.SalesInvoiceId}) غير موجودة.");

                if (invoice.Status != "معتمدة")
                {
                    throw new InvalidOperationException($"لا يمكن عمل مرتجع لفاتورة مبيعات بحالة '{invoice.Status}'. يجب أن تكون الفاتورة معتمدة.");
                }

                // فحص عدم تكرار رقم المرتجع
                if (string.IsNullOrWhiteSpace(returnModel.ReturnNumber))
                {
                    returnModel.ReturnNumber = await GenerateSalesReturnNumberAsync();
                }
                else
                {
                    bool exists = await context.SalesReturns.AnyAsync(r => r.ReturnNumber == returnModel.ReturnNumber);
                    if (exists)
                    {
                        throw new InvalidOperationException($"رقم المرتجع '{returnModel.ReturnNumber}' مسجل مسبقاً! يرجى توليد رقم جديد لمنع التكرار.");
                    }
                }

                returnModel.SalesInvoiceNumber = invoice.InvoiceNumber;
                returnModel.CustomerName = invoice.CustomerName ?? "عميل نقدي";

                // جلب المرتجعات السابقة المعتمدة على نفس الفاتورة
                var alreadyReturnedDict = await (from ret in context.SalesReturns
                                                 where ret.SalesInvoiceId == invoice.Id && ret.Status == "معتمدة"
                                                 from ri in ret.Items
                                                 group ri by ri.SalesInvoiceItemId into g
                                                 select new { ItemId = g.Key, Total = g.Sum(x => x.Quantity) })
                                                .ToDictionaryAsync(x => x.ItemId, x => x.Total);

                decimal calculatedTotal = 0;

                foreach (var item in returnModel.Items)
                {
                    if (item.Quantity <= 0)
                        throw new InvalidOperationException($"الكمية المرتجعة للصنف '{item.ProductName}' يجب أن تكون أكبر من الصفر.");

                    var originalLine = invoice.Items.FirstOrDefault(i => 
                        (item.SalesInvoiceItemId > 0 && i.Id == item.SalesInvoiceItemId) ||
                        (item.SalesInvoiceItemId <= 0 && i.ProductId == item.ProductId));

                    if (originalLine == null)
                        throw new InvalidOperationException($"البند ذو المعرف ({item.SalesInvoiceItemId}) أو الصنف ({item.ProductId}) غير موجود في فاتورة المبيعات الأصلية.");

                    // ضمان حفظ معرف بند الفاتورة الصحيح
                    item.SalesInvoiceItemId = originalLine.Id;

                    int prevReturned = alreadyReturnedDict.TryGetValue(originalLine.Id, out int pr) ? pr : 0;
                    int maxReturnable = originalLine.Quantity - prevReturned;

                    if (item.Quantity > maxReturnable)
                    {
                        throw new InvalidOperationException(
                            $"الكمية المراد إرجاعها من الصنف '{item.ProductName}' ({item.Quantity} قطعة) تتجاوز الكمية المتبقية القابلة للإرجاع ({maxReturnable} قطعة).\n" +
                            $"(الكمية المباعة: {originalLine.Quantity}، المرتجع مسبقاً: {prevReturned})");
                    }

                    // التحقق من الصنف في قاعدة البيانات
                    var product = await context.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId);
                    if (product == null)
                        throw new InvalidOperationException($"الصنف '{item.ProductName}' غير موجود في دليل الأصناف.");

                    // تثبيت سعر الوحدة من الفاتورة الأصلية لضمان الدقة المالية
                    item.UnitPrice = originalLine.UnitPrice;
                    item.PartNumber = originalLine.PartNumber;
                    item.ProductName = originalLine.ProductName;
                    calculatedTotal += item.Quantity * item.UnitPrice;

                    int previousQty = product.Quantity;
                    int previousDamagedQty = product.DamagedQuantity;

                    // معالجة مصير القطعة المرتجعة وفق اختيار المستخدم
                    bool isSellable = item.Condition == "إرجاع إلى المخزون المتاح للبيع" || item.Condition == "صالح للبيع";
                    bool isDamagedStock = item.Condition == "إرجاع إلى مخزون التالف" || item.Condition == "تالف";
                    bool isPermanentDisposal = item.Condition == "استبعاد نهائي من المخزون";

                    if (isSellable)
                    {
                        // 1. إرجاع إلى المخزون المتاح للبيع (يزيد الرصيد المتاح)
                        product.Quantity += item.Quantity;

                        var movement = new InventoryMovementModel
                        {
                            ProductId = product.Id,
                            PartNumber = product.PartNumber,
                            ProductName = product.Name,
                            MovementType = "مرتجع بيع وارد (صالح للبيع)",
                            QuantityChange = item.Quantity,
                            PreviousQuantity = previousQty,
                            NewQuantity = product.Quantity,
                            MovementDate = DateTime.Now,
                            ReferenceNumber = returnModel.ReturnNumber,
                            Notes = $"مرتجع مبيعات صالح للبيع - أضيف للمخزون المتاح - فاتورة {invoice.InvoiceNumber} - سبب: {returnModel.Reason}"
                        };
                        context.InventoryMovements.Add(movement);
                    }
                    else if (isDamagedStock)
                    {
                        // 2. إرجاع إلى مخزون التالف (لا يزيد الرصيد المتاح للبيع بل يضاف لرصيد التالف المحتفظ به)
                        product.DamagedQuantity += item.Quantity;

                        var movement = new InventoryMovementModel
                        {
                            ProductId = product.Id,
                            PartNumber = product.PartNumber,
                            ProductName = product.Name,
                            MovementType = "مرتجع بيع تالف (مخزون تالف)",
                            QuantityChange = 0,
                            PreviousQuantity = previousQty,
                            NewQuantity = product.Quantity,
                            MovementDate = DateTime.Now,
                            ReferenceNumber = returnModel.ReturnNumber,
                            Notes = $"مرتجع صنف تالف - أضيف لمخزون التالف (الكمية: {item.Quantity}، رصيد التالف الحالي: {product.DamagedQuantity}) - فاتورة {invoice.InvoiceNumber} - سبب: {returnModel.Reason}"
                        };
                        context.InventoryMovements.Add(movement);
                    }
                    else
                    {
                        // 3. استبعاد نهائي من المخزون (تخريد/إتلاف نهائي: لا يضاف للمخزون المتاح ولا لمخزون التالف)
                        var movement = new InventoryMovementModel
                        {
                            ProductId = product.Id,
                            PartNumber = product.PartNumber,
                            ProductName = product.Name,
                            MovementType = "مرتجع بيع تالف (استبعاد نهائي)",
                            QuantityChange = 0,
                            PreviousQuantity = previousQty,
                            NewQuantity = product.Quantity,
                            MovementDate = DateTime.Now,
                            ReferenceNumber = returnModel.ReturnNumber,
                            Notes = $"مرتجع صنف تالف مستبعد نهائياً من المخزون (الكمية: {item.Quantity}) - فاتورة {invoice.InvoiceNumber} - سبب: {returnModel.Reason}"
                        };
                        context.InventoryMovements.Add(movement);
                    }
                }

                returnModel.TotalAmount = calculatedTotal;
                returnModel.Status = "معتمدة";
                returnModel.ReturnDate = DateTime.Now;

                context.SalesReturns.Add(returnModel);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                DataEvents.RaiseInventoryOrInvoicesChanged();

                return returnModel;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// اعتماد ومعالجة مرتجع مشتريات مع التحقق من كفاية المخزون وتحديثه آمنًا داخل Transaction
        /// </summary>
        public async Task<PurchaseReturnModel> ProcessPurchaseReturnAsync(PurchaseReturnModel returnModel)
        {
            if (returnModel == null)
                throw new ArgumentNullException(nameof(returnModel));

            if (returnModel.Items == null || returnModel.Items.Count == 0)
                throw new InvalidOperationException("لا يمكن اعتماد مرتجع مشتريات فارغ بدون أي بنود.");

            using var context = CreateContext();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // التحقق من وجود الفاتورة الأصلية وأنها معتمدة
                var invoice = await context.PurchaseInvoices
                                           .Include(p => p.Items)
                                           .FirstOrDefaultAsync(p => p.Id == returnModel.PurchaseInvoiceId)
                                           ?? throw new InvalidOperationException($"فاتورة المشتريات الأصلية (المعرف: {returnModel.PurchaseInvoiceId}) غير موجودة.");

                if (invoice.Status != "معتمدة")
                {
                    throw new InvalidOperationException($"لا يمكن عمل مرتجع لفاتورة مشتريات بحالة '{invoice.Status}'. يجب أن تكون الفاتورة معتمدة.");
                }

                // فحص عدم تكرار رقم المرتجع
                if (string.IsNullOrWhiteSpace(returnModel.ReturnNumber))
                {
                    returnModel.ReturnNumber = await GeneratePurchaseReturnNumberAsync();
                }
                else
                {
                    bool exists = await context.PurchaseReturns.AnyAsync(r => r.ReturnNumber == returnModel.ReturnNumber);
                    if (exists)
                    {
                        throw new InvalidOperationException($"رقم المرتجع '{returnModel.ReturnNumber}' مسجل مسبقاً! يرجى توليد رقم جديد لمنع التكرار.");
                    }
                }

                returnModel.PurchaseInvoiceNumber = invoice.InvoiceNumber;
                returnModel.SupplierId = invoice.SupplierId;
                returnModel.SupplierName = invoice.SupplierName ?? string.Empty;

                // جلب المرتجعات السابقة المعتمدة على نفس فاتورة الشراء
                var alreadyReturnedDict = await (from ret in context.PurchaseReturns
                                                 where ret.PurchaseInvoiceId == invoice.Id && ret.Status == "معتمدة"
                                                 from ri in ret.Items
                                                 group ri by ri.PurchaseInvoiceItemId into g
                                                 select new { ItemId = g.Key, Total = g.Sum(x => x.Quantity) })
                                                .ToDictionaryAsync(x => x.ItemId, x => x.Total);

                decimal calculatedTotal = 0;

                foreach (var item in returnModel.Items)
                {
                    if (item.Quantity <= 0)
                        throw new InvalidOperationException($"الكمية المرتجعة للصنف '{item.ProductName}' يجب أن تكون أكبر من الصفر.");

                    var originalLine = invoice.Items.FirstOrDefault(i => 
                        (item.PurchaseInvoiceItemId > 0 && i.Id == item.PurchaseInvoiceItemId) ||
                        (item.PurchaseInvoiceItemId <= 0 && i.ProductId == item.ProductId));

                    if (originalLine == null)
                        throw new InvalidOperationException($"البند ذو المعرف ({item.PurchaseInvoiceItemId}) أو الصنف ({item.ProductId}) غير موجود في فاتورة المشتريات الأصلية.");

                    // ضمان حفظ معرف بند الفاتورة الصحيح
                    item.PurchaseInvoiceItemId = originalLine.Id;

                    int prevReturned = alreadyReturnedDict.TryGetValue(originalLine.Id, out int pr) ? pr : 0;
                    int maxReturnable = originalLine.Quantity - prevReturned;

                    if (item.Quantity > maxReturnable)
                    {
                        throw new InvalidOperationException(
                            $"الكمية المراد إرجاعها من الصنف '{item.ProductName}' ({item.Quantity} قطعة) تتجاوز الكمية المتبقية القابلة للإرجاع ({maxReturnable} قطعة).\n" +
                            $"(الكمية المشتراة: {originalLine.Quantity}، المرتجع مسبقاً: {prevReturned})");
                    }

                    // التحقق من الصنف في المخزون والتأكد من كفاية الرصيد
                    var product = await context.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId);
                    if (product == null)
                        throw new InvalidOperationException($"الصنف '{item.ProductName}' غير موجود في دليل الأصناف.");

                    if (product.Quantity < item.Quantity)
                    {
                        throw new InvalidOperationException(
                            $"الكمية المتوفرة حالياً في المخزون من الصنف '{product.Name}' هي ({product.Quantity} قطعة)، وهي لا تكفي لإرجاع ({item.Quantity} قطعة) للمورد!\n" +
                            $"تم رفض العملية منعاً لحدوث رصيد مخزون سالب نتيجة مبيعات أو حركات سابقة.");
                    }

                    // تثبيت سعر الشراء من الفاتورة الأصلية
                    item.UnitPrice = originalLine.UnitPrice;
                    item.PartNumber = originalLine.PartNumber;
                    item.ProductName = originalLine.ProductName;
                    calculatedTotal += item.Quantity * item.UnitPrice;

                    int previousQty = product.Quantity;

                    // خصم الكمية من المخزون
                    product.Quantity -= item.Quantity;

                    var movement = new InventoryMovementModel
                    {
                        ProductId = product.Id,
                        PartNumber = product.PartNumber,
                        ProductName = product.Name,
                        MovementType = "مرتجع شراء صادر",
                        QuantityChange = -item.Quantity,
                        PreviousQuantity = previousQty,
                        NewQuantity = product.Quantity,
                        MovementDate = DateTime.Now,
                        ReferenceNumber = returnModel.ReturnNumber,
                        Notes = $"مرتجع مشتريات للمورد فاتورة {invoice.InvoiceNumber} - سبب: {returnModel.Reason}"
                    };
                    context.InventoryMovements.Add(movement);
                }

                returnModel.TotalAmount = calculatedTotal;
                returnModel.Status = "معتمدة";
                returnModel.ReturnDate = DateTime.Now;

                context.PurchaseReturns.Add(returnModel);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                DataEvents.RaiseInventoryOrInvoicesChanged();

                return returnModel;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
