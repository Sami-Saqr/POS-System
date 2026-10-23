using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using CarPartManager.Data;
using CarPartManager.Models;

namespace CarPartManager.Services
{
    /// <summary>
    /// خدمة إدارة فواتير المبيعات ونقاط البيع المعتمدة على SQLite وتحديث المخزون عبر المعاملات الذرية
    /// </summary>
    public class SqliteSalesInvoiceService : ISalesInvoiceService
    {
        private CarPartsDbContext CreateContext() => new CarPartsDbContext();

        /// <summary>
        /// توليد رقم فاتورة بيع تسلسلي فريد وغير مكرر (مثل INV-2026-0001)
        /// </summary>
        public async Task<string> GenerateNextInvoiceNumberAsync()
        {
            using var context = CreateContext();
            var year = DateTime.Now.Year;
            var prefix = $"INV-{year}-";

            var currentInvoices = await context.SalesInvoices
                                               .AsNoTracking()
                                               .Where(s => s.InvoiceNumber.StartsWith(prefix))
                                               .Select(s => s.InvoiceNumber)
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

        public async Task<IEnumerable<SalesInvoiceModel>> GetAllInvoicesAsync()
        {
            try
            {
                using var context = CreateContext();
                return await context.SalesInvoices
                                    .Include(s => s.Items)
                                    .AsNoTracking()
                                    .OrderByDescending(s => s.InvoiceDate)
                                    .ThenByDescending(s => s.Id)
                                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر تحميل فواتير المبيعات: {ex.Message}", ex);
            }
        }

        public async Task<SalesInvoiceModel?> GetInvoiceByIdAsync(int id)
        {
            try
            {
                using var context = CreateContext();
                return await context.SalesInvoices
                                    .Include(s => s.Items)
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(s => s.Id == id);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر تحميل فاتورة المبيعات رقم {id}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// حفظ الفاتورة كمسودة دون التأثير إطلاقاً على المخزون
        /// </summary>
        public async Task<SalesInvoiceModel> SaveDraftInvoiceAsync(SalesInvoiceModel invoice)
        {
            if (invoice == null) throw new ArgumentNullException(nameof(invoice));
            if (invoice.Items == null || invoice.Items.Count == 0)
            {
                throw new InvalidOperationException("لا يمكن حفظ فاتورة مبيعات خالية من الأصناف.");
            }

            try
            {
                using var context = CreateContext();

                // التحقق من عدم تكرار رقم الفاتورة
                var duplicate = await context.SalesInvoices
                                             .AsNoTracking()
                                             .FirstOrDefaultAsync(s => s.InvoiceNumber == invoice.InvoiceNumber && s.Id != invoice.Id);
                if (duplicate != null)
                {
                    throw new InvalidOperationException($"رقم الفاتورة '{invoice.InvoiceNumber}' مسجل مسبقاً في النظام.");
                }

                invoice.Status = "مسودة";
                invoice.RecalculateTotals();

                if (invoice.Id == 0)
                {
                    var newEntity = new SalesInvoiceModel
                    {
                        InvoiceNumber = invoice.InvoiceNumber.Trim(),
                        InvoiceDate = invoice.InvoiceDate,
                        CustomerId = invoice.CustomerId,
                        CustomerName = string.IsNullOrWhiteSpace(invoice.CustomerName) ? "عميل نقدي" : invoice.CustomerName.Trim(),
                        CustomerPhone = invoice.CustomerPhone?.Trim() ?? string.Empty,
                        CustomerAddress = invoice.CustomerAddress?.Trim() ?? string.Empty,
                        Status = "مسودة",
                        SubTotal = invoice.SubTotal,
                        Discount = invoice.Discount,
                        GrandTotal = invoice.GrandTotal,
                        PaymentMethod = invoice.PaymentMethod,
                        PaidAmount = invoice.PaidAmount,
                        Notes = invoice.Notes?.Trim() ?? string.Empty,
                        Items = invoice.Items.Select(i => new SalesInvoiceItemModel
                        {
                            ProductId = i.ProductId,
                            PartNumber = i.PartNumber?.Trim() ?? string.Empty,
                            Barcode = i.Barcode?.Trim() ?? string.Empty,
                            ProductName = i.ProductName?.Trim() ?? string.Empty,
                            Quantity = i.Quantity,
                            UnitPrice = i.UnitPrice
                        }).ToList()
                    };

                    await context.SalesInvoices.AddAsync(newEntity);
                    await context.SaveChangesAsync();
                    invoice.Id = newEntity.Id;
                    invoice.Items = newEntity.Items;
                }
                else
                {
                    var existing = await context.SalesInvoices
                                                .Include(s => s.Items)
                                                .FirstOrDefaultAsync(s => s.Id == invoice.Id);
                    if (existing == null)
                    {
                        throw new InvalidOperationException("لم يتم العثور على الفاتورة لتعديلها.");
                    }

                    if (existing.Status == "معتمدة")
                    {
                        throw new InvalidOperationException("لا يمكن تعديل فاتورة معتمدة ومخصومة من المخزون لحماية حركة الأصناف.");
                    }

                    existing.InvoiceNumber = invoice.InvoiceNumber.Trim();
                    existing.InvoiceDate = invoice.InvoiceDate;
                    existing.CustomerId = invoice.CustomerId;
                    existing.CustomerName = string.IsNullOrWhiteSpace(invoice.CustomerName) ? "عميل نقدي" : invoice.CustomerName.Trim();
                    existing.CustomerPhone = invoice.CustomerPhone?.Trim() ?? string.Empty;
                    existing.CustomerAddress = invoice.CustomerAddress?.Trim() ?? string.Empty;
                    existing.SubTotal = invoice.SubTotal;
                    existing.Discount = invoice.Discount;
                    existing.GrandTotal = invoice.GrandTotal;
                    existing.PaymentMethod = invoice.PaymentMethod;
                    existing.PaidAmount = invoice.PaidAmount;
                    existing.Notes = invoice.Notes?.Trim() ?? string.Empty;

                    // تحديث البنود
                    context.SalesInvoiceItems.RemoveRange(existing.Items);
                    existing.Items = invoice.Items.Select(i => new SalesInvoiceItemModel
                    {
                        SalesInvoiceId = existing.Id,
                        ProductId = i.ProductId,
                        PartNumber = i.PartNumber?.Trim() ?? string.Empty,
                        Barcode = i.Barcode?.Trim() ?? string.Empty,
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
                throw new InvalidOperationException($"فشل حفظ مسودة فاتورة المبيعات: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// اعتماد فاتورة البيع والتحقق من توفر الأرصدة وخصم الكميات من المخزون داخل Transaction واحدة
        /// </summary>
        public async Task<SalesInvoiceModel> PostInvoiceAsync(SalesInvoiceModel invoice)
        {
            if (invoice == null) throw new ArgumentNullException(nameof(invoice));
            if (invoice.Items == null || invoice.Items.Count == 0)
            {
                throw new InvalidOperationException("لا يمكن اعتماد فاتورة مبيعات خالية من الأصناف.");
            }

            // فحص صحة الكميات والأسعار والخصم
            foreach (var item in invoice.Items)
            {
                if (item.Quantity <= 0)
                {
                    throw new InvalidOperationException($"الكمية للصنف '{item.ProductName}' يجب أن تكون أكبر من الصفر.");
                }
                if (item.UnitPrice < 0)
                {
                    throw new InvalidOperationException($"سعر البيع للصنف '{item.ProductName}' لا يمكن أن يكون سالباً.");
                }
            }

            if (invoice.Discount < 0)
            {
                throw new InvalidOperationException("قيمة الخصم لا يمكن أن تكون سالبة.");
            }

            invoice.RecalculateTotals();
            if (invoice.Discount > invoice.SubTotal)
            {
                throw new InvalidOperationException("لا يمكن أن يتجاوز الخصم إجمالي قيمة الفاتورة.");
            }

            using var context = CreateContext();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                SalesInvoiceModel targetInvoice;

                if (invoice.Id == 0)
                {
                    var exists = await context.SalesInvoices.AnyAsync(s => s.InvoiceNumber == invoice.InvoiceNumber);
                    if (exists)
                    {
                        throw new InvalidOperationException($"رقم الفاتورة '{invoice.InvoiceNumber}' مسجل مسبقاً. تم إلغاء الاعتماد لمنع التكرار.");
                    }

                    targetInvoice = new SalesInvoiceModel
                    {
                        InvoiceNumber = invoice.InvoiceNumber.Trim(),
                        InvoiceDate = invoice.InvoiceDate,
                        Status = "معتمدة",
                        PostedDate = DateTime.Now,
                        SubTotal = invoice.SubTotal,
                        Discount = invoice.Discount,
                        GrandTotal = invoice.GrandTotal,
                        PaymentMethod = invoice.PaymentMethod,
                        PaidAmount = invoice.PaidAmount,
                        Notes = invoice.Notes?.Trim() ?? string.Empty,
                        Items = invoice.Items.Select(i => new SalesInvoiceItemModel
                        {
                            ProductId = i.ProductId,
                            PartNumber = i.PartNumber?.Trim() ?? string.Empty,
                            Barcode = i.Barcode?.Trim() ?? string.Empty,
                            ProductName = i.ProductName?.Trim() ?? string.Empty,
                            Quantity = i.Quantity,
                            UnitPrice = i.UnitPrice
                        }).ToList()
                    };

                    await ResolveAndLinkCustomerAsync(context, invoice, targetInvoice);

                    await context.SalesInvoices.AddAsync(targetInvoice);
                    await context.SaveChangesAsync();
                    invoice.Id = targetInvoice.Id;
                }
                else
                {
                    targetInvoice = await context.SalesInvoices
                                                 .Include(s => s.Items)
                                                 .FirstOrDefaultAsync(s => s.Id == invoice.Id)
                                                 ?? throw new InvalidOperationException("الفاتورة المراد اعتمادها غير موجودة.");

                    // منع الاعتماد المتكرر لنفس الفاتورة إطلاقاً
                    if (targetInvoice.Status == "معتمدة")
                    {
                        throw new InvalidOperationException("هذه الفاتورة معتمدة مسبقاً بالفعل! لا يمكن إعادة اعتمادها لتجنب تكرار خصم المخزون.");
                    }

                    targetInvoice.InvoiceNumber = invoice.InvoiceNumber.Trim();
                    targetInvoice.InvoiceDate = invoice.InvoiceDate;
                    targetInvoice.Status = "معتمدة";
                    targetInvoice.PostedDate = DateTime.Now;
                    targetInvoice.SubTotal = invoice.SubTotal;
                    targetInvoice.Discount = invoice.Discount;
                    targetInvoice.GrandTotal = invoice.GrandTotal;
                    targetInvoice.PaymentMethod = invoice.PaymentMethod;
                    targetInvoice.PaidAmount = invoice.PaidAmount;
                    targetInvoice.Notes = invoice.Notes?.Trim() ?? string.Empty;

                    await ResolveAndLinkCustomerAsync(context, invoice, targetInvoice);

                    // تحديث البنود
                    context.SalesInvoiceItems.RemoveRange(targetInvoice.Items);
                    targetInvoice.Items = invoice.Items.Select(i => new SalesInvoiceItemModel
                    {
                        SalesInvoiceId = targetInvoice.Id,
                        ProductId = i.ProductId,
                        PartNumber = i.PartNumber?.Trim() ?? string.Empty,
                        ProductName = i.ProductName?.Trim() ?? string.Empty,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice
                    }).ToList();

                    await context.SaveChangesAsync();
                }

                // 2. التحقق الدقيق من رصيد كل صنف في المخزن والخصم الذري
                foreach (var line in targetInvoice.Items)
                {
                    var product = await context.Products.FirstOrDefaultAsync(p => p.Id == line.ProductId);
                    if (product == null)
                    {
                        throw new InvalidOperationException($"الصنف ذو المعرف ({line.ProductId}) غير موجود في قاعدة بيانات الأصناف.");
                    }

                    if (product.Quantity < line.Quantity)
                    {
                        throw new InvalidOperationException(
                            $"الكمية المتوفرة في المستودع للصنف '{product.Name}' ({product.Quantity} قطعة) غير كافية لبيع ({line.Quantity} قطعة).\nيرجى تعديل الكمية قبل الاعتماد.");
                    }

                    int previousQty = product.Quantity;
                    product.Quantity -= line.Quantity; // خصم الكمية المباعة من المستودع

                    // تسجيل حركة المخزون في سجل التدقيق الرقابي
                    var movement = new InventoryMovementModel
                    {
                        ProductId = product.Id,
                        PartNumber = product.PartNumber,
                        ProductName = product.Name,
                        MovementType = "بيع صادر",
                        QuantityChange = -line.Quantity,
                        PreviousQuantity = previousQty,
                        NewQuantity = product.Quantity,
                        MovementDate = DateTime.Now,
                        ReferenceId = targetInvoice.Id,
                        ReferenceNumber = targetInvoice.InvoiceNumber,
                        Notes = $"فاتورة مبيعات صادرة رقم {targetInvoice.InvoiceNumber} للعميل {targetInvoice.CustomerName}"
                    };

                    await context.InventoryMovements.AddAsync(movement);
                }

                await context.SaveChangesAsync();

                // تثبيت المعاملة
                await transaction.CommitAsync();

                invoice.Status = "معتمدة";
                invoice.PostedDate = targetInvoice.PostedDate;
                invoice.Items = targetInvoice.Items;
                invoice.CustomerId = targetInvoice.CustomerId;
                invoice.CustomerName = targetInvoice.CustomerName;
                invoice.CustomerPhone = targetInvoice.CustomerPhone;
                invoice.CustomerAddress = targetInvoice.CustomerAddress;

                // إشعار لوحة التحكم وباقي الشاشات وشاشة العملاء بالتحديث
                DataEvents.RaiseInventoryOrInvoicesChanged();
                DataEvents.RaiseCustomersChanged();

                return invoice;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException($"فشل اعتماد فاتورة المبيعات وتحديث المخزون: {ex.Message}", ex);
            }
        }

        public async Task<SalesInvoiceModel> PostInvoiceAsync(int invoiceId)
        {
            var invoice = await GetInvoiceByIdAsync(invoiceId);
            if (invoice == null)
            {
                throw new InvalidOperationException($"لم يتم العثور على فاتورة المبيعات رقم ({invoiceId}).");
            }
            return await PostInvoiceAsync(invoice);
        }

        public async Task<IEnumerable<SalesInvoiceModel>> SearchInvoicesAsync(string query)
        {
            try
            {
                using var context = CreateContext();
                if (string.IsNullOrWhiteSpace(query))
                {
                    return await GetAllInvoicesAsync();
                }

                var q = query.Trim().ToLower();
                return await context.SalesInvoices
                                    .Include(s => s.Items)
                                    .AsNoTracking()
                                    .Where(s =>
                                        s.InvoiceNumber.ToLower().Contains(q) ||
                                        (s.CustomerName != null && s.CustomerName.ToLower().Contains(q)) ||
                                        (s.CustomerPhone != null && s.CustomerPhone.ToLower().Contains(q)) ||
                                        (s.PaymentMethod != null && s.PaymentMethod.ToLower().Contains(q)) ||
                                        (s.Notes != null && s.Notes.ToLower().Contains(q))
                                    )
                                    .OrderByDescending(s => s.InvoiceDate)
                                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل البحث في فواتير المبيعات: {ex.Message}", ex);
            }
        }

        public async Task<bool> CancelInvoiceAsync(int invoiceId, string reason)
        {
            using var context = CreateContext();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                var targetInvoice = await context.SalesInvoices
                                                 .Include(s => s.Items)
                                                 .FirstOrDefaultAsync(s => s.Id == invoiceId)
                                                 ?? throw new InvalidOperationException($"لم يتم العثور على فاتورة المبيعات رقم ({invoiceId}).");

                if (targetInvoice.Status == "ملغاة")
                {
                    throw new InvalidOperationException($"فاتورة المبيعات رقم '{targetInvoice.InvoiceNumber}' ملغاة بالفعل مسبقاً! لا يمكن تكرار الإلغاء.");
                }

                // فحص هل توجد مرتجعات معتمدة مسجلة على الفاتورة
                bool hasReturns = await context.SalesReturns
                                               .AnyAsync(r => r.SalesInvoiceId == invoiceId && r.Status == "معتمدة");
                if (hasReturns)
                {
                    throw new InvalidOperationException(
                        $"لا يمكن إلغاء فاتورة المبيعات رقم '{targetInvoice.InvoiceNumber}' لوجود مرتجعات مبيعات معتمدة مسجلة عليها مسبقاً.\nلحماية سلامة المخزون والحسابات، لا يمكن إلغاء فاتورة تم إرجاع بنود منها.");
                }

                if (targetInvoice.Status == "معتمدة")
                {
                    // إعادة الكميات المخصومة إلى المستودع وتسجيل حركات عكسية
                    foreach (var line in targetInvoice.Items)
                    {
                        var product = await context.Products.FirstOrDefaultAsync(p => p.Id == line.ProductId);
                        if (product == null)
                        {
                            throw new InvalidOperationException($"الصنف ذو المعرف ({line.ProductId}) غير موجود في قاعدة بيانات الأصناف.");
                        }

                        int prevQty = product.Quantity;
                        product.Quantity += line.Quantity; // إعادة الكميات المباعة إلى المستودع

                        var reversalMovement = new InventoryMovementModel
                        {
                            ProductId = product.Id,
                            PartNumber = product.PartNumber,
                            ProductName = product.Name,
                            MovementType = "إلغاء بيع وارد",
                            QuantityChange = line.Quantity,
                            PreviousQuantity = prevQty,
                            NewQuantity = product.Quantity,
                            MovementDate = DateTime.Now,
                            ReferenceId = targetInvoice.Id,
                            ReferenceNumber = targetInvoice.InvoiceNumber,
                            Notes = $"حركة عكسية لإلغاء فاتورة مبيعات رقم {targetInvoice.InvoiceNumber}. السبب: {(string.IsNullOrWhiteSpace(reason) ? "إلغاء من قبل المستخدم" : reason.Trim())}"
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
                throw new InvalidOperationException($"فشل إلغاء فاتورة المبيعات: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// معالجة وتوحيد وربط بيانات العميل في فاتورة المبيعات ضمن نفس المعاملة المالية الذرية (Single Transaction)
        /// القواعد:
        /// 1. اختيار من الاقتراحات -> ربط بالعميل المحدد وتعبئة الحقول الفارغة فقط (دون الكتابة فوق بياناته).
        /// 2. إدخال رقم هاتف -> توحيد الرقم (PhoneHelper)، البحث عن عميل بنفس الرقم الموحد، إعادة استخدامه إن وجد، أو إنشاء عميل جديد.
        ///    عند حدوث سباق (DbUpdateException) على الفهرس الفريد: إعادة قراءة العميل واستخدامه فورياً.
        /// 3. إدخال اسم فقط -> مطابقة بالاسم الموحد (trim, collapse spaces, case-insensitive)، إعادة استخدامه إن وجد أو إنشاؤه.
        /// 4. ترك الحقول فارغة / عميل نقدي -> بيع مباشر (Walk-in sale) مع CustomerId = null.
        /// </summary>
        private static async Task ResolveAndLinkCustomerAsync(CarPartsDbContext context, SalesInvoiceModel inputInvoice, SalesInvoiceModel targetInvoice)
        {
            // 1. اختيار صريح لعميل مسجل مسبقاً من المقترحات
            if (inputInvoice.CustomerId.HasValue && inputInvoice.CustomerId.Value > 0)
            {
                var existing = await context.Customers.FirstOrDefaultAsync(c => c.Id == inputInvoice.CustomerId.Value);
                if (existing != null)
                {
                    var normInputPhone = PhoneHelper.Normalize(inputInvoice.CustomerPhone);
                    if (string.IsNullOrWhiteSpace(existing.Phone) && !string.IsNullOrWhiteSpace(normInputPhone))
                    {
                        bool phoneTaken = await context.Customers.AnyAsync(c => c.Id != existing.Id && c.Phone == normInputPhone);
                        if (!phoneTaken)
                        {
                            existing.Phone = normInputPhone;
                        }
                    }
                    if (string.IsNullOrWhiteSpace(existing.Address) && !string.IsNullOrWhiteSpace(inputInvoice.CustomerAddress))
                    {
                        existing.Address = inputInvoice.CustomerAddress.Trim();
                    }
                    targetInvoice.CustomerId = existing.Id;
                    targetInvoice.CustomerName = existing.Name;
                    targetInvoice.CustomerPhone = existing.Phone ?? string.Empty;
                    targetInvoice.CustomerAddress = existing.Address ?? string.Empty;
                    return;
                }
            }

            // 2. إدخال رقم الهاتف
            var normalizedPhone = PhoneHelper.Normalize(inputInvoice.CustomerPhone);
            if (!string.IsNullOrWhiteSpace(normalizedPhone))
            {
                var existingByPhone = await context.Customers.FirstOrDefaultAsync(c => c.Phone == normalizedPhone);
                if (existingByPhone == null)
                {
                    var candidates = await context.Customers.Where(c => c.Phone != null && c.Phone != "").ToListAsync();
                    existingByPhone = candidates.FirstOrDefault(c => PhoneHelper.Normalize(c.Phone) == normalizedPhone);
                }

                if (existingByPhone != null)
                {
                    if (existingByPhone.Phone != normalizedPhone)
                    {
                        existingByPhone.Phone = normalizedPhone;
                    }
                    if ((string.IsNullOrWhiteSpace(existingByPhone.Name) || existingByPhone.Name == "عميل نقدي")
                        && !string.IsNullOrWhiteSpace(inputInvoice.CustomerName)
                        && inputInvoice.CustomerName.Trim() != "عميل نقدي")
                    {
                        existingByPhone.Name = inputInvoice.CustomerName.Trim();
                    }
                    // إعادة الاستخدام دون مساس بالبيانات الموجودة إلا ملء الحقول الفارغة
                    if (string.IsNullOrWhiteSpace(existingByPhone.Address) && !string.IsNullOrWhiteSpace(inputInvoice.CustomerAddress))
                    {
                        existingByPhone.Address = inputInvoice.CustomerAddress.Trim();
                    }
                    targetInvoice.CustomerId = existingByPhone.Id;
                    targetInvoice.CustomerName = existingByPhone.Name;
                    targetInvoice.CustomerPhone = existingByPhone.Phone ?? string.Empty;
                    targetInvoice.CustomerAddress = existingByPhone.Address ?? string.Empty;
                    return;
                }

                // إنشاء عميل جديد بالهاتف الموحد
                var rawName = inputInvoice.CustomerName?.Trim();
                var newName = string.IsNullOrWhiteSpace(rawName) || rawName == "عميل نقدي" ? "عميل نقدي" : rawName;
                var newCustomer = new CustomerModel
                {
                    Name = newName,
                    Phone = normalizedPhone,
                    Address = inputInvoice.CustomerAddress?.Trim() ?? string.Empty,
                    CreatedDate = DateTime.Now,
                    IsActive = true
                };

                try
                {
                    await context.Customers.AddAsync(newCustomer);
                    await context.SaveChangesAsync();
                    targetInvoice.CustomerId = newCustomer.Id;
                    targetInvoice.CustomerName = newCustomer.Name;
                    targetInvoice.CustomerPhone = newCustomer.Phone;
                    targetInvoice.CustomerAddress = newCustomer.Address;
                }
                catch (DbUpdateException)
                {
                    // في حال تكرار الفهرس الفريد عند حدوث سباق بيانات، نعيد قراءة العميل ونربطه
                    context.Entry(newCustomer).State = EntityState.Detached;
                    var raceCustomer = await context.Customers.FirstOrDefaultAsync(c => c.Phone == normalizedPhone);
                    if (raceCustomer != null)
                    {
                        if (string.IsNullOrWhiteSpace(raceCustomer.Address) && !string.IsNullOrWhiteSpace(inputInvoice.CustomerAddress))
                        {
                            raceCustomer.Address = inputInvoice.CustomerAddress.Trim();
                        }
                        targetInvoice.CustomerId = raceCustomer.Id;
                        targetInvoice.CustomerName = raceCustomer.Name;
                        targetInvoice.CustomerPhone = raceCustomer.Phone ?? string.Empty;
                        targetInvoice.CustomerAddress = raceCustomer.Address ?? string.Empty;
                    }
                    else
                    {
                        throw;
                    }
                }
                return;
            }

            // 3. إدخال اسم العميل فقط دون هاتف
            var nameInput = inputInvoice.CustomerName?.Trim();
            if (!string.IsNullOrWhiteSpace(nameInput) && nameInput != "عميل نقدي")
            {
                var normName = Regex.Replace(nameInput, @"\s+", " ").ToLowerInvariant();
                var allCustomers = await context.Customers.ToListAsync();
                var existingByName = allCustomers.FirstOrDefault(c =>
                    Regex.Replace(c.Name.Trim(), @"\s+", " ").ToLowerInvariant() == normName);

                if (existingByName != null)
                {
                    if (string.IsNullOrWhiteSpace(existingByName.Address) && !string.IsNullOrWhiteSpace(inputInvoice.CustomerAddress))
                    {
                        existingByName.Address = inputInvoice.CustomerAddress.Trim();
                    }
                    targetInvoice.CustomerId = existingByName.Id;
                    targetInvoice.CustomerName = existingByName.Name;
                    targetInvoice.CustomerPhone = existingByName.Phone ?? string.Empty;
                    targetInvoice.CustomerAddress = existingByName.Address ?? string.Empty;
                    return;
                }

                // إنشاء عميل جديد بالاسم
                var newCustomer = new CustomerModel
                {
                    Name = Regex.Replace(nameInput, @"\s+", " "),
                    Phone = string.Empty,
                    Address = inputInvoice.CustomerAddress?.Trim() ?? string.Empty,
                    CreatedDate = DateTime.Now,
                    IsActive = true
                };

                await context.Customers.AddAsync(newCustomer);
                await context.SaveChangesAsync();
                targetInvoice.CustomerId = newCustomer.Id;
                targetInvoice.CustomerName = newCustomer.Name;
                targetInvoice.CustomerPhone = string.Empty;
                targetInvoice.CustomerAddress = newCustomer.Address;
                return;
            }

            // 4. بيع نقدي مباشر (Walk-in sale)
            targetInvoice.CustomerId = null;
            targetInvoice.CustomerName = "عميل نقدي";
            targetInvoice.CustomerPhone = string.Empty;
            targetInvoice.CustomerAddress = string.Empty;
        }
    }
}
