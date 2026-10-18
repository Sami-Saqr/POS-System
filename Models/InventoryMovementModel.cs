using CarPartManager.MVVM;

namespace CarPartManager.Models
{
    /// <summary>
    /// يمثل حركة مخزنية مسجلة بدقة في كارت الصنف (Inventory Movement Audit Log)
    /// </summary>
    public class InventoryMovementModel : ViewModelBase
    {
        private int _id;
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public int ProductId { get; set; }
        public string PartNumber { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string MovementType { get; set; } = "شراء وارد";
        public int QuantityChange { get; set; }
        public int PreviousQuantity { get; set; }
        public int NewQuantity { get; set; }
        public DateTime MovementDate { get; set; } = DateTime.Now;
        public int? ReferenceId { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;

        public int PreviousBalance
        {
            get => PreviousQuantity;
            set => PreviousQuantity = value;
        }

        public int NewBalance
        {
            get => NewQuantity;
            set => NewQuantity = value;
        }
    }
}
