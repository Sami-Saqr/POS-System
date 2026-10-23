namespace CarPartManager.Models
{
    /// <summary>
    /// سطر في كشف حساب العميل — يمثل إما فاتورة بيع أو دفعة، مع رصيد تراكمي.
    /// هذا النموذج للعرض فقط (ViewModel projection) ولا يُحفظ في قاعدة البيانات.
    /// </summary>
    public class CustomerStatementLineModel
    {
        /// <summary>
        /// تاريخ الحركة
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// نوع الحركة: "فاتورة بيع" أو "دفعة استلام"
        /// </summary>
        public string EntryType { get; set; } = string.Empty;

        /// <summary>
        /// رقم مرجعي: رقم الفاتورة أو رقم تسلسلي للدفعة
        /// </summary>
        public string Reference { get; set; } = string.Empty;

        /// <summary>
        /// وصف الحركة
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// مبلغ مدين (فاتورة بيع — تزيد الرصيد)
        /// </summary>
        public decimal DebitAmount { get; set; }

        /// <summary>
        /// مبلغ دائن (دفعة — تخفض الرصيد)
        /// </summary>
        public decimal CreditAmount { get; set; }

        /// <summary>
        /// الرصيد التراكمي بعد هذه الحركة
        /// </summary>
        public decimal RunningBalance { get; set; }

        /// <summary>
        /// لون عرض الرصيد: أحمر إذا > 0، أخضر إذا = 0
        /// </summary>
        public string BalanceColor => RunningBalance > 0 ? "#DC2626" : "#059669";

        /// <summary>
        /// لون خلفية نوع الحركة
        /// </summary>
        public string TypeBackground => EntryType == "دفعة استلام" ? "#DCFCE7" : "#EEF2FF";
        public string TypeColor => EntryType == "دفعة استلام" ? "#166534" : "#3730A3";

        /// <summary>
        /// معرف السجل الأصلي (Id الفاتورة أو الدفعة)
        /// </summary>
        public int SourceId { get; set; }

        /// <summary>
        /// هل هذا السطر دفعة (لتمكين خيار التفاصيل أو إلغاء الدفعة)؟
        /// </summary>
        public bool IsPayment => EntryType == "دفعة استلام" || EntryType == "دفعة ملغاة";

        /// <summary>
        /// تفاصيل فاتورة المبيعات (إجمالي الفاتورة، المدفوع عند البيع، المتبقي)
        /// </summary>
        public decimal? InvoiceTotal { get; set; }
        public decimal? InvoicePaid { get; set; }
        public decimal? InvoiceRemaining { get; set; }

        /// <summary>
        /// هل هذه دفعة ملغاة؟
        /// </summary>
        public bool IsCancelledPayment { get; set; }
        public string? CancellationReason { get; set; }
    }
}
