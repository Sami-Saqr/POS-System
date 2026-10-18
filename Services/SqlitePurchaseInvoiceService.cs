using Microsoft.EntityFrameworkCore;
using CarPartManager.Data;
using CarPartManager.Models;

namespace CarPartManager.Services
{
    /// <summary>
    /// خدمة إدارة فواتير المشتريات المعتمدة على SQLite وتحديث المخزون داخل معاملات ذرية (Transactions).
    /// </summary>
    public class SqlitePurchaseInvoiceService : IPurchaseInvoiceService
    {
        private CarPartsDbContext CreateContext() => new CarPartsDbContext();

        /// <summary>
        /// توليد رقم فاتورة شراء تسلسلي فريد وغير مكرر
        /// </summary>
        public async Task<string> GenerateNextInvoiceNumberAsync()
        {
            using var context = CreateContext();
            var year = DateTime.Now.Year;
            var prefix = $"PUR-{year}-";

            var currentInvoices = await context.PurchaseInvoices
                                               .AsNoTracking()
                                               .Where(p => p.InvoiceNumber.StartsWith(prefix))
                                               .Select(p => p.InvoiceNumber)
                                               .ToListAsync();

            int maxIndex = 0;
            foreach (var inv in currentInvoices)
            {
                var suffix = inv.Replace(prefix, "");
                if (int.TryParse(suffix, out int parsedIndex) && parsedIndex > maxIndex)
                {
                    maxIndex = parsedIndex;
                }
            }

            return $"{prefix}{(maxIndex + 1):D4}";
        }

        public Task<string> GenerateInvoiceNumberAsync() => GenerateNextInvoiceNumberAsync();

        public async Task<IEnumerable<PurchaseInvoiceModel>> GetAllInvoicesAsync()
        {
            try
            {
                using var context = CreateContext();
                return await context.PurchaseInvoices
                                    .Include(p => p.Items)
                                    .AsNoTracking()
                                    .OrderByDescending(p => p.InvoiceDate)
                                    .ThenByDescending(p => p.Id)
                                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر تحميل فواتير المشتريات: {ex.Message}", ex);
            }
        }

        public async Task<PurchaseInvoiceModel?> GetInvoiceByIdAsync(int id)
        {
            try
            {
                using var context = CreateContext();
                return await context.PurchaseInvoices
                                    .Include(p => p.Items)
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(p => p.Id == id);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر تحميل الفاتورة رقم {id}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// حفظ الفاتورة كمسودة فقط دون أي تأثير على كميات المخزون
        /// </summary>
        public async Task<PurchaseInvoiceModel> SaveDraftInvoiceAsync(PurchaseInvoiceModel invoice)
        {
            if (invoice == null) throw new ArgumentNullException(nameof(invoice));
            if (invoice.Items == null || invoice.Items.Count == 0)
            {
                throw new InvalidOperationException("لا يمكن حفظ الفاتورة بدون إضافة بند واحد على الأقل.");
            }

            try
            {
                using var context = CreateContext();

                // التأكد من عدم تكرار رقم الفاتورة مع فاتورة أخرى
                var duplicate = await context.PurchaseInvoices
                                             .AsNoTracking()
                                             .FirstOrDefaultAsync(p => p.InvoiceNumber == invoice.InvoiceNumber && p.Id != invoice.Id);
                if (duplicate != null)
                {
                    throw new InvalidOperationException($"رقم الفاتورة '{invoice.InvoiceNumber}' مسجل مسبقاً في النظام. يرجى استخدام رقم آخر.");
                }

                invoice.Status = "مسودة";
                invoice.RecalculateTotal();

                if (invoice.Id == 0)
                {
                    // فاتورة جديدة تماماً
                    var newEntity = new PurchaseInvoiceModel
                    {
                        InvoiceNumber = invoice.InvoiceNumber.Trim(),
                        InvoiceDate = invoice.InvoiceDate,
                        SupplierId = invoice.SupplierId,
                        SupplierName = invoice.SupplierName?.Trim() ?? string.Empty,
                        Status = "مسودة",
                        TotalAmount = invoice.TotalAmount,
                        Notes = invoice.Notes?.Trim() ?? string.Empty,
                        Items = invoice.Items.Select(i => new PurchaseInvoiceItemModel
                        {
                            ProductId = i.ProductId,
                            PartNumber = i.PartNumber?.Trim() ?? string.Empty,
                            ProductName = i.ProductName?.Trim() ?? string.Empty,
                            Quantity = i.Quantity,
                            UnitPrice = i.UnitPrice
                        }).ToList()
                    };

                    await context.PurchaseInvoices.AddAsync(newEntity);
                    await context.SaveChangesAsync();
                    invoice.Id = newEntity.Id;
                    invoice.Items = newEntity.Items;
                }
                else
                {
                    // تعديل مسودة سابقة موجودة
                    var existing = await context.PurchaseInvoices
                                                .Include(p => p.Items)
                                                .FirstOrDefaultAsync(p => p.Id == invoice.Id);
                    if (existing == null)
                    {
                        throw new InvalidOperationException("لم يتم العثور على الفاتورة المطلوبة لتعديلها.");
                    }

                    if (existing.Status == "معتمدة")
                    {
                        throw new InvalidOperationException("لا يمكن تعديل الفاتورة كمسودة لأنها معتمدة ومرحلة إلى المخزون مسبقاً.");
                    }

                    existing.InvoiceNumber = invoice.InvoiceNumber.Trim();
                    existing.InvoiceDate = invoice.InvoiceDate;
                    existing.SupplierId = invoice.SupplierId;
                    existing.SupplierName = invoice.SupplierName?.Trim() ?? string.Empty;
                    existing.TotalAmount = invoice.TotalAmount;
                    existing.Notes = invoice.Notes?.Trim() ?? string.Empty;

                    // استبدال البنود
                    context.PurchaseInvoiceItems.RemoveRange(existing.Items);
                    existing.Items = invoice.Items.Select(i => new PurchaseInvoiceItemModel
                    {
                        PurchaseInvoiceId = existing.Id,
                        ProductId = i.ProductId,
                        PartNumber = i.PartNumber?.Trim() ?? string.Empty,
                        ProductName = i.ProductName?.Trim() ?? string.Empty,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice
                    }).ToList();

                    await context.SaveChangesAsync();
                    invoice.Items = existing.Items;
                }

                return invoice;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل حفظ مسودة الفاتورة: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// اعتماد فاتورة الشراء وتحديث أرصدة المخزون تلقائياً داخل معاملة ذرية (Transaction) واحدة
        /// </summary>
        public async Task<PurchaseInvoiceModel> PostInvoiceAsync(PurchaseInvoiceModel invoice)
        {
            if (invoice == null) throw new ArgumentNullException(nameof(invoice));
            if (invoice.Items == null || invoice.Items.Count == 0)
            {
                throw new InvalidOperationException("لا يمكن اعتماد فاتورة شراء خالية من الأصناف.");
            }

            // التحقق من صحة الكميات والأسعار
            foreach (var item in invoice.Items)
            {
                if (item.Quantity <= 0)
                {
                    throw new InvalidOperationException($"الكمية للصنف '{item.ProductName}' يجب أن تكون أكبر من الصفر.");
                }
                if (item.UnitPrice < 0)
                {
                    throw new InvalidOperationException($"سعر الشراء للصنف '{item.ProductName}' لا يمكن أن يكون سالباً.");
                }
            }

            using var context = CreateContext();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                PurchaseInvoiceModel targetInvoice;

                if (invoice.Id == 0)
                {
                    // التأكد من عدم تكرار رقم الفاتورة
                    var exists = await context.PurchaseInvoices.AnyAsync(p => p.InvoiceNumber == invoice.InvoiceNumber);
                    if (exists)
                    {
                        throw new InvalidOperationException($"رقم الفاتورة '{invoice.InvoiceNumber}' مسجل مسبقاً. تم إلغاء الاعتماد لمنع التكرار.");
                    }

                    invoice.RecalculateTotal();
                    targetInvoice = new PurchaseInvoiceModel
                    {
                        InvoiceNumber = invoice.InvoiceNumber.Trim(),
                        InvoiceDate = invoice.InvoiceDate,
                        SupplierId = invoice.SupplierId,
                        SupplierName = invoice.SupplierName?.Trim() ?? string.Empty,
                        Status = "معتمدة",
                        PostedDate = DateTime.Now,
                        TotalAmount = invoice.TotalAmount,
                        Notes = invoice.Notes?.Trim() ?? string.Empty,
                        Items = invoice.Items.Select(i => new PurchaseInvoiceItemModel
                        {
                            ProductId = i.ProductId,
                            PartNumber = i.PartNumber?.Trim() ?? string.Empty,
                            ProductName = i.ProductName?.Trim() ?? string.Empty,
                            Quantity = i.Quantity,
                            UnitPrice = i.UnitPrice
                        }).ToList()
                    };

                    await context.PurchaseInvoices.AddAsync(targetInvoice);
                    await context.SaveChangesAsync();
                    invoice.Id = targetInvoice.Id;
                }
                else
                {
                    targetInvoice = await context.PurchaseInvoices
                                                 .Include(p => p.Items)
                                                 .FirstOrDefaultAsync(p => p.Id == invoice.Id)
                                                 ?? throw new InvalidOperationException("الفاتورة المراد اعتمادها غير موجودة.");

                    // منع الاعتماد المتكرر لنفس الفاتورة إطلاقاً
                    if (targetInvoice.Status == "معتمدة")
                    {
                        throw new InvalidOperationException("هذه الفاتورة معتمدة مسبقاً بالفعل! لا يمكن إعادة اعتمادها لتجنب تكرار زيادة المخزون.");
                    }

                    targetInvoice.InvoiceNumber = invoice.InvoiceNumber.Trim();
                    targetInvoice.InvoiceDate = invoice.InvoiceDate;
                    targetInvoice.SupplierId = invoice.SupplierId;
                    targetInvoice.SupplierName = invoice.SupplierName?.Trim() ?? string.Empty;
                    targetInvoice.Status = "معتمدة";
                    targetInvoice.PostedDate = DateTime.Now;
                    targetInvoice.RecalculateTotal();
                    targetInvoice.Notes = invoice.Notes?.Trim() ?? string.Empty;

                    // تحديث البنود
                    context.PurchaseInvoiceItems.RemoveRange(targetInvoice.Items);
                    targetInvoice.Items = invoice.Items.Select(i => new PurchaseInvoiceItemModel
                    {
                        PurchaseInvoiceId = targetInvoice.Id,
                        ProductId = i.ProductId,
                        PartNumber = i.PartNumber?.Trim() ?? string.Empty,
                        ProductName = i.ProductName?.Trim() ?? string.Empty,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice
                    }).ToList();

                    await context.SaveChangesAsync();
                }

                // تحديث كميات المخزون وتسجيل حركات الإدخال
                foreach (var line in targetInvoice.Items)
                {
                    var product = await context.Products.FirstOrDefaultAsync(p => p.Id == line.ProductId);
                    if (product == null)
                    {
                        throw new InvalidOperationException($"الصنف ذو المعرف ({line.ProductId}) غير موجود في قاعدة بيانات الأصناف.");
                    }

                    int previousQty = product.Quantity;
                    product.Quantity += line.Quantity; // زيادة كمية المخزون بالوارد
                    product.PurchasePrice = line.UnitPrice; // تحديث سعر الشراء الأخير للصنف

                    // تسجيل حركة المخزون للرقابة والتدقيق
                    var movement = new InventoryMovementModel
                    {
                        ProductId = product.Id,
                        PartNumber = product.PartNumber,
                        ProductName = product.Name,
                        MovementType = "شراء وارد",
                        QuantityChange = line.Quantity,
                        PreviousQuantity = previousQty,
                        NewQuantity = product.Quantity,
                        MovementDate = DateTime.Now,
                        ReferenceId = targetInvoice.Id,
                        ReferenceNumber = targetInvoice.InvoiceNumber,
                        Notes = $"فاتورة مشتريات واردة رقم {targetInvoice.InvoiceNumber} من المورد {targetInvoice.SupplierName}"
                    };

                    await context.InventoryMovements.AddAsync(movement);
                }

                await context.SaveChangesAsync();

                // تثبيت المعاملة
                await transaction.CommitAsync();

                invoice.Status = "معتمدة";
                invoice.PostedDate = targetInvoice.PostedDate;
                invoice.Items = targetInvoice.Items;
                return invoice;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException($"فشل اعتماد الفاتورة وتحديث المخزون: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// اعتماد فاتورة الشراء بواسطة معرف الفاتورة (Invoice Id)
        /// </summary>
        public async Task<PurchaseInvoiceModel> PostInvoiceAsync(int invoiceId)
        {
            var invoice = await GetInvoiceByIdAsync(invoiceId);
            if (invoice == null)
            {
                throw new InvalidOperationException($"لم يتم العثور على الفاتورة رقم ({invoiceId}) لاعتمادها.");
            }

            return await PostInvoiceAsync(invoice);
        }

        public async Task<IEnumerable<PurchaseInvoiceModel>> SearchInvoicesAsync(string query)
        {
            try
            {
                using var context = CreateContext();
                if (string.IsNullOrWhiteSpace(query))
                {
                    return await GetAllInvoicesAsync();
                }

                var q = query.Trim().ToLower();
                return await context.PurchaseInvoices
                                    .Include(p => p.Items)
                                    .AsNoTracking()
                                    .Where(p =>
                                        p.InvoiceNumber.ToLower().Contains(q) ||
                                        (p.SupplierName != null && p.SupplierName.ToLower().Contains(q)) ||
                                        (p.Notes != null && p.Notes.ToLower().Contains(q))
                                    )
                                    .OrderByDescending(p => p.InvoiceDate)
                                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل البحث في فواتير المشتريات: {ex.Message}", ex);
            }
        }

        public async Task<bool> CancelInvoiceAsync(int invoiceId, string reason)
        {
            using var context = CreateContext();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                var targetInvoice = await context.PurchaseInvoices
                                                 .Include(p => p.Items)
                                                 .FirstOrDefaultAsync(p => p.Id == invoiceId)
                                                 ?? throw new InvalidOperationException($"لم يتم العثور على فاتورة المشتريات رقم ({invoiceId}).");

                if (targetInvoice.Status == "ملغاة")
                {
                    throw new InvalidOperationException($"فاتورة المشتريات رقم '{targetInvoice.InvoiceNumber}' ملغاة بالفعل مسبقاً! لا يمكن تكرار الإلغاء.");
                }

                // فحص هل توجد مرتجعات معتمدة مرتبطة بهذه الفاتورة
                bool hasReturns = await context.PurchaseReturns
                                               .AnyAsync(r => r.PurchaseInvoiceId == invoiceId && r.Status == "معتمدة");
                if (hasReturns)
                {
                    throw new InvalidOperationException(
                        $"لا يمكن إلغاء فاتورة المشتريات رقم '{targetInvoice.InvoiceNumber}' لوجود مرتجعات مشتريات معتمدة مسجلة عليها مسبقاً.\nلحماية سلامة المخزون والحسابات، لا يمكن إلغاء فاتورة تم إرجاع بنود منها.");
                }

                if (targetInvoice.Status == "معتمدة")
                {
                    // فحص كفاية رصيد المخزون لعكس الكميات الواردة لكل بند
                    foreach (var line in targetInvoice.Items)
                    {
                        var product = await context.Products.FirstOrDefaultAsync(p => p.Id == line.ProductId);
                        if (product == null)
                        {
                            throw new InvalidOperationException($"الصنف رقم ({line.ProductId}) غير موجود في قاعدة البيانات.");
                        }

                        if (product.Quantity < line.Quantity)
                        {
                            throw new InvalidOperationException(
                                $"لا يمكن إلغاء فاتورة الشراء؛ لأن رصيد الصنف '{product.Name}' في المستودع حالياً ({product.Quantity} قطعة) أقل من الكمية المشتراة المراد عكسها ({line.Quantity} قطعة) بسبب مبيعات تمت لاحقاً.\nيمنع النظام حدوث رصيد مخزني سالب.");
                        }

                        int prevQty = product.Quantity;
                        product.Quantity -= line.Quantity; // عكس الكمية المشتراة بخصمها من المستودع

                        var reversalMovement = new InventoryMovementModel
                        {
                            ProductId = product.Id,
                            PartNumber = product.PartNumber,
                            ProductName = product.Name,
                            MovementType = "إلغاء شراء صادر",
                            QuantityChange = -line.Quantity,
                            PreviousQuantity = prevQty,
                            NewQuantity = product.Quantity,
                            MovementDate = DateTime.Now,
                            ReferenceId = targetInvoice.Id,
                            ReferenceNumber = targetInvoice.InvoiceNumber,
                            Notes = $"حركة عكسية لإلغاء فاتورة مشتريات رقم {targetInvoice.InvoiceNumber}. السبب: {(string.IsNullOrWhiteSpace(reason) ? "إلغاء من قبل المستخدم" : reason.Trim())}"
                        };

                        await context.InventoryMovements.AddAsync(reversalMovement);
                    }
                }

                targetInvoice.Status = "ملغاة";
                targetInvoice.CancellationDate = DateTime.Now;
                targetInvoice.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "إلغاء من قبل المستخدم" : reason.Trim();

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                DataEvents.RaiseInventoryOrInvoicesChanged();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException($"فشل إلغاء فاتورة المشتريات: {ex.Message}", ex);
            }
        }

        public async Task<IEnumerable<InventoryMovementModel>> GetProductMovementsAsync(int productId)
        {
            try
            {
                using var context = CreateContext();
                return await context.InventoryMovements
                                    .AsNoTracking()
                                    .Where(m => m.ProductId == productId)
                                    .OrderByDescending(m => m.MovementDate)
                                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر قراءة سجل حركات المخزون: {ex.Message}", ex);
            }
        }
    }
}
