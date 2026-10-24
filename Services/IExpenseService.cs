using CarPartManager.Models;

namespace CarPartManager.Services
{
    /// <summary>
    /// واجهة خدمة إدارة المصروفات وتصنيفاتها (IExpenseService).
    /// جميع الاستعلامات تُنفَّذ على قاعدة البيانات (مفلترة) وبوضع القراءة فقط AsNoTracking.
    /// المصروفات الملغاة لا تدخل في أي إجمالي، ولا يتم حذف أي مصروف أو تصنيف نهائياً.
    /// </summary>
    public interface IExpenseService
    {
        // ── المصروفات ──────────────────────────────────────────────────────────

        Task<IEnumerable<ExpenseModel>> GetExpensesAsync(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? categoryId = null,
            string? paymentMethod = null,
            bool includeCancelled = false,
            string? searchText = null);

        Task<ExpenseModel?> GetExpenseByIdAsync(int id);

        Task<ExpenseSummaryModel> GetSummaryAsync();

        Task<ExpenseModel> AddExpenseAsync(ExpenseModel expense);

        Task<bool> UpdateExpenseAsync(ExpenseModel expense);

        Task<bool> CancelExpenseAsync(int expenseId, string reason, string? cancelledBy = null);

        Task<IEnumerable<ExpenseHistoryModel>> GetExpenseHistoryAsync(int expenseId);

        // ── التصنيفات ──────────────────────────────────────────────────────────

        Task<IEnumerable<ExpenseCategoryModel>> GetCategoriesAsync(bool includeInactive = true);

        Task<ExpenseCategoryModel> AddCategoryAsync(string name);

        Task<bool> SetCategoryActiveAsync(int categoryId, bool isActive);

        Task<bool> IsCategoryNameTakenAsync(string name, int? excludeCategoryId = null);

        Task<int> GetCategoryUsageCountAsync(int categoryId);
    }
}
