using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Admin.WebApi.Models.Payments;
using Admin.WebApi.Services;

namespace Admin.WebApi.Infrastructure.MercadoPago
{
    public interface IMercadoPagoClient
    {
        Task<MercadoPagoOAuthTokenResponse> ExchangeAuthorizationCodeAsync(string code, MercadoPagoSettings settings, CancellationToken cancellationToken = default);
        Task<MercadoPagoOAuthTokenResponse> RefreshTokenAsync(string refreshToken, MercadoPagoSettings settings, CancellationToken cancellationToken = default);
        Task<MercadoPagoRawPaymentResponse> CreatePaymentAsync(string sellerAccessToken, MercadoPagoCreatePaymentRequest request, decimal applicationFee, CancellationToken cancellationToken = default);
    }

    public class MercadoPagoClient : IMercadoPagoClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _httpClient;

        public MercadoPagoClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress ??= new Uri("https://api.mercadopago.com/");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public Task<MercadoPagoOAuthTokenResponse> ExchangeAuthorizationCodeAsync(string code, MercadoPagoSettings settings, CancellationToken cancellationToken = default)
        {
            var payload = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = settings.ClientId,
                ["client_secret"] = settings.ClientSecret,
                ["code"] = code,
                ["redirect_uri"] = settings.RedirectUri
            };

            return SendOAuthTokenRequestAsync(payload, cancellationToken);
        }

        public Task<MercadoPagoOAuthTokenResponse> RefreshTokenAsync(string refreshToken, MercadoPagoSettings settings, CancellationToken cancellationToken = default)
        {
            var payload = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = settings.ClientId,
                ["client_secret"] = settings.ClientSecret,
                ["refresh_token"] = refreshToken
            };

            return SendOAuthTokenRequestAsync(payload, cancellationToken);
        }

        public async Task<MercadoPagoRawPaymentResponse> CreatePaymentAsync(string sellerAccessToken, MercadoPagoCreatePaymentRequest request, decimal applicationFee, CancellationToken cancellationToken = default)
        {
            var payload = new
            {
                transaction_amount = request.TotalAmount,
                description = request.Description,
                payment_method_id = request.PaymentMethodId,
                token = request.Token,
                installments = request.Installments < 1 ? 1 : request.Installments,
                application_fee = applicationFee,
                payer = new
                {
                    email = request.PayerEmail,
                    first_name = request.PayerFirstName,
                    last_name = request.PayerLastName
                }
            };

            var json = JsonSerializer.Serialize(payload, JsonOptions);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/payments")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sellerAccessToken);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Error MercadoPago CreatePayment ({(int)response.StatusCode}): {responseText}");
            }

            var payment = JsonSerializer.Deserialize<MercadoPagoRawPaymentResponse>(responseText, JsonOptions);
            if (payment == null)
            {
                throw new InvalidOperationException("MercadoPago devolvió una respuesta de pago vacía.");
            }

            return payment;
        }

        private async Task<MercadoPagoOAuthTokenResponse> SendOAuthTokenRequestAsync(Dictionary<string, string> payload, CancellationToken cancellationToken)
        {
            using var response = await _httpClient.PostAsJsonAsync("oauth/token", payload, JsonOptions, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Error MercadoPago OAuth ({(int)response.StatusCode}): {responseText}");
            }

            var tokenResponse = JsonSerializer.Deserialize<MercadoPagoOAuthTokenResponse>(responseText, JsonOptions);
            if (tokenResponse == null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            {
                throw new InvalidOperationException("MercadoPago devolvió un token inválido en OAuth.");
            }

            return tokenResponse;
        }
    }

    public class MercadoPagoRawPaymentResponse
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
        [JsonPropertyName("status_detail")]
        public string StatusDetail { get; set; } = string.Empty;
        [JsonPropertyName("transaction_amount")]
        public decimal TransactionAmount { get; set; }
    }
}
