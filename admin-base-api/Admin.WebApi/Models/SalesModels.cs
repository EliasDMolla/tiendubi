namespace Admin.WebApi.Models
{
    public class SalesSummaryDto
    {
        public decimal TotalSalesThisMonth { get; set; }
        public decimal TotalSalesAllTime { get; set; }
        public decimal PendingAmount { get; set; }
        public decimal AvailableAmount { get; set; }
        public decimal TotalWithdrawn { get; set; }
        public int SalesCountThisMonth { get; set; }
        public int TotalSalesCount { get; set; }
    }

    public class SaleItemDto
    {
        public int OrderId { get; set; }
        public DateTime Date { get; set; }
        public int EventId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string? PhotoTitle { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PlatformCommission { get; set; }
        public decimal PhotographerNet { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class LiquidationDto
    {
        public int LiquidationId { get; set; }
        public DateTime LiquidationDate { get; set; }
        public decimal Amount { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int OrdersCount { get; set; }
    }

    public class WithdrawalResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime ProcessedAt { get; set; }
    }

    public class SalesListQuery
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int? EventId { get; set; }
        public string? Status { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    public class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }
}