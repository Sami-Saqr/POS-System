using Microsoft.EntityFrameworkCore;
using CarPartManager.Data;
using CarPartManager.Models;

namespace CarPartManager.Services
{
    /// <summary>
    /// خدمة إدارة الموردين المعتمدة على SQLite
    /// </summary>
    public class SqliteSupplierService : ISupplierService
    {
        private CarPartsDbContext CreateContext() => new CarPartsDbContext();

        public async Task<IEnumerable<SupplierModel>> GetAllSuppliersAsync()
        {
            try
            {
                using var context = CreateContext();
                return await context.Suppliers
                                    .AsNoTracking()
                                    .OrderBy(s => s.Name)
                                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر تحميل قائمة الموردين من قاعدة البيانات: {ex.Message}", ex);
            }
        }

        public async Task<SupplierModel?> GetByIdAsync(int id)
        {
            try
            {
                using var context = CreateContext();
                return await context.Suppliers
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync(s => s.Id == id);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"تعذر تحميل بيانات المورد رقم {id}: {ex.Message}", ex);
            }
        }

        public async Task<SupplierModel> AddSupplierAsync(SupplierModel supplier)
        {
            try
            {
                using var context = CreateContext();
                var entity = new SupplierModel
                {
                    Name = supplier.Name?.Trim() ?? string.Empty,
                    Phone = supplier.Phone?.Trim() ?? string.Empty,
                    Address = supplier.Address?.Trim() ?? string.Empty,
                    CompanyName = supplier.CompanyName?.Trim() ?? string.Empty,
                    Notes = supplier.Notes?.Trim() ?? string.Empty,
                    IsActive = supplier.IsActive
                };

                await context.Suppliers.AddAsync(entity);
                await context.SaveChangesAsync();

                supplier.Id = entity.Id;
                return supplier;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل حفظ المورد في قاعدة البيانات: {ex.Message}", ex);
            }
        }

        public async Task<bool> UpdateSupplierAsync(SupplierModel supplier)
        {
            try
            {
                using var context = CreateContext();
                var existing = await context.Suppliers.FirstOrDefaultAsync(s => s.Id == supplier.Id);
                if (existing == null) return false;

                existing.Name = supplier.Name?.Trim() ?? string.Empty;
                existing.Phone = supplier.Phone?.Trim() ?? string.Empty;
                existing.Address = supplier.Address?.Trim() ?? string.Empty;
                existing.CompanyName = supplier.CompanyName?.Trim() ?? string.Empty;
                existing.Notes = supplier.Notes?.Trim() ?? string.Empty;
                existing.IsActive = supplier.IsActive;

                await context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل تحديث بيانات المورد: {ex.Message}", ex);
            }
        }

        public async Task<bool> DeleteSupplierAsync(int id)
        {
            try
            {
                using var context = CreateContext();
                var existing = await context.Suppliers.FirstOrDefaultAsync(s => s.Id == id);
                if (existing == null) return false;

                context.Suppliers.Remove(existing);
                await context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل حذف المورد: {ex.Message}", ex);
            }
        }

        public async Task<IEnumerable<SupplierModel>> SearchSuppliersAsync(string query)
        {
            try
            {
                using var context = CreateContext();
                if (string.IsNullOrWhiteSpace(query))
                {
                    return await context.Suppliers.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
                }

                var q = query.Trim().ToLower();
                return await context.Suppliers
                                    .AsNoTracking()
                                    .Where(s =>
                                        s.Name.ToLower().Contains(q) ||
                                        s.Phone.ToLower().Contains(q) ||
                                        s.CompanyName.ToLower().Contains(q) ||
                                        s.Address.ToLower().Contains(q)
                                    )
                                    .OrderBy(s => s.Name)
                                    .ToListAsync();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"فشل البحث عن الموردين: {ex.Message}", ex);
            }
        }
    }
}
