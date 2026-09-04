using Admin.Entities;
using Admin.Entities.Entities;
using Admin.WebApi.Models;
using MercadoPago.Client.MerchantOrder;
using MercadoPago.Client.Preference;
using MercadoPago.Config;
using MercadoPago.Resource.MerchantOrder;
using MercadoPago.Resource.Preference;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Admin.WebApi.Services
{
    public interface ISubscriptionService
    {
        Task<PlanStatusResponse?> GetPlanStatusAsync(int userId);
        Task<ActivateTrialResponse> ActivateTrialAsync(int userId);
        Task<CreateMercadoPagoCheckoutResponse> CreateMercadoPagoCheckoutAsync(int userId, int months);
        Task ProcessMercadoPagoNotificationAsync(string topic, long id);
        Task<ConfirmMercadoPagoPaymentResponse> ConfirmMercadoPagoPaymentAsync(int userId, long merchantOrderId);
    }

    public class SubscriptionService : ISubscriptionService
    {
        private readonly Context _context;
        private readonly PaymentSettings _paymentSettings;
        private readonly MercadoPagoSettings _mercadoPagoSettings;
        private readonly ILogger<SubscriptionService> _logger;
        private readonly string _frontendUrl;

        public SubscriptionService(
            Context context,
            IOptions<PaymentSettings> paymentSettings,
            IOptions<MercadoPagoSettings> mercadoPagoSettings,
            IConfiguration configuration,
            ILogger<SubscriptionService> logger)
        {
            _context = context;
            _paymentSettings = paymentSettings.Value;
            _mercadoPagoSettings = mercadoPagoSettings.Value;
            _frontendUrl = configuration["AppSettings:FrontendUrl"] ?? "http://localhost:4200";
            _logger = logger;
        }

        public async Task<PlanStatusResponse?> GetPlanStatusAsync(int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return null;

            var proDaysRemaining = 0;
            if (user.ProSubscriptionEndDate.HasValue)
            {
                var days = (user.ProSubscriptionEndDate.Value - DateTime.UtcNow).Days;
                proDaysRemaining = days > 0 ? days : 0;
            }

            return new PlanStatusResponse
            {
                PlanType = user.PlanType.ToString(),
                IsProActive = user.IsProActive ||
                              user.Role == UserRole.Admin ||
                              user.Role == UserRole.SuperAdmin,
                TrialUsed = user.TrialUsed,
                CanActivateTrial = user.PlanType == PlanType.FREE && !user.TrialUsed,
                TrialStartDate = user.TrialStartDate,
                TrialEndDate = user.TrialEndDate,
                TrialDaysRemaining = user.TrialDaysRemaining,
                ProSubscriptionStartDate = user.ProSubscriptionStartDate,
                ProSubscriptionEndDate = user.ProSubscriptionEndDate,
                ProDaysRemaining = proDaysRemaining,
                MonthlyPrice = _paymentSettings.MonthlyPrice,
                AnnualPrice = _paymentSettings.AnnualPrice,
                Currency = _paymentSettings.Currency,
                PriceDisplay = $"{_paymentSettings.MonthlyPrice.ToString("N0", new CultureInfo("es-AR"))} {_paymentSettings.Currency}",
                AnnualPriceDisplay = $"{_paymentSettings.AnnualPrice.ToString("N0", new CultureInfo("es-AR"))} {_paymentSettings.Currency}",
                PaymentEnabled = _paymentSettings.Enabled,
                MercadoPagoEnabled = _paymentSettings.MercadoPagoEnabled
            };
        }

        public async Task<ActivateTrialResponse> ActivateTrialAsync(int userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return new ActivateTrialResponse { Success = false, Message = "Usuario no encontrado" };
            }

            if (user.TrialUsed)
            {
                return new ActivateTrialResponse { Success = false, Message = "Ya usaste tu período de prueba gratuito" };
            }

            if (user.PlanType != PlanType.FREE)
            {
                return new ActivateTrialResponse { Success = false, Message = "Solo podés activar trial desde plan FREE" };
            }

            var now = DateTime.UtcNow;
            var trialEnd = now.AddDays(_paymentSettings.TrialDays);

            user.PlanType = PlanType.PRO_TRIAL;
            user.TrialUsed = true;
            user.TrialStartDate = now;
            user.TrialEndDate = trialEnd;
#pragma warning disable CS0618
            user.Plan = "PRO";
            user.SubscriptionStatus = "TRIAL";
#pragma warning restore CS0618
            user.UpdatedAt = now;

            await _context.SaveChangesAsync();

            return new ActivateTrialResponse
            {
                Success = true,
                Message = $"¡Trial activado! Tenés {_paymentSettings.TrialDays} días de acceso Pro."
            };
        }

        public async Task<CreateMercadoPagoCheckoutResponse> CreateMercadoPagoCheckoutAsync(int userId, int months)
        {
            if (!_paymentSettings.Enabled)
            {
                return new CreateMercadoPagoCheckoutResponse
                {
                    Success = false,
                    Message = "El sistema de pagos no está habilitado"
                };
            }

            if (!_paymentSettings.MercadoPagoEnabled)
            {
                return new CreateMercadoPagoCheckoutResponse
                {
                    Success = false,
                    Message = "MercadoPago está deshabilitado"
                };
            }

            if (string.IsNullOrWhiteSpace(_mercadoPagoSettings.AccessToken))
            {
                return new CreateMercadoPagoCheckoutResponse
                {
                    Success = false,
                    Message = "MercadoPago no está configurado (falta AccessToken)"
                };
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                return new CreateMercadoPagoCheckoutResponse { Success = false, Message = "Usuario no encontrado" };
            }

            months = months == 12 ? 12 : 1;
            var amount = months == 12 ? _paymentSettings.AnnualPrice : _paymentSettings.MonthlyPrice;

            var successUrl = BuildPlanReturnUrl(_frontendUrl, "success");
            var failureUrl = BuildPlanReturnUrl(_frontendUrl, "failure");
            var pendingUrl = BuildPlanReturnUrl(_frontendUrl, "pending");
            var notificationUrl = NormalizeAbsoluteUrl(_mercadoPagoSettings.NotificationUrl);
            var hasAnyBackUrl = !string.IsNullOrWhiteSpace(successUrl)
                                || !string.IsNullOrWhiteSpace(failureUrl)
                                || !string.IsNullOrWhiteSpace(pendingUrl);
            var canUseAutoReturn = CanUseAutoReturn(successUrl);

            MercadoPagoConfig.AccessToken = _mercadoPagoSettings.AccessToken;

            var preferenceRequest = new PreferenceRequest
            {
                BackUrls = hasAnyBackUrl
                    ? new PreferenceBackUrlsRequest
                    {
                        Success = successUrl,
                        Failure = failureUrl,
                        Pending = pendingUrl
                    }
                    : null,
                NotificationUrl = notificationUrl,
                BinaryMode = true,
                AutoReturn = null,
                ExternalReference = user.Id.ToString(),
                AdditionalInfo = months.ToString(),
                Expires = true,
                ExpirationDateFrom = DateTime.UtcNow,
                ExpirationDateTo = DateTime.UtcNow.AddDays(1),
                Marketplace = "Tiendubi",
                Payer = new PreferencePayerRequest
                {
                    Email = user.Email,
                    Name = user.FullName ?? user.Email
                },
                Items = new List<PreferenceItemRequest>
                {
                    new PreferenceItemRequest
                    {
                        Id = $"pro-{months}",
                        Title = months == 12 ? "Suscripción Pro anual" : "Suscripción Pro mensual",
                        Quantity = 1,
                        CurrencyId = _paymentSettings.Currency,
                        UnitPrice = amount
                    }
                }
            };

            if (canUseAutoReturn)
            {
                preferenceRequest.AutoReturn = "approved";
            }

            var client = new PreferenceClient();
            Preference preference = await client.CreateAsync(preferenceRequest);

            return new CreateMercadoPagoCheckoutResponse
            {
                Success = true,
                Message = "Preferencia creada correctamente",
                CheckoutUrl = preference.InitPoint,
                PreferenceId = preference.Id,
                Amount = amount,
                Currency = _paymentSettings.Currency
            };
        }

        private static string? NormalizeAbsoluteUrl(string? rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl))
                return null;

            var trimmed = rawUrl.Trim();
            return Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                ? uri.ToString()
                : null;
        }

        private static bool CanUseAutoReturn(string? successUrl)
        {
            if (string.IsNullOrWhiteSpace(successUrl))
                return false;

            if (!Uri.TryCreate(successUrl, UriKind.Absolute, out var uri))
                return false;

            var host = uri.Host.ToLowerInvariant();
            if (host == "localhost" || host == "127.0.0.1" || host == "::1")
                return false;

            return true;
        }

        private static string BuildPlanReturnUrl(string? frontendBaseUrl, string paymentStatus)
        {
            var normalizedBase = NormalizeAbsoluteUrl(frontendBaseUrl) ?? "http://localhost:4200";
            return $"{normalizedBase.TrimEnd('/')}/panel/plans?payment={paymentStatus}";
        }

        public async Task ProcessMercadoPagoNotificationAsync(string topic, long id)
        {
            if (!string.Equals(topic, "merchant_order", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Webhook MP ignorado. Topic inválido: {Topic}", topic);
                return;
            }

            if (string.IsNullOrWhiteSpace(_mercadoPagoSettings.AccessToken))
            {
                _logger.LogWarning("Webhook MP recibido sin AccessToken configurado");
                return;
            }

            MercadoPagoConfig.AccessToken = _mercadoPagoSettings.AccessToken;

            var merchantOrderClient = new MerchantOrderClient();
            MerchantOrder merchantOrder = merchantOrderClient.Get(id);

            if (merchantOrder == null)
            {
                _logger.LogWarning("No se pudo recuperar merchant order de MP. Id={Id}", id);
                return;
            }

            var paid = string.Equals(merchantOrder.Status, "approved", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(merchantOrder.OrderStatus, "paid", StringComparison.OrdinalIgnoreCase);

            if (!paid)
            {
                _logger.LogInformation("Webhook MP recibido pero no pagado. Id={Id}, Status={Status}, OrderStatus={OrderStatus}",
                    id, merchantOrder.Status, merchantOrder.OrderStatus);
                return;
            }

            var alreadyProcessed = await _context.AdminActions.AnyAsync(a =>
                a.ActionType == AdminActionType.ManualPaymentCreated
                && a.EntityType == "MercadoPagoOrder"
                && a.NewValue == id.ToString());

            if (alreadyProcessed)
            {
                _logger.LogInformation("Webhook MP duplicado ignorado. Id={Id}", id);
                return;
            }

            if (!int.TryParse(merchantOrder.ExternalReference, out var userId) || userId <= 0)
            {
                _logger.LogWarning("Webhook MP sin ExternalReference válido. Id={Id}, ExternalReference={ExternalReference}",
                    id, merchantOrder.ExternalReference);
                return;
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                _logger.LogWarning("Webhook MP para usuario inexistente. Id={Id}, UserId={UserId}", id, userId);
                return;
            }

            var months = 1;
            if (!string.IsNullOrWhiteSpace(merchantOrder.AdditionalInfo) && int.TryParse(merchantOrder.AdditionalInfo, out var monthsParsed) && monthsParsed > 0)
            {
                months = monthsParsed;
            }

            var now = DateTime.UtcNow;
            var renewalBaseDate = user.ProSubscriptionEndDate.HasValue && user.ProSubscriptionEndDate.Value > now
                ? user.ProSubscriptionEndDate.Value
                : now;
            var endDate = renewalBaseDate.AddMonths(months);

            user.PlanType = PlanType.PRO;
            user.ProSubscriptionStartDate ??= now;
            user.ProSubscriptionEndDate = endDate;
#pragma warning disable CS0618
            user.Plan = "PRO";
            user.ProUpgradeDate ??= now;
            user.SubscriptionStatus = "ACTIVO";
#pragma warning restore CS0618
            user.UpdatedAt = now;

            _context.AdminActions.Add(new AdminAction
            {
                AdminUserId = 1,
                TargetUserId = user.Id,
                ActionType = AdminActionType.ManualPaymentCreated,
                EntityType = "MercadoPagoOrder",
                Description = $"Pago MercadoPago aprobado. OrderId={id}",
                NewValue = id.ToString(),
                IpAddress = "mercadopago-webhook",
                CreatedAt = now
            });

            await _context.SaveChangesAsync();

            _logger.LogInformation("Pago MercadoPago aplicado correctamente. Id={Id}, UserId={UserId}, Months={Months}", id, userId, months);
        }

        public async Task<ConfirmMercadoPagoPaymentResponse> ConfirmMercadoPagoPaymentAsync(int userId, long merchantOrderId)
        {
            if (merchantOrderId <= 0)
            {
                return new ConfirmMercadoPagoPaymentResponse
                {
                    Success = false,
                    Message = "merchant_order_id inválido"
                };
            }

            if (string.IsNullOrWhiteSpace(_mercadoPagoSettings.AccessToken))
            {
                return new ConfirmMercadoPagoPaymentResponse
                {
                    Success = false,
                    Message = "Mercado Pago no está configurado"
                };
            }

            MercadoPagoConfig.AccessToken = _mercadoPagoSettings.AccessToken;
            var merchantOrder = new MerchantOrderClient().Get(merchantOrderId);
            if (merchantOrder == null
                || !int.TryParse(merchantOrder.ExternalReference, out var orderUserId)
                || orderUserId != userId)
            {
                return new ConfirmMercadoPagoPaymentResponse
                {
                    Success = false,
                    Message = "La orden no pertenece al usuario autenticado"
                };
            }

            var paid = string.Equals(merchantOrder.Status, "approved", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(merchantOrder.OrderStatus, "paid", StringComparison.OrdinalIgnoreCase);
            if (!paid)
            {
                return new ConfirmMercadoPagoPaymentResponse
                {
                    Success = false,
                    Message = "El pago todavía no fue acreditado por Mercado Pago"
                };
            }

            await ProcessMercadoPagoNotificationAsync("merchant_order", merchantOrderId);

            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId);
            if (user?.IsProActive != true)
            {
                return new ConfirmMercadoPagoPaymentResponse
                {
                    Success = false,
                    Message = "El pago todavía no fue acreditado por Mercado Pago"
                };
            }

            return new ConfirmMercadoPagoPaymentResponse
            {
                Success = true,
                Message = "Pago procesado correctamente"
            };
        }
    }
}
