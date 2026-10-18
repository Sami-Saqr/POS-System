using CarPartManager.Models;

namespace CarPartManager.Services
{
    /// <summary>
    /// واجهة خدمة إدارة قطع الغيار (IProductService).
    /// تفصل طبقة البيانات عن واجهات العرض (UI)، مما يتيح استبدال البيانات التجريبية
    /// بقاعدة بيانات حقيقية (SQLite / Entity Framework Core) لاحقاً دون تعديل شاشات العرض.
    /// </summary>
    public interface IProductService
    {
        Task<IEnumerable<ProductModel>> GetAllProductsAsync();
        Task<ProductModel?> GetByIdAsync(int id);
        Task<ProductModel> AddProductAsync(ProductModel product);
        Task<bool> UpdateProductAsync(ProductModel product);
        Task<bool> DeleteProductAsync(int id);
        Task<IEnumerable<ProductModel>> SearchProductsAsync(string query);
        Task<ProductModel?> GetByBarcodeAsync(string barcode);
        Task<List<ProductModel>> GetBarcodeSuggestionsAsync(string prefix, CancellationToken cancellationToken = default);
    }
}
