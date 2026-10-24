using System.Text.RegularExpressions;

namespace CarPartManager.Services
{
    /// <summary>
    /// مساعد توحيد الأسماء النصية (Name Normalization Helper).
    /// يُستخدم لمقارنة أسماء التصنيفات بمنع التكرار: تقليم المسافات، تقليص المسافات المتعددة،
    /// وتجاهل حالة الأحرف أثناء المقارنة.
    /// </summary>
    public static class NameNormalizer
    {
        /// <summary>
        /// إرجاع الاسم بعد تقليم الأطراف وتقليص المسافات المتعددة إلى مسافة واحدة.
        /// </summary>
        public static string Normalize(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            return Regex.Replace(name.Trim(), @"\s+", " ");
        }

        /// <summary>
        /// مقارنة اسمين مع التوحيد وتجاهل حالة الأحرف.
        /// </summary>
        public static bool AreEquivalent(string? first, string? second)
        {
            return string.Equals(Normalize(first), Normalize(second), StringComparison.OrdinalIgnoreCase);
        }
    }
}
