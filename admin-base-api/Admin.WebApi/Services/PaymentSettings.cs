namespace Admin.WebApi.Services
{
    public class FeatureSettings
    {
        public bool RegistrationEnabled { get; set; } = true;
        public bool PhotoUploadEnabled { get; set; } = true;
        public bool SeedDevelopmentAdmin { get; set; } = true;
        public bool SeedDemoData { get; set; } = false;
    }

    public class PaymentSettings
    {
        public bool Enabled { get; set; } = false;
        public bool MercadoPagoEnabled { get; set; } = true;
        public bool TransfersEnabled { get; set; } = false;
        public decimal MonthlyPrice { get; set; } = 24999;
        public decimal AnnualPrice { get; set; } = 239990;
        public string Currency { get; set; } = "ARS";
        public int TrialDays { get; set; } = 30;
        public decimal CommissionPercent { get; set; } = 0;
        public string DiscountCode { get; set; } = string.Empty;
        public decimal DiscountPercent { get; set; } = 0;
        public string TransferHolderName { get; set; } = string.Empty;
        public string TransferBankName { get; set; } = string.Empty;
        public string TransferAlias { get; set; } = string.Empty;
        public string TransferCbu { get; set; } = string.Empty;
        public string TransferAccountInfo { get; set; } = string.Empty;
    }

    public class MercadoPagoSettings
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
        public decimal CommissionPercentage { get; set; } = 0m;

        public string PublicKey { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string SuccessUrl { get; set; } = string.Empty;
        public string FailureUrl { get; set; } = string.Empty;
        public string PendingUrl { get; set; } = string.Empty;
        public string NotificationUrl { get; set; } = string.Empty;
    }

    public class PhotoDeliveryRetrySettings
    {
        public bool Enabled { get; set; } = true;
        public int MaxAttempts { get; set; } = 5;
        public int PollIntervalSeconds { get; set; } = 60;
        public List<int> BackoffMinutes { get; set; } = new() { 0, 1, 5, 30, 120 };
    }
}
