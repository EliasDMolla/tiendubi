namespace Admin.Entities.Entities
{
    public class PhotoSale : Audit
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int PhotographerEventId { get; set; }
        public int Quantity { get; set; }
        public decimal TotalAmount { get; set; }
        public string BuyerName { get; set; } = string.Empty;
        public string? BuyerEmail { get; set; }
        public string PaymentMethod { get; set; } = "manual";
        public string Status { get; set; } = "paid";
        public DateTime SoldAt { get; set; }

        public User User { get; set; } = null!;
        public PhotographerEvent PhotographerEvent { get; set; } = null!;
    }
}