using CarPartManager.Models;

namespace CarPartManager.Services
{
    /// <summary>
    /// واجهة خدمة فواتير المبيعات ونقاط البيع وخصم المخزون
    /// </summary>
    public interface ISalesInvoiceService
    {
        Task<string> GenerateNextInvoiceNumberAsync();
        Task<string> GenerateInvoiceNumberAsync();
        Task<IEnumerable<SalesInvoiceModel>> GetAllInvoicesAsync();
        Task<SalesInvoiceModel?> GetInvoiceByIdAsync(int id);
        Task<SalesInvoiceModel> SaveDraftInvoiceAsync(SalesInvoiceModel invoice);
        Task<SalesInvoiceModel> PostInvoiceAsync(SalesInvoiceModel invoice);
        Task<SalesInvoiceModel> PostInvoiceAsync(int invoiceId);
        Task<IEnumerable<SalesInvoiceModel>> SearchInvoicesAsync(string query);
        Task<bool> CancelInvoiceAsync(int invoiceId, string reason);
    }
}
