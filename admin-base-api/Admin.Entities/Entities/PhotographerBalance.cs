namespace Admin.Entities.Entities
{
    public class PhotographerBalance
    {
        public int PhotographerId { get; set; }
        public decimal PendingAmount { get; set; }
        public decimal AvailableAmount { get; set; }
        public decimal TotalWithdrawn { get; set; }

        public User Photographer { get; set; } = null!;
    }
}