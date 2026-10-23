using CarPartManager.Models;

namespace CarPartManager.Services
{
    /// <summary>
    /// واجهة خدمة إدارة الجرد
    /// </summary>
    public interface IStocktakeService
    {
        // Session Management
        Task<StocktakeSessionModel> CreateSessionAsync(string name, string scopeType, string? scopeFilter = null, string? notes = null);
        Task<StocktakeSessionModel?> GetSessionByIdAsync(int id);
        Task<IEnumerable<StocktakeSessionModel>> GetAllSessionsAsync();
        Task<bool> UpdateSessionAsync(StocktakeSessionModel session);
        Task<bool> DeleteSessionAsync(int id);

        // Workflow
        Task<bool> StartCountingAsync(int sessionId);
        Task<bool> SubmitForReviewAsync(int sessionId);
        Task<bool> ApproveSessionAsync(int sessionId);
        Task<bool> CancelSessionAsync(int sessionId, string reason);

        // Lines
        Task<IEnumerable<StocktakeLineModel>> GetLinesAsync(int sessionId);
        Task<StocktakeLineModel?> GetLineByProductAsync(int sessionId, int productId);
        Task<StocktakeLineModel?> GetLineByBarcodeAsync(int sessionId, string barcode);
        Task<bool> UpdateLineAsync(StocktakeLineModel line);
        Task<bool> CountProductAsync(int sessionId, int productId, int countedQuantity, string? notes = null);
        Task<bool> CountByBarcodeAsync(int sessionId, string barcode, int countedQuantity, string? notes = null);

        // Validation
        Task<bool> HasMovementsAfterSnapshotAsync(int sessionId);
        Task<StocktakeSummary> GetSessionSummaryAsync(int sessionId);
    }

    /// <summary>
    /// ملخص جلسة الجرد
    /// </summary>
    public class StocktakeSummary
    {
        public int TotalLines { get; set; }
        public int CountedLines { get; set; }
        public int UncountedLines { get; set; }
        public int LinesWithDifference { get; set; }
        public int TotalShortage { get; set; }
        public int TotalSurplus { get; set; }
        public decimal ShortageValue { get; set; }
        public decimal SurplusValue { get; set; }
    }
}
