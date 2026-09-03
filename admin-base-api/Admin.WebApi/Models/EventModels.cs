using Microsoft.AspNetCore.Http;

namespace Admin.WebApi.Models
{
    public class CreateEventRequest
    {
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
        public bool IsPublished { get; set; }
    }

    public class UpdateEventRequest
    {
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
        public bool IsPublished { get; set; }
    }

    public class ProductAssetDto
    {
        public int Id { get; set; }
        public string Kind { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string ObjectKey { get; set; } = string.Empty;
        public string? Url { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class EventPhotoDto
    {
        public int Id { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public bool WatermarkApplied { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PhotographerEventDto
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
        public string? BuyerInstructions { get; set; }
        public string? DeliveryLink { get; set; }
        public string? CoverImageUrl { get; set; }
        public bool IsPublished { get; set; }
        public int PhotoCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<EventPhotoDto> Photos { get; set; } = new();
        public List<ProductAssetDto> ProductAssets { get; set; } = new();
    }

    public class ProductAssetUploadRequest
    {
        public string Kind { get; set; } = "digital_file";
        public IFormFile? File { get; set; }
    }
}
