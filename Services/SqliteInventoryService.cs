using Microsoft.EntityFrameworkCore;
using CarPartManager.Data;
using CarPartManager.Models;

namespace CarPartManager.Services
{
    /// <summary>
    /// خدمة إدارة المخزون المعتمدة على SQLite
    /// </summary>
    public class SqliteInventoryService : IInventoryService
    {
        private CarPartsDbContext CreateContext() => new CarPartsDbContext();

        public async Task<InventorySummary> GetInventorySummaryAsync()
        {
            using var context = CreateContext();
            var products = await context.Products.AsNoTracking().ToListAsync();

            return new InventorySummary
            {
                ProductCount = products.Count,
                TotalStockValue = products.Sum(p => p.PurchasePrice * p.Quantity),
                LowStockCount = products.Count(p => p.Quantity > 0 && p.Quantity <= p.MinimumStock),
                OutOfStockCount = products.Count(p => p.Quantity <= 0)
            };
        }

        public async Task<IEnumerable<ProductStockInfo>> GetProductStockInfoAsync(string? searchQuery = null, StockFilter filter = StockFilter.All)
        {
            using var context = CreateContext();
            var query = context.Products.AsNoTracking().AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                var q = searchQuery.Trim().ToLower();
                query = query.Where(p =>
                    p.Name.ToLower().Contains(q) ||
                    p.PartNumber.ToLower().Contains(q) ||
                    (p.Barcode != null && p.Barcode.ToLower().Contains(q)));
            }

            // Apply status filter
            query = filter switch
            {
                StockFilter.LowStock => query.Where(p => p.Quantity > 0 && p.Quantity <= p.MinimumStock),
                StockFilter.OutOfStock => query.Where(p => p.Quantity <= 0),
                _ => query
            };

            var products = await query
                .Select(p => new ProductStockInfo
                {
                    Id = p.Id,
                    Name = p.Name,
                    Barcode = p.Barcode ?? string.Empty,
                    Category = p.Category,
                    Quantity = p.Quantity,
                    MinimumStock = p.MinimumStock,
                    CostPrice = p.PurchasePrice
                })
                .OrderBy(p => p.Name)
                .ToListAsync();

            return products;
        }

        public async Task<IEnumerable<InventoryMovementModel>> GetMovementsAsync(DateTime? fromDate = null, DateTime? toDate = null, int? productId = null, string? movementType = null)
        {
            using var context = CreateContext();
            var query = context.InventoryMovements.AsNoTracking().AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(m => m.MovementDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                var endDate = toDate.Value.Date.AddDays(1);
                query = query.Where(m => m.MovementDate < endDate);
            }

            if (productId.HasValue)
            {
                query = query.Where(m => m.ProductId == productId.Value);
            }

            if (!string.IsNullOrWhiteSpace(movementType))
            {
                query = query.Where(m => m.MovementType == movementType);
            }

            return await query
                .OrderByDescending(m => m.MovementDate)
                .ThenByDescending(m => m.Id)
                .ToListAsync();
        }

        public async Task<IEnumerable<string>> GetMovementTypesAsync()
        {
            using var context = CreateContext();
            return await context.InventoryMovements
                .AsNoTracking()
                .Select(m => m.MovementType)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();
        }

        public async Task<InventoryMovementModel> RecordManualAdjustmentAsync(int productId, int quantityChange, string reason, string? note = null)
        {
            if (quantityChange == 0)
            {
                throw new InvalidOperationException("يجب أن يكون تغيير الكمية مختلفاً عن الصفر.");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new InvalidOperationException("يجب تحديد سبب التعديل اليدوي.");
            }

            using var context = CreateContext();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                var product = await context.Products.FirstOrDefaultAsync(p => p.Id == productId)
                    ?? throw new InvalidOperationException($"الصنف رقم {productId} غير موجود.");

                int previousQty = product.Quantity;
                product.Quantity += quantityChange;

                if (product.Quantity < 0)
                {
                    throw new InvalidOperationException("لا يمكن أن يصبح رصيد الصنف سالباً.");
                }

                var movement = new InventoryMovementModel
                {
                    ProductId = product.Id,
                    PartNumber = product.PartNumber,
                    ProductName = product.Name,
                    MovementType = $"تعديل يدوي ({reason})",
                    QuantityChange = quantityChange,
                    PreviousQuantity = previousQty,
                    NewQuantity = product.Quantity,
                    MovementDate = DateTime.Now,
                    Notes = string.IsNullOrWhiteSpace(note) ? reason : $"{reason}. {note}"
                };

                await context.InventoryMovements.AddAsync(movement);
                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                DataEvents.RaiseInventoryOrInvoicesChanged();

                return movement;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new InvalidOperationException($"فشل تسجيل التعديل اليدوي: {ex.Message}", ex);
            }
        }

        public async Task<IEnumerable<StockMismatch>> CheckConsistencyAsync()
        {
            using var context = CreateContext();
            var products = await context.Products.AsNoTracking().ToListAsync();
            var mismatches = new List<StockMismatch>();

            foreach (var product in products)
            {
                var movementSum = await context.InventoryMovements
                    .AsNoTracking()
                    .Where(m => m.ProductId == product.Id)
                    .SumAsync(m => m.QuantityChange);

                // Calculate expected quantity from movements
                // Note: If there are no movements, expected is 0 (new products should have opening balance)
                int expectedQuantity = movementSum;

                if (product.Quantity != expectedQuantity)
                {
                    mismatches.Add(new StockMismatch
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        PartNumber = product.PartNumber,
                        ExpectedQuantity = expectedQuantity,
                        ActualQuantity = product.Quantity
                    });
                }
            }

            return mismatches;
        }
    }
}
