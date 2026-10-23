using System.Text;
using System.Text.RegularExpressions;

namespace CarPartManager.Services
{
    /// <summary>
    /// فئة مساعدة موحدة لتوحيد وتدقيق أرقام الهواتف (Phone Normalization Helper).
    /// تعالج الأرقام العربية المشرقية، والمسافات والشرطات والبادئات (+20, 0020, 0)،
    /// ومصممة لإعادة الاستخدام في نقطة البيع (POS) ووحدة العملاء.
    /// </summary>
    public static class PhoneHelper
    {
        private static readonly Dictionary<char, char> EasternToArabicNumerals = new()
        {
            ['٠'] = '0', ['١'] = '1', ['٢'] = '2', ['٣'] = '3', ['٤'] = '4',
            ['٥'] = '5', ['٦'] = '6', ['٧'] = '7', ['٨'] = '8', ['٩'] = '9',
            ['۰'] = '0', ['۱'] = '1', ['۲'] = '2', ['۳'] = '3', ['۴'] = '4',
            ['۵'] = '5', ['۶'] = '6', ['۷'] = '7', ['۸'] = '8', ['۹'] = '9'
        };

        /// <summary>
        /// تحويل الأرقام المشرقية/الفارسية إلى أرقام غربية لاتينية قياسية (0-9)
        /// </summary>
        public static string NormalizeDigits(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            var sb = new StringBuilder(input.Length);
            foreach (var ch in input)
            {
                if (EasternToArabicNumerals.TryGetValue(ch, out var latinDigit))
                {
                    sb.Append(latinDigit);
                }
                else
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// توحيد رقم الهاتف (Normalization):
        /// 1. تحويل الأرقام المشرقية إلى لاتينية.
        /// 2. حذف المسافات والشرطات والأقواس ورموز زائد.
        /// 3. توحيد بادئات مصر والدول (+20 / 0020 / 20 / 0).
        /// </summary>
        public static string Normalize(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

            var latin = NormalizeDigits(phone.Trim());

            // إزالة كافة الأحرف غير الرقمية
            var digits = Regex.Replace(latin, @"[^\d]", "");
            if (string.IsNullOrEmpty(digits)) return string.Empty;

            // توحيد البادئة المصرية الدولية:
            // 002010... -> 010...
            if (digits.StartsWith("0020") && digits.Length >= 12)
            {
                digits = "0" + digits[4..];
            }
            // 2010... -> 010... (إذا كان الطول 12 ويبدأ بـ 20)
            else if (digits.StartsWith("20") && digits.Length == 12)
            {
                digits = "0" + digits[2..];
            }
            // إذا كان 10 أرقام ويبدأ بـ 10 أو 11 أو 12 أو 15 (موبايل مصري بدون 0)، نضيف 0
            else if (digits.Length == 10 && (digits.StartsWith("10") || digits.StartsWith("11") || digits.StartsWith("12") || digits.StartsWith("15")))
            {
                digits = "0" + digits;
            }

            return digits;
        }

        /// <summary>
        /// التحقق من صحة رقم الهاتف بعد التوحيد (7 إلى 15 رقماً)
        /// </summary>
        public static bool IsValid(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return true; // اختياري

            var normalized = Normalize(phone);
            return normalized.Length >= 7 && normalized.Length <= 15 && normalized.All(char.IsAsciiDigit);
        }
    }
}
