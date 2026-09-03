namespace Admin.Entities.Entities
{
    public class Order : Audit
    {
        public int Id { get; set; }
        public int PhotographerId { get; set; }
        public int EventId { get; set; }
        public int? PhotoId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PlatformCommission { get; set; }
        public decimal MercadoPagoFee { get; set; }
        public decimal PhotographerNet { get; set; }
        public string Status { get; set; } = "Paid";
        public DateTime ClearedAt { get; set; }
        public DateTime? PaidOutAt { get; set; }

        public User Photographer { get; set; } = null!;
        public PhotographerEvent Event { get; set; } = null!;
        public EventPhoto? Photo { get; set; }
    }
}