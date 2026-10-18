using CarPartManager.Models;

namespace CarPartManager.Services
{
    /// <summary>
    /// واجهة خدمة فواتير المشتريات والربط التلقائي بالمخزون
    /// </summary>
    public interface IPurchaseInvoiceService
    {
        Task<string> GenerateNextInvoiceNumberAsync();
        Task<string> GenerateInvoiceNumberAsync();
        Task<IEnumerable<PurchaseInvoiceModel>> GetAllInvoicesAsync();
        Task<PurchaseInvoiceModel?> GetInvoiceByIdAsync(int id);
        Task<PurchaseInvoiceModel> SaveDraftInvoiceAsync(PurchaseInvoiceModel invoice);
        Task<PurchaseInvoiceModel> PostInvoiceAsync(PurchaseInvoiceModel invoice);
        Task<PurchaseInvoiceModel> PostInvoiceAsync(int invoiceId);
        Task<IEnumerable<PurchaseInvoiceModel>> SearchInvoicesAsync(string query);
        Task<bool> CancelInvoiceAsync(int invoiceId, string reason);
        Task<IEnumerable<InventoryMovementModel>> GetProductMovementsAsync(int productId);
    }
}
