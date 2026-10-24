using Microsoft.EntityFrameworkCore;
using CarPartManager.Data;
using CarPartManager.Models;

namespace CarPartManager.Services
{
    /// <summary>
    /// تنفيذ خدمة المصروفات المعتمدة على SQLite.
    /// ملاحظة مهمة: لا يدعم مزود SQLite تجميع (SUM) على نوع decimal مباشرة في SQL،
    /// لذلك نُفلتر الصفوف في قاعدة البيانات ثم نجمع في الذاكرة — نفس النمط المتبع في SqliteDashboardService.
    /// </summary>
    public class SqliteExpenseService : IExpenseService
    {
        private CarPartsDbContext CreateContext() => new CarPartsDbContext();

        public const string DefaultPaymentMethod = "نقدي";

        // ── المصروفات ──────────────────────────────────────────────────────────

        public async Task<IEnumerable<ExpenseModel>> GetExpensesAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? categoryId = null,
            string? paymentMethod = null,
            bool includeCancelled = false,
            string? searchText = null)
        {
            try
            {
                using var context = CreateContext();
                var query = context.Expenses.AsNoTracking().AsQueryable();

                if (!includeCancelled)
                    query = query.Where(e => !e.IsCancelled);

                if (fromDate.HasValue)
                {
                    var from = fromDate.Value.Date;
                    query = query.Where(e => e.ExpenseDate >= from);
                }

                if (toDate.HasValue)
                {
                    var toExclusive = toDate.Value.Date.AddDays(1);
                    query = query.Where(e => e.ExpenseDate < toExclusive);
                }

                if (categoryId.HasValue && categoryId.Value > 0)
                    query = query.Where(e => e.CategoryId == categoryId.Value);

                if (!string.IsNullOrWhiteSpace(paymentMethod))
                {
                    var pm = paymentMethod.Trim();
                    query = query.Where(e => e.PaymentMethod == pm);
                }

                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    var term = searchText.Trim().ToLower();
                    query = query.Where(e =>
                        (e.Description ?? string.Empty).ToLower().Contains(term) ||
                        (e.ReferenceNumber ?? string.Empty).ToLower().Contains(term));
                }

                var list = await query
                    .OrderByDescending(e => e.ExpenseDate)
                    .ThenByDescending(e => e.Id)
                    .ToListAsync();

                // إثراء اسم التصنيف للعرض فقط (غير مخزَّن في جدول المصروفات)
                var categoryNames = await context.ExpenseCategories
                    .AsNoTracking()
                    .ToDictionaryAsync(c => c.Id, c => c.Name);

                foreach (var item in list)
                {
                    item.CategoryName = categoryNames.TryGetValue(item.CategoryId, out var name) ? name : "—";
                }

                return list;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر تحميل قائمة المصروفات: {ex.Message}", ex);
            }
        }

        public async Task<ExpenseModel?> GetExpenseByIdAsync(int id)
        {
            try
            {
                using var context = CreateContext();
                var expense = await context.Expenses.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
                if (expense == null) return null;

                expense.CategoryName = await context.ExpenseCategories
                    .AsNoTracking()
                    .Where(c => c.Id == expense.CategoryId)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync();
                return expense;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر تحميل بيانات المصروف رقم {id}: {ex.Message}", ex);
            }
        }

        public async Task<ExpenseSummaryModel> GetSummaryAsync()
        {
            try
            {
                using var context = CreateContext();
                var today = DateTime.Today;
                var firstOfThisMonth = new DateTime(today.Year, today.Month, 1);
                var firstOfNextMonth = firstOfThisMonth.AddMonths(1);
                var firstOfPrevMonth = firstOfThisMonth.AddMonths(-1);

                // نُفلتر في قاعدة البيانات على مدى الشهرين (الحالي والسابق) مع استبعاد الملغاة
                var rows = await context.Expenses
                    .AsNoTracking()
                    .Where(e => !e.IsCancelled &&
                                e.ExpenseDate >= firstOfPrevMonth &&
                                e.ExpenseDate < firstOfNextMonth)
                    .ToListAsync();

                var summary = new ExpenseSummaryModel();

                summary.TodayTotal = rows
                    .Where(e => e.ExpenseDate.Date == today)
                    .Sum(e => e.Amount);

                var currentMonthRows = rows
                    .Where(e => e.ExpenseDate >= firstOfThisMonth && e.ExpenseDate < firstOfNextMonth)
                    .ToList();

                summary.CurrentMonthTotal = currentMonthRows.Sum(e => e.Amount);
                summary.CurrentMonthCount = currentMonthRows.Count;

                summary.PreviousMonthTotal = rows
                    .Where(e => e.ExpenseDate >= firstOfPrevMonth && e.ExpenseDate < firstOfThisMonth)
                    .Sum(e => e.Amount);

                var top = currentMonthRows
                    .GroupBy(e => e.CategoryId)
                    .Select(g => new { CategoryId = g.Key, Total = g.Sum(x => x.Amount) })
                    .OrderByDescending(x => x.Total)
                    .FirstOrDefault();

                if (top != null && top.Total > 0)
                {
                    var name = await context.ExpenseCategories
                        .AsNoTracking()
                        .Where(c => c.Id == top.CategoryId)
                        .Select(c => c.Name)
                        .FirstOrDefaultAsync();

                    summary.TopCategoryName = string.IsNullOrWhiteSpace(name) ? "—" : name!;
                    summary.TopCategoryTotal = top.Total;
                }

                return summary;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر حساب ملخص المصروفات: {ex.Message}", ex);
            }
        }

        public async Task<ExpenseModel> AddExpenseAsync(ExpenseModel expense)
        {
            try
            {
                using var context = CreateContext();

                var entity = new ExpenseModel
                {
                    ExpenseDate = expense.ExpenseDate.Date,
                    CategoryId = expense.CategoryId,
                    Description = expense.Description?.Trim() ?? string.Empty,
                    Amount = expense.Amount,
                    PaymentMethod = string.IsNullOrWhiteSpace(expense.PaymentMethod)
                        ? DefaultPaymentMethod
                        : expense.PaymentMethod.Trim(),
                    ReferenceNumber = string.IsNullOrWhiteSpace(expense.ReferenceNumber)
                        ? null
                        : expense.ReferenceNumber.Trim(),
                    Notes = string.IsNullOrWhiteSpace(expense.Notes) ? null : expense.Notes.Trim(),
                    IsCancelled = false,
                    CreatedAt = DateTime.Now,
                    CreatedBy = string.IsNullOrWhiteSpace(expense.CreatedBy) ? null : expense.CreatedBy!.Trim()
                };

                await context.Expenses.AddAsync(entity);
                await context.SaveChangesAsync();

                expense.Id = entity.Id;
                expense.CreatedAt = entity.CreatedAt;
                expense.PaymentMethod = entity.PaymentMethod;
                return expense;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل حفظ المصروف في قاعدة البيانات: {ex.Message}", ex);
            }
        }

        public async Task<bool> UpdateExpenseAsync(ExpenseModel expense)
        {
            try
            {
                using var context = CreateContext();

                var existing = await context.Expenses.FirstOrDefaultAsync(e => e.Id == expense.Id);
                if (existing == null) return false;

                var previousCategoryName = await context.ExpenseCategories
                    .AsNoTracking()
                    .Where(c => c.Id == existing.CategoryId)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync();

                var newCategoryName = await context.ExpenseCategories
                    .AsNoTracking()
                    .Where(c => c.Id == expense.CategoryId)
                    .Select(c => c.Name)
                    .FirstOrDefaultAsync();

                var newDescription = expense.Description?.Trim() ?? string.Empty;
                var newPaymentMethod = string.IsNullOrWhiteSpace(expense.PaymentMethod)
                    ? DefaultPaymentMethod
                    : expense.PaymentMethod.Trim();

                var hasChanges =
                    existing.Amount != expense.Amount ||
                    existing.CategoryId != expense.CategoryId ||
                    existing.ExpenseDate.Date != expense.ExpenseDate.Date ||
                    (existing.Description ?? string.Empty) != newDescription ||
                    (existing.PaymentMethod ?? string.Empty) != newPaymentMethod;

                // حفظ أثر القيم الأصلية قبل التعديل (جدول تدقيق مبسط) حتى لا تُفقد البيانات القديمة
                if (hasChanges)
                {
                    var now = DateTime.Now;
                    await context.ExpenseHistories.AddAsync(new ExpenseHistoryModel
                    {
                        ExpenseId = existing.Id,
                        ChangedAt = now,
                        ChangedBy = string.IsNullOrWhiteSpace(expense.ModifiedBy) ? null : expense.ModifiedBy!.Trim(),
                        ChangeType = "تعديل",
                        PreviousAmount = existing.Amount,
                        NewAmount = expense.Amount,
                        PreviousCategoryName = previousCategoryName,
                        NewCategoryName = newCategoryName,
                        PreviousExpenseDate = existing.ExpenseDate,
                        NewExpenseDate = expense.ExpenseDate.Date,
                        Reason = "تعديل بيانات المصروف"
                    });

                    existing.ModifiedAt = now;
                    existing.ModifiedBy = string.IsNullOrWhiteSpace(expense.ModifiedBy) ? null : expense.ModifiedBy!.Trim();
                }

                existing.ExpenseDate = expense.ExpenseDate.Date;
                existing.CategoryId = expense.CategoryId;
                existing.Description = newDescription;
                existing.Amount = expense.Amount;
                existing.PaymentMethod = newPaymentMethod;
                existing.ReferenceNumber = string.IsNullOrWhiteSpace(expense.ReferenceNumber)
                    ? null
                    : expense.ReferenceNumber.Trim();
                existing.Notes = string.IsNullOrWhiteSpace(expense.Notes) ? null : expense.Notes.Trim();

                await context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل تحديث بيانات المصروف: {ex.Message}", ex);
            }
        }

        public async Task<bool> CancelExpenseAsync(int expenseId, string reason, string? cancelledBy = null)
        {
            try
            {
                using var context = CreateContext();

                var existing = await context.Expenses.FirstOrDefaultAsync(e => e.Id == expenseId);
                if (existing == null) return false;
                if (existing.IsCancelled) return true;

                var now = DateTime.Now;
                var trimmedReason = reason?.Trim() ?? string.Empty;

                existing.IsCancelled = true;
                existing.CancellationDate = now;
                existing.CancellationReason = trimmedReason;
                existing.ModifiedAt = now;
                existing.ModifiedBy = string.IsNullOrWhiteSpace(cancelledBy) ? null : cancelledBy!.Trim();

                await context.ExpenseHistories.AddAsync(new ExpenseHistoryModel
                {
                    ExpenseId = existing.Id,
                    ChangedAt = now,
                    ChangedBy = existing.ModifiedBy,
                    ChangeType = "إلغاء",
                    PreviousAmount = existing.Amount,
                    NewAmount = existing.Amount,
                    PreviousCategoryName = await context.ExpenseCategories
                        .AsNoTracking()
                        .Where(c => c.Id == existing.CategoryId)
                        .Select(c => c.Name)
                        .FirstOrDefaultAsync(),
                    NewCategoryName = await context.ExpenseCategories
                        .AsNoTracking()
                        .Where(c => c.Id == existing.CategoryId)
                        .Select(c => c.Name)
                        .FirstOrDefaultAsync(),
                    PreviousExpenseDate = existing.ExpenseDate,
                    NewExpenseDate = existing.ExpenseDate,
                    Reason = trimmedReason
                });

                await context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل إلغاء المصروف: {ex.Message}", ex);
            }
        }

        public async Task<IEnumerable<ExpenseHistoryModel>> GetExpenseHistoryAsync(int expenseId)
        {
            try
            {
                using var context = CreateContext();
                return await context.ExpenseHistories
                    .AsNoTracking()
                    .Where(h => h.ExpenseId == expenseId)
                    .OrderByDescending(h => h.ChangedAt)
                    .ThenByDescending(h => h.Id)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر تحميل سجل تعديلات المصروف: {ex.Message}", ex);
            }
        }

        // ── التصنيفات ──────────────────────────────────────────────────────────

        public async Task<IEnumerable<ExpenseCategoryModel>> GetCategoriesAsync(bool includeInactive = true)
        {
            try
            {
                using var context = CreateContext();

                var query = context.ExpenseCategories.AsNoTracking().AsQueryable();
                if (!includeInactive)
                    query = query.Where(c => c.IsActive);

                var list = await query
                    .OrderByDescending(c => c.IsActive)
                    .ThenBy(c => c.Name)
                    .ToListAsync();

                // عدد المصروفات المرتبطة بكل تصنيف (بما فيها الملغاة — لأنها ما زالت مرتبطة بالتصنيف)
                var counts = await context.Expenses
                    .AsNoTracking()
                    .GroupBy(e => e.CategoryId)
                    .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                    .ToListAsync();

                var map = counts.ToDictionary(x => x.CategoryId, x => x.Count);
                foreach (var category in list)
                {
                    category.ExpensesCount = map.TryGetValue(category.Id, out var count) ? count : 0;
                }

                return list;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر تحميل تصنيفات المصروفات: {ex.Message}", ex);
            }
        }

        public async Task<ExpenseCategoryModel> AddCategoryAsync(string name)
        {
            try
            {
                var normalized = NameNormalizer.Normalize(name);
                if (string.IsNullOrWhiteSpace(normalized))
                    throw new InvalidOperationException("اسم التصنيف مطلوب ولا يمكن أن يكون فارغاً.");

                using var context = CreateContext();

                // منع تكرار الاسم بعد التطبيع (إزالة المسافات الزائدة + تجاهل حالة الأحرف)
                var existingNames = await context.ExpenseCategories
                    .AsNoTracking()
                    .Select(c => c.Name)
                    .ToListAsync();

                if (existingNames.Any(n => NameNormalizer.AreEquivalent(n, normalized)))
                    throw new InvalidOperationException("يوجد تصنيف بنفس الاسم بالفعل.");

                var entity = new ExpenseCategoryModel
                {
                    Name = normalized,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };

                await context.ExpenseCategories.AddAsync(entity);
                await context.SaveChangesAsync();
                return entity;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل إضافة التصنيف: {ex.Message}", ex);
            }
        }

        public async Task<bool> SetCategoryActiveAsync(int categoryId, bool isActive)
        {
            try
            {
                using var context = CreateContext();
                var existing = await context.ExpenseCategories.FirstOrDefaultAsync(c => c.Id == categoryId);
                if (existing == null) return false;

                existing.IsActive = isActive;
                await context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل تحديث حالة التصنيف: {ex.Message}", ex);
            }
        }

        public async Task<bool> IsCategoryNameTakenAsync(string name, int? excludeCategoryId = null)
        {
            try
            {
                var normalized = NameNormalizer.Normalize(name);
                if (string.IsNullOrWhiteSpace(normalized)) return false;

                using var context = CreateContext();
                var candidates = await context.ExpenseCategories
                    .AsNoTracking()
                    .Where(c => !excludeCategoryId.HasValue || c.Id != excludeCategoryId.Value)
                    .Select(c => c.Name)
                    .ToListAsync();

                return candidates.Any(n => NameNormalizer.AreEquivalent(n, normalized));
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر التحقق من اسم التصنيف: {ex.Message}", ex);
            }
        }

        public async Task<int> GetCategoryUsageCountAsync(int categoryId)
        {
            try
            {
                using var context = CreateContext();
                return await context.Expenses
                    .AsNoTracking()
                    .CountAsync(e => e.CategoryId == categoryId);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر حساب عدد مصروفات التصنيف: {ex.Message}", ex);
            }
        }
    }
}
