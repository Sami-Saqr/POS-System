using CarPartManager.Models;

namespace CarPartManager.Services
{
    /// <summary>
    /// واجهة خدمة إدارة العملاء ودفعاتهم (ICustomerService)
    /// </summary>
    public interface ICustomerService
    {
        // ── قراءة العملاء ──────────────────────────────────────────────────────
        Task<IEnumerable<CustomerModel>> GetAllCustomersAsync();
        Task<CustomerModel?> GetByIdAsync(int id);
        Task<IEnumerable<CustomerModel>> SearchCustomersAsync(string query);

        // ── إضافة وتعديل وتعطيل ───────────────────────────────────────────────
        Task<CustomerModel> AddCustomerAsync(CustomerModel customer);
        Task<bool> UpdateCustomerAsync(CustomerModel customer);

        /// <summary>
        /// تعطيل أو تفعيل العميل بدلاً من الحذف الفعلي
        /// </summary>
        Task<bool> SetActiveStatusAsync(int customerId, bool isActive);

        // ── الرصيد والكشف ──────────────────────────────────────────────────────
        /// <summary>
        /// حساب الرصيد الحالي المستحق على العميل من السجلات
        /// </summary>
        Task<decimal> CalculateBalanceAsync(int customerId);

        /// <summary>
        /// استرجاع كشف حساب العميل في نطاق تاريخي
        /// </summary>
        Task<IEnumerable<CustomerStatementLineModel>> GetStatementAsync(
            int customerId, DateTime? from = null, DateTime? to = null);

        // ── الدفعات ────────────────────────────────────────────────────────────
        Task<IEnumerable<CustomerPaymentModel>> GetPaymentsAsync(int customerId);
        Task<CustomerPaymentModel> AddPaymentAsync(CustomerPaymentModel payment);

        /// <summary>
        /// إلغاء دفعة مسجلة بالخطأ مع توثيق السبب والتاريخ دون حذف السجل المالي
        /// </summary>
        Task<bool> CancelPaymentAsync(int paymentId, string reason);

        // ── التحقق من الهاتف ───────────────────────────────────────────────────
        Task<bool> PhoneExistsAsync(string phone, int excludeCustomerId = 0);
        Task<CustomerModel?> FindCustomerByPhoneAsync(string phone, int excludeCustomerId = 0);

        // ── اقتراحات سريعة لنقطة البيع (POS) ──────────────────────────────────
        Task<IEnumerable<CustomerModel>> GetCustomerSuggestionsAsync(string query, CancellationToken cancellationToken = default);
    }
}
