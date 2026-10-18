using CarPartManager.MVVM;

namespace CarPartManager.Models
{
    /// <summary>
    /// نموذج بيانات دفعة الدفع المسجلة من العميل (Customer Payment Model)
    /// تمثل سداداً جزئياً أو كاملاً يقلل من رصيد العميل.
    /// </summary>
    public class CustomerPaymentModel : ViewModelBase
    {
        private int _id;
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private int _customerId;
        /// <summary>
        /// معرف العميل المرتبط بهذه الدفعة
        /// </summary>
        public int CustomerId
        {
            get => _customerId;
            set => SetProperty(ref _customerId, value);
        }

        private decimal _amount;
        /// <summary>
        /// مبلغ الدفعة (يجب أن يكون > 0)
        /// </summary>
        public decimal Amount
        {
            get => _amount;
            set => SetProperty(ref _amount, value);
        }

        private DateTime _paymentDate = DateTime.Now;
        /// <summary>
        /// تاريخ تسجيل الدفعة
        /// </summary>
        public DateTime PaymentDate
        {
            get => _paymentDate;
            set => SetProperty(ref _paymentDate, value);
        }

        private string _paymentMethod = "نقدي";
        /// <summary>
        /// طريقة الدفع: نقدي / شبكة / تحويل بنكي
        /// </summary>
        public string PaymentMethod
        {
            get => _paymentMethod;
            set => SetProperty(ref _paymentMethod, value);
        }

        private string _notes = string.Empty;
        /// <summary>
        /// ملاحظات اختيارية على الدفعة
        /// </summary>
        public string Notes
        {
            get => _notes;
            set => SetProperty(ref _notes, value);
        }

        private DateTime _createdDate = DateTime.Now;
        /// <summary>
        /// تاريخ إنشاء السجل
        /// </summary>
        public DateTime CreatedDate
        {
            get => _createdDate;
            set => SetProperty(ref _createdDate, value);
        }
    }
}
