namespace CarPartManager.Services
{
    /// <summary>
    /// وسيط أحداث النظام لإشعار الشاشات المختلفة بحدوث تغييرات في قاعدة البيانات
    /// لتحديث المؤشرات والبيانات تلقائياً دون الحاجة إلى مؤقتات فحص دورية (Decoupled Event Aggregator)
    /// </summary>
    public static class DataEvents
    {
        public static event Action? InventoryOrInvoicesChanged;

        public static void RaiseInventoryOrInvoicesChanged()
        {
            try
            {
                InventoryOrInvoicesChanged?.Invoke();
            }
            catch
            {
                // منع تعطل أي استدعاء عند حدوث خطأ غير متوقع في معالجي الأحداث
            }
        }
    }
}
