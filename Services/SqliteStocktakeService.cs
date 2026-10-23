using Microsoft.EntityFrameworkCore;
using CarPartManager.Data;
using CarPartManager.Models;

namespace CarPartManager.Services
{
    /// <summary>
    /// خدمة إدارة الجرد المعتمدة على SQLite
    /// </summary>
    public class SqliteStocktakeService : IStocktakeService
    {
        private CarPartsDbContext CreateContext() => new CarPartsDbContext();

        public async Task<StocktakeSessionModel> CreateSessionAsync(string name, string scopeType, string? scopeFilter = null, string? notes = null)
        {
            using var context = CreateContext();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // Generate session number
                var year = DateTime.Now.Year;
                var prefix = $"STK-{year}-";
                var currentSessions = await context.StocktakeSessions
                    .AsNoTracking()
                    .Where(s => s.SessionNumber.StartsWith(prefix))
                    .Select(s => s.SessionNumber)
                    .ToListAsync();

                int maxIndex = 0;
                foreach (var sn in currentSessions)
                {
                    var suffix = sn.Replace(prefix, "");
                    if (int.TryParse(suffix, out int parsedIndex) && parsedIndex > maxIndex)
                    {
                        maxIndex = parsedIndex;
                    }
                }

                var sessionNumber = $"{prefix}{(maxIndex + 1):D4}";

                // Determine products to include based on scope
                var productsQuery = context.Products.AsQueryable();
                if (scopeType == "تصنيف" && !string.IsNullOrWhiteSpace(scopeFilter))
                {
                    productsQuery = productsQuery.Where(p => p.Category == scopeFilter);
                }

                var products = await productsQuery.AsNoTracking().ToListAsync();

                var session = new StocktakeSessionModel
                {
                    SessionNumber = sessionNumber,
                    Name = name.Trim(),
                    ScopeType = scopeType,
                    ScopeFilter = scopeFilter ?? string.Empty,
                    Notes = notes?.Trim() ?? string.Empty,
                    Status = "مسودة",
                    SessionDate = DateTime.Now
                };

                await context.StocktakeSessions.AddAsync(session);
                await context.SaveChangesAsync();

                // Create lines for each product with snapshot book quantity
                foreach (var product in products)
                {
                    var line = new StocktakeLineModel
                    {
                        SessionId = session.Id,
                        ProductId = product.Id,
                        PartNumber = product.PartNumber,
                        ProductName = product.Name,
                        Barcode = product.Barcode ?? string.Empty,
                        Category = product.Category,
                        BookQuantity = product.Quantity,
                        CostPrice = product.PurchasePrice,
                        CountedQuantity = null // Not counted yet
                    };

                    await context.StocktakeLines.AddAsync(line);
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                return session;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException($"فشل إنشاء جلسة الجرد: {ex.Message}", ex);
            }
        }

        public async Task<StocktakeSessionModel?> GetSessionByIdAsync(int id)
        {
            using var context = CreateContext();
            return await context.StocktakeSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<IEnumerable<StocktakeSessionModel>> GetAllSessionsAsync()
        {
            using var context = CreateContext();
            return await context.StocktakeSessions
                .AsNoTracking()
                .OrderByDescending(s => s.SessionDate)
                .ThenByDescending(s => s.Id)
                .ToListAsync();
        }

        public async Task<bool> UpdateSessionAsync(StocktakeSessionModel session)
        {
            using var context = CreateContext();
            var existing = await context.StocktakeSessions.FirstOrDefaultAsync(s => s.Id == session.Id);
            if (existing == null) return false;

            if (existing.Status == "معتمد" || existing.Status == "ملغاة")
            {
                throw new InvalidOperationException("لا يمكن تعديل جلسة معتمدة أو ملغاة.");
            }

            existing.Name = session.Name;
            existing.Notes = session.Notes;

            await context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteSessionAsync(int id)
        {
            using var context = CreateContext();
            var session = await context.StocktakeSessions.FirstOrDefaultAsync(s => s.Id == id);
            if (session == null) return false;

            if (session.Status == "معتمد")
            {
                throw new InvalidOperationException("لا يمكن حذف جلسة معتمدة.");
            }

            context.StocktakeSessions.Remove(session);
            await context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> StartCountingAsync(int sessionId)
        {
            using var context = CreateContext();
            var session = await context.StocktakeSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session == null) return false;

            if (session.Status != "مسودة")
            {
                throw new InvalidOperationException("يمكن بدء العد فقط من حالة المسودة.");
            }

            session.Status = "جاري العد";
            session.StartDate = DateTime.Now;

            await context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SubmitForReviewAsync(int sessionId)
        {
            using var context = CreateContext();
            var session = await context.StocktakeSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session == null) return false;

            if (session.Status != "جاري العد")
            {
                throw new InvalidOperationException("يمكن تقديم للمراجعة فقط من حالة جاري العد.");
            }

            session.Status = "مراجعة";

            await context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ApproveSessionAsync(int sessionId)
        {
            using var context = CreateContext();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                var session = await context.StocktakeSessions
                    .Include(s => s.Lines)
                    .FirstOrDefaultAsync(s => s.Id == sessionId)
                    ?? throw new InvalidOperationException("الجلسة غير موجودة.");

                if (session.Status != "مراجعة")
                {
                    throw new InvalidOperationException("يمكن الاعتماد فقط من حالة المراجعة.");
                }

                // Check for movements after snapshot
                bool hasMovementsAfterSnapshot = await HasMovementsAfterSnapshotAsync(sessionId);
                if (hasMovementsAfterSnapshot)
                {
                    // We warn but proceed with snapshot-based adjustment
                    // This is the safe approach: adjustment is based on snapshot book quantity
                }

                // Process each line with a difference
                foreach (var line in session.Lines.Where(l => l.IsCounted && l.Difference != 0))
                {
                    var product = await context.Products.FirstOrDefaultAsync(p => p.Id == line.ProductId)
                        ?? throw new InvalidOperationException($"الصنف رقم {line.ProductId} غير موجود.");

                    int previousQty = product.Quantity;
                    product.Quantity += line.Difference;

                    if (product.Quantity < 0)
                    {
                        throw new InvalidOperationException($"الصنف '{product.Name}' سيصبح رصيده سالباً بعد التعديل.");
                    }

                    var movement = new InventoryMovementModel
                    {
                        ProductId = product.Id,
                        PartNumber = product.PartNumber,
                        ProductName = product.Name,
                        MovementType = "تعديل جرد",
                        QuantityChange = line.Difference,
                        PreviousQuantity = previousQty,
                        NewQuantity = product.Quantity,
                        MovementDate = DateTime.Now,
                        ReferenceId = session.Id,
                        ReferenceNumber = session.SessionNumber,
                        Notes = $"جرد {session.Name} - التعديل: {line.Difference:+0;-0}"
                    };

                    await context.InventoryMovements.AddAsync(movement);
                }

                session.Status = "معتمد";
                session.ApprovedDate = DateTime.Now;

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                DataEvents.RaiseInventoryOrInvoicesChanged();

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException($"فشل اعتماد جلسة الجرد: {ex.Message}", ex);
            }
        }

        public async Task<bool> CancelSessionAsync(int sessionId, string reason)
        {
            using var context = CreateContext();
            var session = await context.StocktakeSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session == null) return false;

            if (session.Status == "معتمد")
            {
                throw new InvalidOperationException("لا يمكن إلغاء جلسة معتمدة.");
            }

            session.Status = "ملغاة";
            session.CancelledDate = DateTime.Now;
            session.CancellationReason = reason;

            await context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<StocktakeLineModel>> GetLinesAsync(int sessionId)
        {
            using var context = CreateContext();
            return await context.StocktakeLines
                .AsNoTracking()
                .Where(l => l.SessionId == sessionId)
                .OrderBy(l => l.ProductName)
                .ToListAsync();
        }

        public async Task<StocktakeLineModel?> GetLineByProductAsync(int sessionId, int productId)
        {
            using var context = CreateContext();
            return await context.StocktakeLines
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.SessionId == sessionId && l.ProductId == productId);
        }

        public async Task<StocktakeLineModel?> GetLineByBarcodeAsync(int sessionId, string barcode)
        {
            using var context = CreateContext();
            return await context.StocktakeLines
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.SessionId == sessionId && l.Barcode == barcode);
        }

        public async Task<bool> UpdateLineAsync(StocktakeLineModel line)
        {
            using var context = CreateContext();
            var existing = await context.StocktakeLines.FirstOrDefaultAsync(l => l.Id == line.Id);
            if (existing == null) return false;

            var session = await context.StocktakeSessions.FirstOrDefaultAsync(s => s.Id == existing.SessionId);
            if (session == null || session.Status == "معتمد" || session.Status == "ملغاة")
            {
                throw new InvalidOperationException("لا يمكن تعديل بنود جلسة معتمدة أو ملغاة.");
            }

            existing.CountedQuantity = line.CountedQuantity;
            existing.Notes = line.Notes;

            await context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> CountProductAsync(int sessionId, int productId, int countedQuantity, string? notes = null)
        {
            using var context = CreateContext();
            var line = await context.StocktakeLines.FirstOrDefaultAsync(l => l.SessionId == sessionId && l.ProductId == productId);
            if (line == null) return false;

            var session = await context.StocktakeSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session == null || session.Status == "معتمد" || session.Status == "ملغاة")
            {
                throw new InvalidOperationException("لا يمكن العد في جلسة معتمدة أو ملغاة.");
            }

            if (countedQuantity < 0)
            {
                throw new InvalidOperationException("الكمية المعدودة لا يمكن أن تكون سالبة.");
            }

            line.CountedQuantity = countedQuantity;
            line.Notes = notes ?? string.Empty;

            await context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> CountByBarcodeAsync(int sessionId, string barcode, int countedQuantity, string? notes = null)
        {
            using var context = CreateContext();
            var line = await context.StocktakeLines.FirstOrDefaultAsync(l => l.SessionId == sessionId && l.Barcode == barcode);
            if (line == null) return false;

            var session = await context.StocktakeSessions.FirstOrDefaultAsync(s => s.Id == sessionId);
            if (session == null || session.Status == "معتمد" || session.Status == "ملغاة")
            {
                throw new InvalidOperationException("لا يمكن العد في جلسة معتمدة أو ملغاة.");
            }

            if (countedQuantity < 0)
            {
                throw new InvalidOperationException("الكمية المعدودة لا يمكن أن تكون سالبة.");
            }

            line.CountedQuantity = countedQuantity;
            line.Notes = notes ?? string.Empty;

            await context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> HasMovementsAfterSnapshotAsync(int sessionId)
        {
            using var context = CreateContext();
            var session = await context.StocktakeSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == sessionId);

            if (session == null || session.StartDate == null) return false;

            // Check if any inventory movements occurred after the session started
            return await context.InventoryMovements
                .AsNoTracking()
                .AnyAsync(m => m.MovementDate > session.StartDate.Value);
        }

        public async Task<StocktakeSummary> GetSessionSummaryAsync(int sessionId)
        {
            using var context = CreateContext();
            var lines = await context.StocktakeLines
                .AsNoTracking()
                .Where(l => l.SessionId == sessionId)
                .ToListAsync();

            var summary = new StocktakeSummary
            {
                TotalLines = lines.Count,
                CountedLines = lines.Count(l => l.IsCounted),
                UncountedLines = lines.Count(l => !l.IsCounted),
                LinesWithDifference = lines.Count(l => l.IsCounted && l.Difference != 0),
                TotalShortage = lines.Where(l => l.IsCounted && l.Difference < 0).Sum(l => Math.Abs(l.Difference)),
                TotalSurplus = lines.Where(l => l.IsCounted && l.Difference > 0).Sum(l => l.Difference),
                ShortageValue = lines.Where(l => l.IsCounted && l.Difference < 0).Sum(l => Math.Abs(l.Difference) * l.CostPrice),
                SurplusValue = lines.Where(l => l.IsCounted && l.Difference > 0).Sum(l => l.Difference * l.CostPrice)
            };

            return summary;
        }
    }
}
