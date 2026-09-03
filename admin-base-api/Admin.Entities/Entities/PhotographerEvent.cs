namespace Admin.Entities.Entities
{
    public class PhotographerEvent : Audit
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime EventDate { get; set; }
        public decimal PricePerPhoto { get; set; }
        public decimal? OriginalPrice { get; set; }
        public string PriceType { get; set; } = "paid";
        public string ProductType { get; set; } = "digital_file";
        public string PaymentMethods { get; set; } = "mercadopago";
        public string? BuyerInstructions { get; set; }
        public string? DeliveryLink { get; set; }
        public string? CoverImagePath { get; set; }
        public bool IsPublished { get; set; } = false;

        public User User { get; set; } = null!;
        public ICollection<EventPhoto> Photos { get; set; } = new List<EventPhoto>();
        public ICollection<ProductAsset> ProductAssets { get; set; } = new List<ProductAsset>();
        public ICollection<PhotoSale> Sales { get; set; } = new List<PhotoSale>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<PhotoCheckoutSession> PhotoCheckoutSessions { get; set; } = new List<PhotoCheckoutSession>();
    }
}
