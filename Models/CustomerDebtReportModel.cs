namespace CarPartManager.Models
{
    /// <summary>صف عميل في تقرير المديونيات (الحالة الحالية)</summary>
    public class CustomerDebtRow
    {
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        /// <summary>الرصيد المستحق — محسوب بنفس صيغة شاشة العملاء</summary>
        public decimal Balance { get; set; }

        public DateTime? LastPaymentDate { get; set; }

        /// <summary>أقدم فاتورة معتمدة عليها مبلغ غير مسدد</summary>
        public DateTime? OldestUnpaidInvoiceDate { get; set; }

        public string LastPaymentDateText => LastPaymentDate.HasValue ? LastPaymentDate.Value.ToString("yyyy-MM-dd") : "لا توجد دفعات";
        public string OldestUnpaidInvoiceDateText => OldestUnpaidInvoiceDate.HasValue ? OldestUnpaidInvoiceDate.Value.ToString("yyyy-MM-dd") : "لا توجد فواتير غير مسددة";
    }

    /// <summary>
    /// مديونيات العملاء — الحالة الحالية، لا يتأثر بالمرشّح التاريخي.
    /// الرصيد يُحسب بإعادة استخدام خدمة العملاء (نفس صيغة شاشة العملاء) وليس بإعادة تنفيذ الصيغة.
    /// </summary>
    public class CustomerDebtReportModel
    {
        public decimal TotalDebt { get; set; }

        /// <summary>عدد العملاء عليهم مديونية</summary>
        public int CustomerCount { get; set; }

        /// <summary>إجمالي عدد العملاء المسجلين</summary>
        public int TotalCustomers { get; set; }

        public List<CustomerDebtRow> Rows { get; set; } = new();

        /// <summary>توضيح الصيغة المستخدمة في حساب الرصيد</summary>
        public string FormulaNote =>
            "المديونيات تدعم البيع الآجل والدفعات الجزئية. الرصيد محسوب بنفس صيغة شاشة العملاء: " +
            "الرصيد الافتتاحي + مجموع (إجمالي الفاتورة المعتمدة − المدفوع) − الدفعات المسجلة غير الملغاة (بحد أدنى صفر).";
    }
}
