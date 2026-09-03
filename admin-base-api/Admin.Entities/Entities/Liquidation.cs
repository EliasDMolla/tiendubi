namespace Admin.Entities.Entities
{
    public class Liquidation
    {
        public int Id { get; set; }
        public int PhotographerId { get; set; }
        public decimal Amount { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public DateTime CreatedAt { get; set; }

        public User Photographer { get; set; } = null!;
    }
}
