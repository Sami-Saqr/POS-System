using Microsoft.EntityFrameworkCore;
using CarPartManager.Data;
using CarPartManager.Models;

namespace CarPartManager.Services
{
    /// <summary>
    /// خدمة إدارة قطع الغيار المعتمدة على قاعدة بيانات SQLite المحلية الحقيقية.
    /// تطبق واجهة IProductService وتفصل بالكامل استعلامات قاعدة البيانات عن شاشات العرض.
    /// </summary>
    public class SqliteProductService : IProductService
    {
        public SqliteProductService()
        {
        }

        private CarPartsDbContext CreateContext() => new CarPartsDbContext();

        public async Task<IEnumerable<ProductModel>> GetAllProductsAsync()
        {
            try
            {
                using var context = CreateContext();
                return await context.Products
                                    .AsNoTracking()
                                    .OrderBy(p => p.Id)
                                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر قراءة الأصناف من قاعدة البيانات المحلية: {ex.Message}", ex);
            }
        }

        public async Task<ProductModel?> GetByIdAsync(int id)
        {
            try
            {
                using var context = CreateContext();
                return await context.Products
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(p => p.Id == id);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر البحث عن الصنف رقم {id}: {ex.Message}", ex);
            }
        }

        public async Task<ProductModel> AddProductAsync(ProductModel product)
        {
            try
            {
                using var context = CreateContext();

                // إنشاء كيان جديد للإضافة لمنع مشاكل تعقب الكائنات
                var entity = new ProductModel
                {
                    PartNumber = product.PartNumber?.Trim() ?? string.Empty,
                    Barcode = product.Barcode?.Trim() ?? string.Empty,
                    Name = product.Name?.Trim() ?? string.Empty,
                    CompatibleVehicles = product.CompatibleVehicles?.Trim() ?? string.Empty,
                    Category = string.IsNullOrWhiteSpace(product.Category) ? "عام" : product.Category.Trim(),
                    PurchasePrice = product.PurchasePrice,
                    SellingPrice = product.SellingPrice,
                    Quantity = product.Quantity,
                    MinimumStock = product.MinimumStock,
                    Notes = product.Notes?.Trim() ?? string.Empty
                };

                // التحقق من فرادة الباركود إن تم إدخاله
                if (!string.IsNullOrWhiteSpace(entity.Barcode))
                {
                    var isDuplicate = await context.Products
                        .AsNoTracking()
                        .AnyAsync(p => p.Barcode == entity.Barcode);
                    if (isDuplicate)
                    {
                        throw new InvalidOperationException($"رقم الباركود '{entity.Barcode}' مسجل مسبقاً لصنف آخر. يجب أن يكون الباركود فريداً لكل صنف.");
                    }
                }

                await context.Products.AddAsync(entity);
                await context.SaveChangesAsync();

                // تحديث الـ ID في الكائن الأصلي
                product.Id = entity.Id;
                return product;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل حفظ قطعة الغيار في قاعدة البيانات: {ex.Message}", ex);
            }
        }

        public async Task<bool> UpdateProductAsync(ProductModel product)
        {
            try
            {
                using var context = CreateContext();
                using var transaction = await context.Database.BeginTransactionAsync();

                var existing = await context.Products.FirstOrDefaultAsync(p => p.Id == product.Id);
                if (existing == null)
                {
                    return false;
                }

                var cleanBarcode = product.Barcode?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(cleanBarcode))
                {
                    var isDuplicate = await context.Products
                        .AsNoTracking()
                        .AnyAsync(p => p.Id != product.Id && p.Barcode == cleanBarcode);
                    if (isDuplicate)
                    {
                        throw new InvalidOperationException($"رقم الباركود '{cleanBarcode}' مسجل مسبقاً لصنف آخر. يجب أن يكون الباركود فريداً لكل صنف.");
                    }
                }

                // Check if quantity changed
                int quantityChange = product.Quantity - existing.Quantity;
                int previousQty = existing.Quantity;

                existing.PartNumber = product.PartNumber?.Trim() ?? string.Empty;
                existing.Barcode = cleanBarcode;
                existing.Name = product.Name?.Trim() ?? string.Empty;
                existing.CompatibleVehicles = product.CompatibleVehicles?.Trim() ?? string.Empty;
                existing.Category = string.IsNullOrWhiteSpace(product.Category) ? "عام" : product.Category.Trim();
                existing.PurchasePrice = product.PurchasePrice;
                existing.SellingPrice = product.SellingPrice;
                existing.Quantity = product.Quantity;
                existing.MinimumStock = product.MinimumStock;
                existing.Notes = product.Notes?.Trim() ?? string.Empty;

                // Record movement if quantity changed
                if (quantityChange != 0)
                {
                    var movement = new InventoryMovementModel
                    {
                        ProductId = existing.Id,
                        PartNumber = existing.PartNumber,
                        ProductName = existing.Name,
                        MovementType = "تعديل يدوي (تعديل الصنف)",
                        QuantityChange = quantityChange,
                        PreviousQuantity = previousQty,
                        NewQuantity = existing.Quantity,
                        MovementDate = DateTime.Now,
                        Notes = "تعديل مباشر لبيانات الصنف من شاشة الأصناف"
                    };

                    await context.InventoryMovements.AddAsync(movement);
                }

                await context.SaveChangesAsync();
                await transaction.CommitAsync();

                if (quantityChange != 0)
                {
                    DataEvents.RaiseInventoryOrInvoicesChanged();
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل تحديث بيانات الصنف في قاعدة البيانات: {ex.Message}", ex);
            }
        }

        public async Task<bool> DeleteProductAsync(int id)
        {
            try
            {
                using var context = CreateContext();
                var existing = await context.Products.FirstOrDefaultAsync(p => p.Id == id);
                if (existing == null)
                {
                    return false;
                }

                context.Products.Remove(existing);
                await context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل حذف الصنف من قاعدة البيانات: {ex.Message}", ex);
            }
        }

        public async Task<IEnumerable<ProductModel>> SearchProductsAsync(string query)
        {
            try
            {
                using var context = CreateContext();
                if (string.IsNullOrWhiteSpace(query))
                {
                    return await context.Products
                                        .AsNoTracking()
                                        .OrderBy(p => p.Id)
                                        .ToListAsync();
                }

                var q = query.Trim().ToLower();
                return await context.Products
                                    .AsNoTracking()
                                    .Where(p =>
                                        p.Name.ToLower().Contains(q) ||
                                        p.PartNumber.ToLower().Contains(q) ||
                                        p.Barcode.ToLower().Contains(q) ||
                                        p.CompatibleVehicles.ToLower().Contains(q) ||
                                        p.Category.ToLower().Contains(q)
                                    )
                                    .OrderBy(p => p.Id)
                                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر تنفيذ عملية البحث في قاعدة البيانات: {ex.Message}", ex);
            }
        }

        public async Task<ProductModel?> GetByBarcodeAsync(string barcode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(barcode))
                    return null;

                var code = barcode.Trim();
                using var context = CreateContext();
                return await context.Products
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(p => p.Barcode == code);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر البحث عن الصنف بالباركود: {ex.Message}", ex);
            }
        }

        public async Task<List<ProductModel>> GetBarcodeSuggestionsAsync(string prefix, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                return new List<ProductModel>();

            var clean = prefix.Trim().Replace("%", "").Replace("_", "");
            if (clean.Length < 2)
                return new List<ProductModel>();

            try
            {
                using var context = CreateContext();

                // 1. الأولوية الأولى: الباركود الذي يبدأ بالنص المكتوب (Prefix match)
                var startsWithResults = await context.Products
                    .AsNoTracking()
                    .Where(p => p.Barcode != null && p.Barcode != "" && EF.Functions.Like(p.Barcode, $"{clean}%"))
                    .OrderBy(p => p.Barcode)
                    .Take(8)
                    .ToListAsync(cancellationToken);

                if (startsWithResults.Count >= 8)
                {
                    return startsWithResults;
                }

                // 2. الأولوية الثانية: الباركود الذي يحتوي على النص المكتوب دون أن يبدأ به (Infix match)
                int needed = 8 - startsWithResults.Count;
                var containsResults = await context.Products
                    .AsNoTracking()
                    .Where(p => p.Barcode != null && p.Barcode != "" &&
                                EF.Functions.Like(p.Barcode, $"%{clean}%") &&
                                !EF.Functions.Like(p.Barcode, $"{clean}%"))
                    .OrderBy(p => p.Barcode)
                    .Take(needed)
                    .ToListAsync(cancellationToken);

                startsWithResults.AddRange(containsResults);
                return startsWithResults;
            }
            catch (OperationCanceledException)
            {
                return new List<ProductModel>();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر البحث عن اقتراحات الباركود: {ex.Message}", ex);
            }
        }
    }
}
