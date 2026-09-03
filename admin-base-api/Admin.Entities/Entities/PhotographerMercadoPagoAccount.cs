namespace Admin.Entities.Entities
{
    public class PhotographerMercadoPagoAccount
    {
        public int Id { get; set; }
        public int PhotographerId { get; set; }
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string PublicKey { get; set; } = string.Empty;
        public string MercadoPagoUserId { get; set; } = string.Empty;
        public DateTime TokenExpiration { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public User Photographer { get; set; } = null!;
    }
}
