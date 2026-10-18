using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CarPartManager.Models;

namespace CarPartManager.Services
{
    public interface IReturnsService
    {
        Task<string> GenerateSalesReturnNumberAsync();
        Task<string> GeneratePurchaseReturnNumberAsync();
        
        Task<List<SalesReturnModel>> GetAllSalesReturnsAsync();
        Task<List<PurchaseReturnModel>> GetAllPurchaseReturnsAsync();
        
        Task<Dictionary<int, int>> GetSalesInvoiceReturnedQuantitiesAsync(int salesInvoiceId);
        Task<Dictionary<int, int>> GetPurchaseInvoiceReturnedQuantitiesAsync(int purchaseInvoiceId);
        
        Task<List<SalesInvoiceModel>> GetEligibleSalesInvoicesAsync();
        Task<List<PurchaseInvoiceModel>> GetEligiblePurchaseInvoicesAsync();
        
        Task<SalesReturnModel> ProcessSalesReturnAsync(SalesReturnModel returnModel);
        Task<PurchaseReturnModel> ProcessPurchaseReturnAsync(PurchaseReturnModel returnModel);
    }
}
