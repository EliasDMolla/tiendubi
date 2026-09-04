namespace Admin.WebApi.Models
{
    public class DashboardSummaryDto
    {
        public decimal TotalSalesThisMonth { get; set; }
        public decimal TotalSalesAllTime { get; set; }
        public decimal PendingAmount { get; set; }
        public decimal AvailableAmount { get; set; }
        public DateTime? NextAvailableAt { get; set; }
        public decimal TotalWithdrawn { get; set; }
        public int PhotosSoldThisMonth { get; set; }
        public int TotalPhotosSold { get; set; }
        public int ActiveEventsCount { get; set; }
    }

    public class EventSalesDto
    {
        public int EventId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string ProductType { get; set; } = string.Empty;
        public decimal TotalSales { get; set; }
        public int PhotosSold { get; set; }
        public decimal PendingAmount { get; set; }
        public decimal AvailableAmount { get; set; }
    }

    public class SaleDetailDto
    {
        public int SaleId { get; set; }
        public DateTime SoldAt { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string ProductType { get; set; } = string.Empty;
        public string BuyerName { get; set; } = string.Empty;
        public string? BuyerEmail { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ExternalReference { get; set; }
        public List<SalePurchasedPhotoDto> PurchasedPhotos { get; set; } = new();
    }

    public class SalePurchasedPhotoDto
    {
        public int PhotoId { get; set; }
        public string Label { get; set; } = string.Empty;
    }
}
