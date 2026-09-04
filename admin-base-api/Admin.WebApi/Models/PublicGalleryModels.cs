using Microsoft.AspNetCore.Http;

namespace Admin.WebApi.Models
{
    public class PublicStudioDto
    {
        public int UserId { get; set; }
        public string StudioName { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public SiteThemeDto? Theme { get; set; }
        public List<PublicEventCardDto> Events { get; set; } = new();
    }

    public class PublicEventCardDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime EventDate { get; set; }
        public decimal PricePerPhoto { get; set; }
        public decimal? OriginalPrice { get; set; }
        public string PriceType { get; set; } = "paid";
        public string ProductType { get; set; } = "digital_file";
        public string PaymentMethods { get; set; } = "mercadopago";
        public int PhotoCount { get; set; }
        public int DigitalAssetCount { get; set; }
        public string? CoverPhotoUrl { get; set; }
    }

    public class PublicEventDetailDto
    {
        public int Id { get; set; }
        public string StudioName { get; set; } = string.Empty;
        public string StudioSlug { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime EventDate { get; set; }
        public decimal PricePerPhoto { get; set; }
        public decimal? OriginalPrice { get; set; }
        public string PriceType { get; set; } = "paid";
        public string ProductType { get; set; } = "digital_file";
        public string PaymentMethods { get; set; } = "mercadopago";
        public int DigitalAssetCount { get; set; }
        public string? CoverPhotoUrl { get; set; }
        public List<PublicPhotoDto> Photos { get; set; } = new();
        public int TotalPhotos { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasMore { get; set; }
    }

    public class PublicPhotoDto
    {
        public int Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public string OriginalFileName { get; set; } = string.Empty;
    }

    public class PublicPhotoCheckoutRequest
    {
        public List<int> PhotoIds { get; set; } = new();
        public string BuyerEmail { get; set; } = string.Empty;
        public string? BuyerName { get; set; }
        public string? DiscountCode { get; set; }
    }

    public class PublicTransferReceiptUploadRequest
    {
        public IFormFile? Receipt { get; set; }
    }

    public class PublicPhotoCheckoutResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? CheckoutUrl { get; set; }
        public string? PreferenceId { get; set; }
        public string? ExternalReference { get; set; }
        public string? PaymentMethod { get; set; }
        public PublicTransferPaymentData? TransferData { get; set; }
        public decimal SubtotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public string Currency { get; set; } = "ARS";
    }

    public class PublicTransferPaymentData
    {
        public string HolderName { get; set; } = string.Empty;
        public string BankName { get; set; } = string.Empty;
        public string? Alias { get; set; }
        public string? Cbu { get; set; }
        public string? AccountInfo { get; set; }
        public string Amount { get; set; } = string.Empty;
        public string Currency { get; set; } = "ARS";
        public string Reference { get; set; } = string.Empty;
    }

    public class PublicTransferReceiptResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ExternalReference { get; set; }
        public string? Status { get; set; }
    }

    public class PublicCheckoutStatusResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? ExternalReference { get; set; }
        public string? PaymentStatus { get; set; }
        public string? DeliveryStatus { get; set; }
        public bool IsPaid { get; set; }
        public bool IsDelivered { get; set; }
        public int DeliveryAttempts { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime? LastDeliveryAttemptAt { get; set; }
        public DateTime? DeliverySentAt { get; set; }
    }
}
