using System.Globalization;
using Admin.Entities;
using Admin.Entities.Entities;
using Admin.WebApi.Infrastructure.MercadoPago;
using Admin.WebApi.Models.Payments;
using Admin.WebApi.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Admin.WebApi.Services
{
    public interface IMercadoPagoService
    {
        Task<MercadoPagoConnectResponse> BuildConnectUrlAsync(int photographerId, CancellationToken cancellationToken = default);
        Task<MercadoPagoConnectionStatusResponse> GetConnectionStatusAsync(int photographerId, CancellationToken cancellationToken = default);
        Task<MercadoPagoCallbackResult> HandleOAuthCallbackAsync(string code, string state, CancellationToken cancellationToken = default);
        Task<MercadoPagoOAuthTokenResponse> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken = default);
        Task<MercadoPagoOAuthTokenResponse> RefreshTokenAsync(int photographerId, CancellationToken cancellationToken = default);
        Task<MercadoPagoCreatePaymentResponse> CreatePaymentAsync(MercadoPagoCreatePaymentRequest request, CancellationToken cancellationToken = default);
    }

    public class MercadoPagoService : IMercadoPagoService
    {
        private readonly Context _context;
        private readonly PaymentSettings _paymentSettings;
        private readonly MercadoPagoSettings _settings;
        private readonly IMercadoPagoClient _mercadoPagoClient;
        private readonly IMercadoPagoOAuthStateService _oauthStateService;
        private readonly IPhotographerMercadoPagoAccountRepository _accountRepository;
        private readonly ISecretProtector _secretProtector;
        private readonly ILogger<MercadoPagoService> _logger;

        public MercadoPagoService(
            Context context,
            IOptions<PaymentSettings> paymentSettings,
            IOptions<MercadoPagoSettings> mercadoPagoSettings,
            IMercadoPagoClient mercadoPagoClient,
            IMercadoPagoOAuthStateService oauthStateService,
            IPhotographerMercadoPagoAccountRepository accountRepository,
            ISecretProtector secretProtector,
            ILogger<MercadoPagoService> logger)
        {
            _context = context;
            _paymentSettings = paymentSettings.Value;
            _settings = mercadoPagoSettings.Value;
            _mercadoPagoClient = mercadoPagoClient;
            _oauthStateService = oauthStateService;
            _accountRepository = accountRepository;
            _secretProtector = secretProtector;
            _logger = logger;
        }

        public Task<MercadoPagoOAuthTokenResponse> ExchangeAuthorizationCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            ValidateOAuthConfiguration();
            return _mercadoPagoClient.ExchangeAuthorizationCodeAsync(code, _settings, cancellationToken);
        }

        public async Task<MercadoPagoOAuthTokenResponse> RefreshTokenAsync(int photographerId, CancellationToken cancellationToken = default)
        {
            ValidateOAuthConfiguration();

            var account = await _accountRepository.GetByPhotographerIdAsync(photographerId, cancellationToken);
            if (account == null || !account.IsActive)
                throw new InvalidOperationException("La cuenta de MercadoPago del fotógrafo no está conectada.");

            var plainRefreshToken = _secretProtector.Unprotect(account.RefreshToken);
            if (string.IsNullOrWhiteSpace(plainRefreshToken))
                throw new InvalidOperationException("No se pudo recuperar el refresh_token del fotógrafo.");

            var tokenResponse = await _mercadoPagoClient.RefreshTokenAsync(plainRefreshToken, _settings, cancellationToken);
            await SaveOAuthTokensAsync(photographerId, tokenResponse, cancellationToken);

            return tokenResponse;
        }

        public Task<MercadoPagoConnectResponse> BuildConnectUrlAsync(int photographerId, CancellationToken cancellationToken = default)
        {
            ValidateOAuthConfiguration();

            var state = _oauthStateService.CreateState(photographerId);
            var url =
                $"https://auth.mercadopago.com.ar/authorization" +
                $"?client_id={Uri.EscapeDataString(_settings.ClientId)}" +
                "&response_type=code" +
                "&platform_id=mp" +
                $"&redirect_uri={Uri.EscapeDataString(_settings.RedirectUri)}" +
                $"&state={Uri.EscapeDataString(state)}";

            return Task.FromResult(new MercadoPagoConnectResponse { AuthorizationUrl = url });
        }

        public async Task<MercadoPagoConnectionStatusResponse> GetConnectionStatusAsync(int photographerId, CancellationToken cancellationToken = default)
        {
            var account = await _accountRepository.GetByPhotographerIdAsync(photographerId, cancellationToken);
            if (account == null || !account.IsActive)
            {
                return new MercadoPagoConnectionStatusResponse
                {
                    Connected = false,
                    TokenExpired = true
                };
            }

            return new MercadoPagoConnectionStatusResponse
            {
                Connected = true,
                MercadoPagoUserId = account.MercadoPagoUserId,
                TokenExpiration = account.TokenExpiration,
                TokenExpired = account.TokenExpiration <= DateTime.UtcNow,
                PublicKey = account.PublicKey
            };
        }

        public async Task<MercadoPagoCallbackResult> HandleOAuthCallbackAsync(string code, string state, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(code))
                return new MercadoPagoCallbackResult { Success = false, Message = "Código OAuth inválido." };

            if (!_oauthStateService.TryReadState(state, out var photographerId))
                return new MercadoPagoCallbackResult { Success = false, Message = "Estado OAuth inválido o expirado." };

            var userExists = await _context.Users.AnyAsync(u => u.Id == photographerId && u.IsActive, cancellationToken);
            if (!userExists)
                return new MercadoPagoCallbackResult { Success = false, Message = "Fotógrafo inválido para conectar MercadoPago." };

            try
            {
                var tokenResponse = await ExchangeAuthorizationCodeAsync(code, cancellationToken);
                await SaveOAuthTokensAsync(photographerId, tokenResponse, cancellationToken);

                return new MercadoPagoCallbackResult { Success = true, Message = "Cuenta conectada correctamente." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error conectando MercadoPago para photographerId={PhotographerId}", photographerId);
                return new MercadoPagoCallbackResult { Success = false, Message = "No se pudo completar la conexión con MercadoPago." };
            }
        }

        public async Task<MercadoPagoCreatePaymentResponse> CreatePaymentAsync(MercadoPagoCreatePaymentRequest request, CancellationToken cancellationToken = default)
        {
            if (request.PhotographerId <= 0)
                return new MercadoPagoCreatePaymentResponse { Success = false, Message = "PhotographerId inválido." };

            if (request.TotalAmount <= 0)
                return new MercadoPagoCreatePaymentResponse { Success = false, Message = "El monto debe ser mayor a cero." };

            if (string.IsNullOrWhiteSpace(request.PaymentMethodId) || string.IsNullOrWhiteSpace(request.PayerEmail) || string.IsNullOrWhiteSpace(request.Token))
                return new MercadoPagoCreatePaymentResponse { Success = false, Message = "Faltan campos requeridos para crear el pago." };

            try
            {
                var accessToken = await EnsureValidAccessTokenAsync(request.PhotographerId, cancellationToken);
                var commissionPercent = Math.Clamp(_settings.CommissionPercentage, 0m, 100m);
                var applicationFee = Math.Round(request.TotalAmount * (commissionPercent / 100m), 2, MidpointRounding.AwayFromZero);

                var payment = await _mercadoPagoClient.CreatePaymentAsync(accessToken, request, applicationFee, cancellationToken);
                var netAmount = Math.Max(0m, request.TotalAmount - applicationFee);

                return new MercadoPagoCreatePaymentResponse
                {
                    Success = true,
                    Message = "Pago creado correctamente.",
                    MercadoPagoPaymentId = payment.Id.ToString(CultureInfo.InvariantCulture),
                    Status = payment.Status,
                    StatusDetail = payment.StatusDetail,
                    TransactionAmount = payment.TransactionAmount,
                    ApplicationFee = applicationFee,
                    NetAmount = netAmount
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando pago split para photographerId={PhotographerId}", request.PhotographerId);
                return new MercadoPagoCreatePaymentResponse
                {
                    Success = false,
                    Message = "No se pudo crear el pago en MercadoPago."
                };
            }
        }

        private async Task<string> EnsureValidAccessTokenAsync(int photographerId, CancellationToken cancellationToken)
        {
            var account = await _accountRepository.GetByPhotographerIdAsync(photographerId, cancellationToken);
            if (account == null || !account.IsActive)
                throw new InvalidOperationException("El fotógrafo no tiene cuenta MercadoPago conectada.");

            if (account.TokenExpiration <= DateTime.UtcNow.AddMinutes(1))
            {
                await RefreshTokenAsync(photographerId, cancellationToken);
                account = await _accountRepository.GetByPhotographerIdAsync(photographerId, cancellationToken);
            }

            if (account == null || string.IsNullOrWhiteSpace(account.AccessToken))
                throw new InvalidOperationException("No se encontró un access_token válido para MercadoPago.");

            return _secretProtector.Unprotect(account.AccessToken);
        }

        private async Task SaveOAuthTokensAsync(int photographerId, MercadoPagoOAuthTokenResponse tokenResponse, CancellationToken cancellationToken)
        {
            var expiration = DateTime.UtcNow.AddSeconds(Math.Max(60, tokenResponse.ExpiresIn - 60));

            var account = new PhotographerMercadoPagoAccount
            {
                PhotographerId = photographerId,
                AccessToken = _secretProtector.Protect(tokenResponse.AccessToken),
                RefreshToken = _secretProtector.Protect(tokenResponse.RefreshToken),
                PublicKey = tokenResponse.PublicKey ?? string.Empty,
                MercadoPagoUserId = tokenResponse.UserId,
                TokenExpiration = expiration,
                IsActive = true,
                UpdatedAt = DateTime.UtcNow
            };

            await _accountRepository.UpsertAsync(account, cancellationToken);
        }

        private void ValidateOAuthConfiguration()
        {
            if (!_paymentSettings.Enabled || !_paymentSettings.MercadoPagoEnabled)
            {
                throw new InvalidOperationException("MercadoPago está deshabilitado por configuración.");
            }

            if (string.IsNullOrWhiteSpace(_settings.ClientId) ||
                string.IsNullOrWhiteSpace(_settings.ClientSecret) ||
                string.IsNullOrWhiteSpace(_settings.RedirectUri))
            {
                throw new InvalidOperationException("MercadoPago OAuth no está configurado. Revisá ClientId, ClientSecret y RedirectUri.");
            }
        }
    }
}
