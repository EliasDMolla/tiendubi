namespace Admin.Entities.Entities
{
    public class PhotoCheckoutSession
    {
        public int Id { get; set; }
        public string ExternalReference { get; set; } = string.Empty;
        public int PhotographerId { get; set; }
        public int EventId { get; set; }
        public string PhotoIdsCsv { get; set; } = string.Empty;
        public string BuyerEmail { get; set; } = string.Empty;
        public string? BuyerName { get; set; }
        public string? DiscountCode { get; set; }
        public decimal SubtotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string? PreferenceId { get; set; }
        public long? MerchantOrderId { get; set; }
        public string Status { get; set; } = "Created";
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public string DeliveryEmailStatus { get; set; } = "NotSent";
        public DateTime? DeliveryEmailLastAttemptAt { get; set; }
        public DateTime? DeliveryEmailSentAt { get; set; }
        public int DeliveryEmailAttempts { get; set; }
        public string? DeliveryEmailError { get; set; }

        public User Photographer { get; set; } = null!;
        public PhotographerEvent Event { get; set; } = null!;
    }
}
