using System.Text.Json.Serialization;

namespace Admin.WebApi.Models.Payments
{
    public class MercadoPagoConnectResponse
    {
        public string AuthorizationUrl { get; set; } = string.Empty;
    }

    public class MercadoPagoConnectionStatusResponse
    {
        public bool Connected { get; set; }
        public string? MercadoPagoUserId { get; set; }
        public DateTime? TokenExpiration { get; set; }
        public bool TokenExpired { get; set; }
        public string? PublicKey { get; set; }
    }

    public class MercadoPagoCallbackResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class MercadoPagoCreatePaymentRequest
    {
        public int PhotographerId { get; set; }
        public decimal TotalAmount { get; set; }
        public string Description { get; set; } = string.Empty;
        public string PaymentMethodId { get; set; } = string.Empty;
        public string PayerEmail { get; set; } = string.Empty;
        public string? PayerFirstName { get; set; }
        public string? PayerLastName { get; set; }
        public string Token { get; set; } = string.Empty;
        public int Installments { get; set; } = 1;
    }

    public class MercadoPagoCreatePaymentResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? MercadoPagoPaymentId { get; set; }
        public string? Status { get; set; }
        public string? StatusDetail { get; set; }
        public decimal TransactionAmount { get; set; }
        public decimal ApplicationFee { get; set; }
        public decimal NetAmount { get; set; }
    }

    public class MercadoPagoOAuthTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
        [JsonPropertyName("scope")]
        public string Scope { get; set; } = string.Empty;
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;
        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;
        [JsonPropertyName("public_key")]
        public string PublicKey { get; set; } = string.Empty;
        [JsonPropertyName("live_mode")]
        public bool LiveMode { get; set; }
    }
}
