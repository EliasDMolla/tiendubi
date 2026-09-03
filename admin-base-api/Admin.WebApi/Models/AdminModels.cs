namespace Admin.WebApi.Models
{
    // ========================================
    // Dashboard DTOs
    // ========================================
    
    public class AdminDashboardDto
    {
        public int TotalUsers { get; set; }
        public double UserGrowthPercent { get; set; }
        public int NewUsersThisMonth { get; set; }
        public double NewUsersGrowthPercent { get; set; }
        public long TotalStorageUsedBytes { get; set; }
        public List<PlanDistributionDto> PlanDistribution { get; set; } = new();
        public List<RecentUserDto> RecentRegistrations { get; set; } = new();
        public List<MonthlyGrowthDto> MonthlyGrowth { get; set; } = new();
    }

    public class PlanDistributionDto
    {
        public string Plan { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    public class RecentUserDto
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PlanType { get; set; } = "FREE";
        public DateTime CreatedAt { get; set; }
    }

    public class MonthlyGrowthDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int NewUsers { get; set; }
    }

    // ========================================
    // User Management DTOs
    // ========================================

    public class AdminUsersPagedDto
    {
        public List<AdminUserListDto> Users { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
    }

    public class AdminUserListDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public bool IsActive { get; set; }
        public string Role { get; set; } = "User";
        public string PlanType { get; set; } = "FREE";
        public bool IsProActive { get; set; }
        public long StorageUsedBytes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLogin { get; set; }
    }

    public class AdminUserDetailDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public bool IsActive { get; set; }
        public bool EmailVerified { get; set; }
        public string Role { get; set; } = "User";
        public string PlanType { get; set; } = "FREE";
        public bool IsProActive { get; set; }
        public bool TrialUsed { get; set; }
        public DateTime? TrialStartDate { get; set; }
        public DateTime? TrialEndDate { get; set; }
        public int TrialDaysRemaining { get; set; }
        public DateTime? ProSubscriptionStartDate { get; set; }
        public DateTime? ProSubscriptionEndDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLogin { get; set; }
        public string UsageTypeName { get; set; } = "Personal";
        public long StorageUsedBytes { get; set; }
        
        // Related data
        public List<AdminActionDto> RecentAdminActions { get; set; } = new();
    }

    // ========================================
    // Admin Action DTOs
    // ========================================

    public class AdminActionDto
    {
        public int Id { get; set; }
        public string AdminUserName { get; set; } = string.Empty;
        public string? TargetUserName { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    // ========================================
    // Request DTOs
    // ========================================

    public class ChangeUserRoleRequest
    {
        public string Role { get; set; } = string.Empty;
    }

    public class ChangeUserPlanRequest
    {
        public string PlanType { get; set; } = string.Empty;
    }

    public class ToggleUserStatusRequest
    {
        public bool Activate { get; set; }
    }

    // ========================================
    // Owner Finance DTOs
    // ========================================

    public class OwnerGlobalSalesSummaryDto
    {
        public decimal GrossTotal { get; set; }
        public decimal PlatformCommissionTotal { get; set; }
        public decimal NetTotal { get; set; }
        public int OrdersCount { get; set; }
        public List<OwnerStudioSalesDto> Studios { get; set; } = new();
    }

    public class OwnerStudioSalesDto
    {
        public int PhotographerId { get; set; }
        public string StudioName { get; set; } = string.Empty;
        public string StudioEmail { get; set; } = string.Empty;
        public int OrdersCount { get; set; }
        public decimal GrossTotal { get; set; }
        public decimal PlatformCommissionTotal { get; set; }
        public decimal NetTotal { get; set; }
        public DateTime? LastSaleAt { get; set; }
    }

    public class OwnerTransferApprovalListDto
    {
        public int TotalCount { get; set; }
        public List<OwnerTransferApprovalItemDto> Items { get; set; } = new();
    }

    public class OwnerTransferApprovalItemDto
    {
        public string ExternalReference { get; set; } = string.Empty;
        public int PhotographerId { get; set; }
        public string StudioName { get; set; } = string.Empty;
        public string StudioEmail { get; set; } = string.Empty;
        public int EventId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string BuyerName { get; set; } = string.Empty;
        public string BuyerEmail { get; set; } = string.Empty;
        public int PhotoCount { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime SubmittedAt { get; set; }
        public string Status { get; set; } = "TransferReceiptSent";
    }

    public class ApproveTransferSaleRequest
    {
        public int ClearanceHours { get; set; } = 72;
    }

    public class OwnerAccreditationSummaryDto
    {
        public decimal TotalPending { get; set; }
        public decimal TotalAvailable { get; set; }
        public decimal TotalWithdrawn { get; set; }
        public decimal TotalToAccreditNow { get; set; }
        public int PhotographersCount { get; set; }
        public List<OwnerAccreditationPhotographerDto> Photographers { get; set; } = new();
    }

    public class OwnerAccreditationPhotographerDto
    {
        public int PhotographerId { get; set; }
        public string StudioName { get; set; } = string.Empty;
        public string StudioEmail { get; set; } = string.Empty;
        public string? WithdrawalHolderName { get; set; }
        public string? WithdrawalBankName { get; set; }
        public string? WithdrawalAliasOrCbu { get; set; }
        public decimal PendingAmount { get; set; }
        public decimal AvailableAmount { get; set; }
        public decimal TotalWithdrawn { get; set; }
        public decimal ToAccreditNow { get; set; }
    }

    public class MarkAccreditationPaidOutRequest
    {
        public string? Note { get; set; }
    }

    public class OwnerPhotoDeliveryFailuresDto
    {
        public int TotalCount { get; set; }
        public List<OwnerPhotoDeliveryFailureItemDto> Items { get; set; } = new();
    }

    public class OwnerPhotoDeliveryFailureItemDto
    {
        public string ExternalReference { get; set; } = string.Empty;
        public string Status { get; set; } = "Paid";
        public int PhotographerId { get; set; }
        public string StudioName { get; set; } = string.Empty;
        public string StudioEmail { get; set; } = string.Empty;
        public int EventId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public string BuyerName { get; set; } = string.Empty;
        public string BuyerEmail { get; set; } = string.Empty;
        public int PhotoCount { get; set; }
        public string DeliveryEmailStatus { get; set; } = "NotSent";
        public int DeliveryEmailAttempts { get; set; }
        public DateTime? DeliveryEmailLastAttemptAt { get; set; }
        public DateTime? DeliveryEmailSentAt { get; set; }
        public string? DeliveryEmailError { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
