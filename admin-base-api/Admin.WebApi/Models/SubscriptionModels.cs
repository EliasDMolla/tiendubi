namespace Admin.WebApi.Models
{
    public class PlanStatusResponse
    {
        public string PlanType { get; set; } = "FREE";
        public bool IsProActive { get; set; }
        public bool TrialUsed { get; set; }
        public bool CanActivateTrial { get; set; }
        public DateTime? TrialStartDate { get; set; }
        public DateTime? TrialEndDate { get; set; }
        public int TrialDaysRemaining { get; set; }
        public DateTime? ProSubscriptionStartDate { get; set; }
        public DateTime? ProSubscriptionEndDate { get; set; }
        public int ProDaysRemaining { get; set; }
        public decimal MonthlyPrice { get; set; }
        public string Currency { get; set; } = "ARS";
        public string PriceDisplay { get; set; } = "$0";
    }

    public class ActivateTrialResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class CreateMercadoPagoCheckoutRequest
    {
        public int Months { get; set; } = 1;
    }

    public class CreateMercadoPagoCheckoutResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? CheckoutUrl { get; set; }
        public string? PreferenceId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "ARS";
    }

    public class ConfirmMercadoPagoPaymentRequest
    {
        public long MerchantOrderId { get; set; }
    }

    public class ConfirmMercadoPagoPaymentResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class PublicSettingsResponse
    {
        public PaymentPublicSettings Payment { get; set; } = new();
        public FeaturePublicSettings Features { get; set; } = new();
    }

    public class PaymentPublicSettings
    {
        public bool Enabled { get; set; }
        public bool MercadoPagoEnabled { get; set; }
        public bool TransfersEnabled { get; set; }
        public decimal CommissionPercent { get; set; }
        public string DiscountCode { get; set; } = string.Empty;
        public decimal DiscountPercent { get; set; }
    }

    public class FeaturePublicSettings
    {
        public bool RegistrationEnabled { get; set; }
        public bool PhotoUploadEnabled { get; set; }
    }
}
