using Microsoft.EntityFrameworkCore;
using CarPartManager.Data;
using CarPartManager.Models;

namespace CarPartManager.Services
{
    /// <summary>
    /// خدمة إدارة العملاء المعتمدة على SQLite (SqliteCustomerService)
    /// تطبق ICustomerService مع منطق الأعمال الكامل:
    /// الرصيد = الرصيد الافتتاحي + مجموع الفواتير المرتبطة غير المدفوعة - مجموع الدفعات المسجلة
    /// </summary>
    public class SqliteCustomerService : ICustomerService
    {
        private CarPartsDbContext CreateContext() => new CarPartsDbContext();

        // ══════════════════════════════════════════════════════════════════════
        // استرجاع العملاء
        // ══════════════════════════════════════════════════════════════════════

        public async Task<IEnumerable<CustomerModel>> GetAllCustomersAsync()
        {
            try
            {
                using var ctx = CreateContext();
                var customers = await ctx.Customers
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                // حساب الرصيد لكل عميل
                foreach (var c in customers)
                {
                    c.CurrentBalance = await CalculateBalanceInternalAsync(ctx, c.Id, c.OpeningBalance);
                }

                return customers;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر تحميل قائمة العملاء: {ex.Message}", ex);
            }
        }

        public async Task<CustomerModel?> GetByIdAsync(int id)
        {
            try
            {
                using var ctx = CreateContext();
                var c = await ctx.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                if (c != null)
                    c.CurrentBalance = await CalculateBalanceInternalAsync(ctx, c.Id, c.OpeningBalance);
                return c;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر تحميل بيانات العميل رقم {id}: {ex.Message}", ex);
            }
        }

        public async Task<IEnumerable<CustomerModel>> SearchCustomersAsync(string query)
        {
            try
            {
                using var ctx = CreateContext();
                List<CustomerModel> customers;

                if (string.IsNullOrWhiteSpace(query))
                {
                    customers = await ctx.Customers
                        .AsNoTracking()
                        .OrderBy(c => c.Name)
                        .ToListAsync();
                }
                else
                {
                    var q = query.Trim().ToLower();
                    customers = await ctx.Customers
                        .AsNoTracking()
                        .Where(c =>
                            c.Name.ToLower().Contains(q) ||
                            (c.Phone != null && c.Phone.ToLower().Contains(q)))
                        .OrderBy(c => c.Name)
                        .ToListAsync();
                }

                foreach (var c in customers)
                    c.CurrentBalance = await CalculateBalanceInternalAsync(ctx, c.Id, c.OpeningBalance);

                return customers;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل البحث عن العملاء: {ex.Message}", ex);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // إضافة وتعديل
        // ══════════════════════════════════════════════════════════════════════

        public async Task<CustomerModel> AddCustomerAsync(CustomerModel customer)
        {
            try
            {
                using var ctx = CreateContext();
                var entity = new CustomerModel
                {
                    Name = customer.Name?.Trim() ?? string.Empty,
                    Phone = PhoneHelper.Normalize(customer.Phone),
                    Address = customer.Address?.Trim() ?? string.Empty,
                    Notes = customer.Notes?.Trim() ?? string.Empty,
                    OpeningBalance = customer.OpeningBalance,
                    IsActive = customer.IsActive,
                    CreatedDate = DateTime.Now
                };

                await ctx.Customers.AddAsync(entity);
                await ctx.SaveChangesAsync();

                customer.Id = entity.Id;
                customer.Phone = entity.Phone;
                customer.CreatedDate = entity.CreatedDate;
                return customer;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل حفظ العميل في قاعدة البيانات: {ex.Message}", ex);
            }
        }

        public async Task<bool> UpdateCustomerAsync(CustomerModel customer)
        {
            try
            {
                using var ctx = CreateContext();
                var existing = await ctx.Customers.FirstOrDefaultAsync(c => c.Id == customer.Id);
                if (existing == null) return false;

                existing.Name = customer.Name?.Trim() ?? string.Empty;
                existing.Phone = PhoneHelper.Normalize(customer.Phone);
                existing.Address = customer.Address?.Trim() ?? string.Empty;
                existing.Notes = customer.Notes?.Trim() ?? string.Empty;
                existing.OpeningBalance = customer.OpeningBalance;
                existing.IsActive = customer.IsActive;

                await ctx.SaveChangesAsync();
                customer.Phone = existing.Phone;
                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل تحديث بيانات العميل: {ex.Message}", ex);
            }
        }

        public async Task<bool> SetActiveStatusAsync(int customerId, bool isActive)
        {
            try
            {
                using var ctx = CreateContext();
                var existing = await ctx.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
                if (existing == null) return false;

                existing.IsActive = isActive;
                await ctx.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل تغيير حالة العميل: {ex.Message}", ex);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // حساب الرصيد
        // معادلة الرصيد:
        //   رصيد العميل = الرصيد الافتتاحي
        //               + مجموع (GrandTotal - PaidAmount) للفواتير المرتبطة المعتمدة
        //               - مجموع دفعات العميل المسجلة
        // ══════════════════════════════════════════════════════════════════════

        public async Task<decimal> CalculateBalanceAsync(int customerId)
        {
            try
            {
                using var ctx = CreateContext();
                var customer = await ctx.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == customerId);
                if (customer == null) return 0m;

                return await CalculateBalanceInternalAsync(ctx, customerId, customer.OpeningBalance);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل حساب رصيد العميل: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// الحساب الداخلي للرصيد (يستخدم نفس الـ context المفتوح لتجنب إعادة الاتصال)
        /// الصيغة: OpeningBalance + Σ(GrandTotal - PaidAmount) للفواتير المرتبطة المعتمدة - Σ دفعات العميل
        /// </summary>
        private static async Task<decimal> CalculateBalanceInternalAsync(
            CarPartsDbContext ctx, int customerId, decimal openingBalance)
        {
            // مجموع المبالغ غير المدفوعة من فواتير المبيعات المرتبطة بهذا العميل
            var invoicesDebt = await ctx.SalesInvoices
                .AsNoTracking()
                .Where(i => i.CustomerId == customerId && i.Status == "معتمدة")
                .SumAsync(i => (decimal?)(i.GrandTotal - i.PaidAmount)) ?? 0m;

            // مجموع الدفعات المسجلة الصالحة لهذا العميل (استبعاد الدفعات الملغاة)
            var totalPayments = await ctx.CustomerPayments
                .AsNoTracking()
                .Where(p => p.CustomerId == customerId && !p.IsCancelled)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            return Math.Max(0m, openingBalance + invoicesDebt - totalPayments);
        }

        // ══════════════════════════════════════════════════════════════════════
        // كشف الحساب
        // ══════════════════════════════════════════════════════════════════════

        public async Task<IEnumerable<CustomerStatementLineModel>> GetStatementAsync(
            int customerId, DateTime? from = null, DateTime? to = null)
        {
            try
            {
                using var ctx = CreateContext();
                var customer = await ctx.Customers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == customerId);
                if (customer == null) return Enumerable.Empty<CustomerStatementLineModel>();

                var lines = new List<CustomerStatementLineModel>();

                // ─ الرصيد الافتتاحي كأول سطر ───────────────────────────────
                if (customer.OpeningBalance > 0)
                {
                    lines.Add(new CustomerStatementLineModel
                    {
                        Date = customer.CreatedDate,
                        EntryType = "رصيد افتتاحي",
                        Reference = "—",
                        Description = "رصيد افتتاحي عند تسجيل العميل",
                        DebitAmount = customer.OpeningBalance,
                        CreditAmount = 0,
                        InvoiceTotal = customer.OpeningBalance,
                        InvoicePaid = 0,
                        InvoiceRemaining = customer.OpeningBalance,
                        SourceId = 0
                    });
                }

                // ─ فواتير المبيعات المرتبطة المعتمدة ───────────────────────────
                var invoiceQuery = ctx.SalesInvoices
                    .AsNoTracking()
                    .Where(i => i.CustomerId == customerId && i.Status == "معتمدة");

                if (from.HasValue)
                    invoiceQuery = invoiceQuery.Where(i => i.InvoiceDate >= from.Value.Date);
                if (to.HasValue)
                    invoiceQuery = invoiceQuery.Where(i => i.InvoiceDate <= to.Value.Date.AddDays(1).AddTicks(-1));

                var invoices = await invoiceQuery
                    .OrderBy(i => i.InvoiceDate)
                    .ToListAsync();

                foreach (var inv in invoices)
                {
                    var remaining = Math.Max(0m, inv.GrandTotal - inv.PaidAmount);
                    lines.Add(new CustomerStatementLineModel
                    {
                        Date = inv.InvoiceDate,
                        EntryType = "فاتورة بيع",
                        Reference = inv.InvoiceNumber,
                        Description = $"فاتورة بيع رقم {inv.InvoiceNumber}" +
                                      (inv.PaidAmount > 0 ? $" (مدفوع: {inv.PaidAmount:N2})" : ""),
                        InvoiceTotal = inv.GrandTotal,
                        InvoicePaid = inv.PaidAmount,
                        InvoiceRemaining = remaining,
                        DebitAmount = remaining,
                        CreditAmount = 0,
                        SourceId = inv.Id
                    });
                }

                // ─ الدفعات المسجلة ──────────────────────────────────────────
                var paymentQuery = ctx.CustomerPayments
                    .AsNoTracking()
                    .Where(p => p.CustomerId == customerId);

                if (from.HasValue)
                    paymentQuery = paymentQuery.Where(p => p.PaymentDate >= from.Value.Date);
                if (to.HasValue)
                    paymentQuery = paymentQuery.Where(p => p.PaymentDate <= to.Value.Date.AddDays(1).AddTicks(-1));

                var payments = await paymentQuery
                    .OrderBy(p => p.PaymentDate)
                    .ToListAsync();

                foreach (var pay in payments)
                {
                    if (pay.IsCancelled)
                    {
                        lines.Add(new CustomerStatementLineModel
                        {
                            Date = pay.PaymentDate,
                            EntryType = "دفعة ملغاة",
                            Reference = $"DPT-{pay.Id:D4}",
                            Description = $"دفعة ملغاة ({pay.PaymentMethod})" +
                                          (!string.IsNullOrWhiteSpace(pay.CancellationReason) ? $" — سبب الإلغاء: {pay.CancellationReason}" : "") +
                                          (!string.IsNullOrWhiteSpace(pay.Notes) ? $" (ملاحظة: {pay.Notes})" : ""),
                            DebitAmount = 0,
                            CreditAmount = 0,
                            SourceId = pay.Id,
                            IsCancelledPayment = true,
                            CancellationReason = pay.CancellationReason
                        });
                    }
                    else
                    {
                        lines.Add(new CustomerStatementLineModel
                        {
                            Date = pay.PaymentDate,
                            EntryType = "دفعة استلام",
                            Reference = $"DPT-{pay.Id:D4}",
                            Description = $"دفعة مستلمة ({pay.PaymentMethod})" +
                                          (!string.IsNullOrWhiteSpace(pay.Notes) ? $" — {pay.Notes}" : ""),
                            DebitAmount = 0,
                            CreditAmount = pay.Amount,
                            SourceId = pay.Id,
                            IsCancelledPayment = false
                        });
                    }
                }

                // ─ ترتيب كل السطور زمنياً وحساب الرصيد التراكمي ──────────────
                lines = lines.OrderBy(l => l.Date).ThenBy(l => l.SourceId).ToList();

                decimal runningBalance = 0m;
                foreach (var line in lines)
                {
                    runningBalance += line.DebitAmount - line.CreditAmount;
                    runningBalance = Math.Max(0m, runningBalance);
                    line.RunningBalance = runningBalance;
                }

                return lines;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل استرجاع كشف حساب العميل: {ex.Message}", ex);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // دفعات العميل
        // ══════════════════════════════════════════════════════════════════════

        public async Task<IEnumerable<CustomerPaymentModel>> GetPaymentsAsync(int customerId)
        {
            try
            {
                using var ctx = CreateContext();
                return await ctx.CustomerPayments
                    .AsNoTracking()
                    .Where(p => p.CustomerId == customerId)
                    .OrderByDescending(p => p.PaymentDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر تحميل دفعات العميل: {ex.Message}", ex);
            }
        }

        public async Task<CustomerPaymentModel> AddPaymentAsync(CustomerPaymentModel payment)
        {
            try
            {
                using var ctx = CreateContext();

                // حفظ الدفعة داخل معاملة لضمان الاتساق
                await using var transaction = await ctx.Database.BeginTransactionAsync();
                try
                {
                    var entity = new CustomerPaymentModel
                    {
                        CustomerId = payment.CustomerId,
                        Amount = payment.Amount,
                        PaymentDate = payment.PaymentDate,
                        PaymentMethod = payment.PaymentMethod?.Trim() ?? "نقدي",
                        Notes = payment.Notes?.Trim() ?? string.Empty,
                        CreatedDate = DateTime.Now,
                        IsCancelled = false
                    };

                    await ctx.CustomerPayments.AddAsync(entity);
                    await ctx.SaveChangesAsync();
                    await transaction.CommitAsync();

                    payment.Id = entity.Id;
                    payment.CreatedDate = entity.CreatedDate;
                    payment.IsCancelled = false;
                    return payment;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                throw new InvalidOperationException($"فشل تسجيل الدفعة: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// إلغاء دفعة مسجلة بالخطأ مع توثيق السبب والتاريخ دون حذف السجل المالي
        /// </summary>
        public async Task<bool> CancelPaymentAsync(int paymentId, string reason)
        {
            try
            {
                using var ctx = CreateContext();
                await using var transaction = await ctx.Database.BeginTransactionAsync();
                try
                {
                    var existing = await ctx.CustomerPayments.FirstOrDefaultAsync(p => p.Id == paymentId);
                    if (existing == null || existing.IsCancelled) return false;

                    existing.IsCancelled = true;
                    existing.CancellationDate = DateTime.Now;
                    existing.CancellationReason = string.IsNullOrWhiteSpace(reason)
                        ? "إلغاء دفعة مسجلة بالخطأ"
                        : reason.Trim();

                    await ctx.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return true;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل إلغاء الدفعة رقم {paymentId}: {ex.Message}", ex);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // التحقق من الهاتف
        // ══════════════════════════════════════════════════════════════════════

        public async Task<CustomerModel?> FindCustomerByPhoneAsync(string phone, int excludeCustomerId = 0)
        {
            var normalized = PhoneHelper.Normalize(phone);
            if (string.IsNullOrWhiteSpace(normalized)) return null;

            try
            {
                using var ctx = CreateContext();
                var candidates = await ctx.Customers
                    .AsNoTracking()
                    .Where(c => c.Id != excludeCustomerId && c.Phone != null && c.Phone != "")
                    .ToListAsync();

                return candidates.FirstOrDefault(c => PhoneHelper.Normalize(c.Phone) == normalized);
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> PhoneExistsAsync(string phone, int excludeCustomerId = 0)
        {
            var customer = await FindCustomerByPhoneAsync(phone, excludeCustomerId);
            return customer != null;
        }

        public async Task<IEnumerable<CustomerModel>> GetCustomerSuggestionsAsync(string query, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query)) return Enumerable.Empty<CustomerModel>();

            var q = query.Trim().ToLower();
            var normQueryDigits = PhoneHelper.NormalizeDigits(q);

            try
            {
                using var ctx = CreateContext();
                cancellationToken.ThrowIfCancellationRequested();

                var matches = await ctx.Customers
                    .AsNoTracking()
                    .Where(c => c.IsActive && (
                        c.Name.ToLower().Contains(q) ||
                        (c.Phone != null && c.Phone.Contains(q)) ||
                        (!string.IsNullOrEmpty(normQueryDigits) && c.Phone != null && c.Phone.Contains(normQueryDigits))))
                    .OrderBy(c => c.Name.ToLower().StartsWith(q) ? 0 : 1)
                    .ThenBy(c => c.Name)
                    .Take(8)
                    .ToListAsync(cancellationToken);

                foreach (var c in matches)
                {
                    c.CurrentBalance = await CalculateBalanceInternalAsync(ctx, c.Id, c.OpeningBalance);
                }

                return matches;
            }
            catch (OperationCanceledException)
            {
                return Enumerable.Empty<CustomerModel>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CustomerSuggestions] Error: {ex.Message}");
                return Enumerable.Empty<CustomerModel>();
            }
        }
    }
}
