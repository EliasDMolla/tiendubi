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
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Admin.WebApi.Services
{
    public interface IPhotoCheckoutService
    {
        Task<PublicPhotoCheckoutResponse> CreateCheckoutAsync(string slug, int eventId, PublicPhotoCheckoutRequest request, CancellationToken cancellationToken = default);
        Task<PublicPhotoCheckoutResponse> CreateTransferCheckoutAsync(string slug, int eventId, PublicPhotoCheckoutRequest request, CancellationToken cancellationToken = default);
        Task<PublicPhotoCheckoutResponse> CreateFreeCheckoutAsync(string slug, int eventId, PublicPhotoCheckoutRequest request, CancellationToken cancellationToken = default);
        Task<PublicTransferReceiptResponse> SubmitTransferReceiptAsync(string externalReference, IFormFile receiptFile, CancellationToken cancellationToken = default);
        Task<PublicCheckoutStatusResponse> GetCheckoutStatusAsync(string externalReference, string buyerEmail, CancellationToken cancellationToken = default);
        Task ProcessMercadoPagoNotificationAsync(string topic, long id, CancellationToken cancellationToken = default);
    }

    public class PhotoCheckoutService : IPhotoCheckoutService
    {
        private const string PaidOrderStatus = "Paid";
        private const string PaymentProcessingErrorStatus = "PaymentProcessingError";

        private readonly Context _context;
        private readonly IR2StorageService _storageService;
        private readonly IPhotoDeliveryService _photoDeliveryService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IEmailService _emailService;
        private readonly ISecretProtector _secretProtector;
        private readonly PaymentSettings _paymentSettings;
        private readonly MercadoPagoSettings _mercadoPagoSettings;
        private readonly string? _frontendUrl;
        private readonly ILogger<PhotoCheckoutService> _logger;

        private static readonly HashSet<string> AllowedTransferReceiptExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".pdf"
        };

        public PhotoCheckoutService(
            Context context,
            IR2StorageService storageService,
            IPhotoDeliveryService photoDeliveryService,
            IHttpClientFactory httpClientFactory,
            IEmailService emailService,
            ISecretProtector secretProtector,
            IOptions<PaymentSettings> paymentSettings,
            IOptions<MercadoPagoSettings> mercadoPagoSettings,
            IConfiguration configuration,
            ILogger<PhotoCheckoutService> logger)
        {
            _context = context;
            _storageService = storageService;
            _photoDeliveryService = photoDeliveryService;
            _httpClientFactory = httpClientFactory;
            _emailService = emailService;
            _secretProtector = secretProtector;
            _paymentSettings = paymentSettings.Value;
            _mercadoPagoSettings = mercadoPagoSettings.Value;
            _frontendUrl = configuration["AppSettings:FrontendUrl"];
            _logger = logger;
        }

        public async Task<PublicPhotoCheckoutResponse> CreateCheckoutAsync(string slug, int eventId, PublicPhotoCheckoutRequest request, CancellationToken cancellationToken = default)
        {
            if (!_paymentSettings.Enabled)
                return new PublicPhotoCheckoutResponse { Success = false, Message = "Pagos deshabilitados" };

            if (!_paymentSettings.MercadoPagoEnabled)
                return new PublicPhotoCheckoutResponse { Success = false, Message = "MercadoPago deshabilitado" };

            var prepared = await PrepareCheckoutAsync(slug, eventId, request, cancellationToken);
            if (!prepared.Success)
                return new PublicPhotoCheckoutResponse { Success = false, Message = prepared.ErrorMessage ?? "No se pudo preparar el checkout" };

            if (!IsPaymentMethodAllowed(prepared.Event!.PaymentMethods, "mercadopago"))
                return new PublicPhotoCheckoutResponse { Success = false, Message = "Este producto no acepta Mercado Pago" };

            if (prepared.Total <= 0)
                return new PublicPhotoCheckoutResponse { Success = false, Message = "Este producto no requiere pago" };

            var sellerAccessToken = await GetSellerMercadoPagoAccessTokenAsync(prepared.Studio!.Id, cancellationToken);
            if (string.IsNullOrWhiteSpace(sellerAccessToken))
                return new PublicPhotoCheckoutResponse { Success = false, Message = "El vendedor todavia no conecto Mercado Pago" };

            var successUrl = NormalizeAbsoluteUrl(_mercadoPagoSettings.SuccessUrl);
            var failureUrl = NormalizeAbsoluteUrl(_mercadoPagoSettings.FailureUrl);
            var pendingUrl = NormalizeAbsoluteUrl(_mercadoPagoSettings.PendingUrl);
            var notificationUrl = NormalizeAbsoluteUrl(_mercadoPagoSettings.NotificationUrl);

            if (string.IsNullOrWhiteSpace(successUrl))
            {
                successUrl = BuildFallbackReturnUrl(_frontendUrl, "success");
            }

            if (string.IsNullOrWhiteSpace(failureUrl))
            {
                failureUrl = BuildFallbackReturnUrl(_frontendUrl, "failure");
            }

            if (string.IsNullOrWhiteSpace(pendingUrl))
            {
                pendingUrl = BuildFallbackReturnUrl(_frontendUrl, "pending");
            }

            var hasAnyBackUrl = !string.IsNullOrWhiteSpace(successUrl)
                                || !string.IsNullOrWhiteSpace(failureUrl)
                                || !string.IsNullOrWhiteSpace(pendingUrl);
            var canUseAutoReturn = CanUseAutoReturn(successUrl);

            if (hasAnyBackUrl && !canUseAutoReturn)
            {
                _logger.LogWarning("MercadoPago checkout sin SuccessUrl apta para auto_return. Se enviará preferencia sin auto_return. SuccessRaw={SuccessRaw}, SuccessResolved={SuccessResolved}", _mercadoPagoSettings.SuccessUrl, successUrl);
            }

            _logger.LogInformation("MercadoPago BackUrls checkout. Success={SuccessUrl}, Failure={FailureUrl}, Pending={PendingUrl}, AutoReturn={AutoReturn}",
                successUrl,
                failureUrl,
                pendingUrl,
                canUseAutoReturn ? "approved" : "null");

            MercadoPagoConfig.AccessToken = sellerAccessToken;

            var externalReference = $"photo:{Guid.NewGuid():N}";
            var session = new PhotoCheckoutSession
            {
                ExternalReference = externalReference,
                PhotographerId = prepared.Studio!.Id,
                EventId = eventId,
                PhotoIdsCsv = string.Join(',', prepared.AvailablePhotoIds),
                BuyerEmail = request.BuyerEmail.Trim(),
                BuyerName = string.IsNullOrWhiteSpace(request.BuyerName) ? null : request.BuyerName.Trim(),
                DiscountCode = prepared.AppliedDiscountCode,
                SubtotalAmount = prepared.Subtotal,
                DiscountAmount = prepared.DiscountAmount,
                TotalAmount = prepared.Total,
                Status = "Created",
                CreatedAt = DateTime.UtcNow
            };

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
                ExternalReference = externalReference,
                Expires = true,
                ExpirationDateFrom = DateTime.UtcNow,
                ExpirationDateTo = DateTime.UtcNow.AddHours(2),
                Payer = new PreferencePayerRequest
                {
                    Email = session.BuyerEmail,
                    Name = session.BuyerName ?? session.BuyerEmail
                },
                Items = new List<PreferenceItemRequest>
                {
                    new PreferenceItemRequest
                    {
                        Id = $"event-{eventId}",
                        Title = prepared.AvailablePhotoIds.Count > 0 ? $"Fotos {prepared.Event!.Name} ({prepared.AvailablePhotoIds.Count})" : prepared.Event!.Name,
                        Quantity = 1,
                        CurrencyId = _paymentSettings.Currency,
                        UnitPrice = prepared.Total
                    }
                }
            };

            if (canUseAutoReturn)
            {
                preferenceRequest.AutoReturn = "approved";
            }

            var preferenceClient = new PreferenceClient();
            Preference preference = await preferenceClient.CreateAsync(preferenceRequest);

            session.PreferenceId = preference.Id;
            _context.PhotoCheckoutSessions.Add(session);
            await _context.SaveChangesAsync(cancellationToken);

            return new PublicPhotoCheckoutResponse
            {
                Success = true,
                Message = "Checkout creado",
                CheckoutUrl = preference.InitPoint,
                PreferenceId = preference.Id,
                ExternalReference = externalReference,
                PaymentMethod = "mercadopago",
                SubtotalAmount = prepared.Subtotal,
                DiscountAmount = prepared.DiscountAmount,
                TotalAmount = prepared.Total,
                Currency = _paymentSettings.Currency
            };
        }

        public async Task<PublicPhotoCheckoutResponse> CreateTransferCheckoutAsync(string slug, int eventId, PublicPhotoCheckoutRequest request, CancellationToken cancellationToken = default)
        {
            if (!_paymentSettings.Enabled)
                return new PublicPhotoCheckoutResponse { Success = false, Message = "Pagos deshabilitados" };

            if (!_paymentSettings.TransfersEnabled)
                return new PublicPhotoCheckoutResponse { Success = false, Message = "Transferencias deshabilitadas" };

            var prepared = await PrepareCheckoutAsync(slug, eventId, request, cancellationToken);
            if (!prepared.Success)
                return new PublicPhotoCheckoutResponse { Success = false, Message = prepared.ErrorMessage ?? "No se pudo preparar el checkout" };

            var studio = prepared.Studio!;

            if (!IsPaymentMethodAllowed(prepared.Event!.PaymentMethods, "transfer"))
                return new PublicPhotoCheckoutResponse { Success = false, Message = "Este producto no acepta transferencia" };

            var holderName = studio.WithdrawalHolderName?.Trim();
            var bankName = studio.WithdrawalBankName?.Trim();
            var aliasOrCbu = studio.WithdrawalAliasOrCbu?.Trim();
            var alias = LooksLikeBankAccountNumber(aliasOrCbu) ? null : aliasOrCbu;
            var cbu = LooksLikeBankAccountNumber(aliasOrCbu) ? aliasOrCbu : null;
            var accountInfo = _paymentSettings.TransferAccountInfo?.Trim();

            if (string.IsNullOrWhiteSpace(holderName) || string.IsNullOrWhiteSpace(bankName) || (string.IsNullOrWhiteSpace(alias) && string.IsNullOrWhiteSpace(cbu)))
            {
                return new PublicPhotoCheckoutResponse
                {
                    Success = false,
                    Message = "El vendedor todavia no configuro sus datos de transferencia"
                };
            }

            var externalReference = $"transfer:{Guid.NewGuid():N}";
            var session = new PhotoCheckoutSession
            {
                ExternalReference = externalReference,
                PhotographerId = studio.Id,
                EventId = eventId,
                PhotoIdsCsv = string.Join(',', prepared.AvailablePhotoIds),
                BuyerEmail = request.BuyerEmail.Trim(),
                BuyerName = string.IsNullOrWhiteSpace(request.BuyerName) ? null : request.BuyerName.Trim(),
                DiscountCode = prepared.AppliedDiscountCode,
                SubtotalAmount = prepared.Subtotal,
                DiscountAmount = prepared.DiscountAmount,
                TotalAmount = prepared.Total,
                Status = "AwaitingTransfer",
                CreatedAt = DateTime.UtcNow
            };

            _context.PhotoCheckoutSessions.Add(session);
            await _context.SaveChangesAsync(cancellationToken);

            return new PublicPhotoCheckoutResponse
            {
                Success = true,
                Message = "Transferencia iniciada. Usa los datos para realizar el pago.",
                ExternalReference = externalReference,
                PaymentMethod = "transfer",
                TransferData = new PublicTransferPaymentData
                {
                    HolderName = holderName,
                    BankName = bankName,
                    Alias = string.IsNullOrWhiteSpace(alias) ? null : alias,
                    Cbu = string.IsNullOrWhiteSpace(cbu) ? null : cbu,
                    AccountInfo = string.IsNullOrWhiteSpace(accountInfo) ? null : accountInfo,
                    Amount = prepared.Total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
                    Currency = _paymentSettings.Currency,
                    Reference = externalReference
                },
                SubtotalAmount = prepared.Subtotal,
                DiscountAmount = prepared.DiscountAmount,
                TotalAmount = prepared.Total,
                Currency = _paymentSettings.Currency
            };
        }

        public async Task<PublicPhotoCheckoutResponse> CreateFreeCheckoutAsync(string slug, int eventId, PublicPhotoCheckoutRequest request, CancellationToken cancellationToken = default)
        {
            var prepared = await PrepareCheckoutAsync(slug, eventId, request, cancellationToken);
            if (!prepared.Success)
                return new PublicPhotoCheckoutResponse { Success = false, Message = prepared.ErrorMessage ?? "No se pudo preparar el checkout" };

            if (!string.Equals(prepared.Event!.PriceType, "free", StringComparison.OrdinalIgnoreCase))
                return new PublicPhotoCheckoutResponse { Success = false, Message = "Este producto no es gratis" };

            var externalReference = $"free:{Guid.NewGuid():N}";
            var now = DateTime.UtcNow;
            var session = new PhotoCheckoutSession
            {
                ExternalReference = externalReference,
                PhotographerId = prepared.Studio!.Id,
                EventId = eventId,
                PhotoIdsCsv = string.Join(',', prepared.AvailablePhotoIds),
                BuyerEmail = request.BuyerEmail.Trim(),
                BuyerName = string.IsNullOrWhiteSpace(request.BuyerName) ? null : request.BuyerName.Trim(),
                SubtotalAmount = 0m,
                DiscountAmount = 0m,
                TotalAmount = 0m,
                Status = "Paid",
                CreatedAt = now,
                PaidAt = now
            };

            _context.PhotoCheckoutSessions.Add(session);
            _context.PhotoSales.Add(new PhotoSale
            {
                UserId = prepared.Studio.Id,
                PhotographerEventId = eventId,
                Quantity = Math.Max(1, prepared.AvailablePhotoIds.Count),
                TotalAmount = 0m,
                BuyerName = string.IsNullOrWhiteSpace(session.BuyerName) ? "Comprador" : session.BuyerName!,
                BuyerEmail = session.BuyerEmail,
                PaymentMethod = "free",
                Status = "paid",
                SoldAt = now,
                CreatedAt = now,
                UpdatedAt = now
            });

            await _context.SaveChangesAsync(cancellationToken);

            if (prepared.AvailablePhotoIds.Count > 0)
            {
                await _photoDeliveryService.SendPurchasedPhotosAsync(session.Id, cancellationToken);
            }

            return new PublicPhotoCheckoutResponse
            {
                Success = true,
                Message = "Producto gratis solicitado correctamente",
                ExternalReference = externalReference,
                PaymentMethod = "free",
                SubtotalAmount = 0m,
                DiscountAmount = 0m,
                TotalAmount = 0m,
                Currency = _paymentSettings.Currency
            };
        }

        public async Task<PublicTransferReceiptResponse> SubmitTransferReceiptAsync(string externalReference, IFormFile receiptFile, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(externalReference))
            {
                return new PublicTransferReceiptResponse
                {
                    Success = false,
                    Message = "Referencia de transferencia inválida"
                };
            }

            if (receiptFile == null || receiptFile.Length <= 0)
            {
                return new PublicTransferReceiptResponse
                {
                    Success = false,
                    Message = "Debes adjuntar un comprobante"
                };
            }

            if (receiptFile.Length > 10 * 1024 * 1024)
            {
                return new PublicTransferReceiptResponse
                {
                    Success = false,
                    Message = "El comprobante supera el máximo permitido (10MB)"
                };
            }

            var extension = Path.GetExtension(receiptFile.FileName ?? string.Empty);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedTransferReceiptExtensions.Contains(extension))
            {
                return new PublicTransferReceiptResponse
                {
                    Success = false,
                    Message = "Formato no permitido. Usa JPG, PNG o PDF"
                };
            }

            var session = await _context.PhotoCheckoutSessions
                .FirstOrDefaultAsync(s => s.ExternalReference == externalReference, cancellationToken);

            if (session == null)
            {
                return new PublicTransferReceiptResponse
                {
                    Success = false,
                    Message = "No se encontró la compra para esa referencia"
                };
            }

            if (string.Equals(session.Status, "TransferReceiptSent", StringComparison.OrdinalIgnoreCase)
                || string.Equals(session.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                return new PublicTransferReceiptResponse
                {
                    Success = true,
                    Message = "El comprobante ya fue enviado anteriormente",
                    ExternalReference = externalReference,
                    Status = session.Status
                };
            }

            var studioSegment = "estudio";
            var eventSegment = $"evento-{session.EventId}";

            var studio = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == session.PhotographerId)
                .Select(u => new
                {
                    u.PublicSlug,
                    u.FullName,
                    u.Email
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (studio != null)
            {
                var studioBase = !string.IsNullOrWhiteSpace(studio.PublicSlug)
                    ? studio.PublicSlug!
                    : !string.IsNullOrWhiteSpace(studio.FullName)
                        ? studio.FullName!
                        : studio.Email;

                studioSegment = SlugifyPathSegment(studioBase);
            }

            var dbEvent = await _context.PhotographerEvents
                .AsNoTracking()
                .Where(e => e.Id == session.EventId)
                .Select(e => new { e.Name, e.Id })
                .FirstOrDefaultAsync(cancellationToken);

            if (dbEvent != null)
            {
                eventSegment = SlugifyPathSegment($"{dbEvent.Name}-{dbEvent.Id}");
            }

            var safeReference = Regex.Replace(externalReference, "[^a-zA-Z0-9_-]", "-").ToLowerInvariant();
            var fileName = $"{safeReference}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{extension.ToLowerInvariant()}";
            var objectKey = $"comprobantes/estudios/{studioSegment}/{eventSegment}/{fileName}";

            var contentType = string.IsNullOrWhiteSpace(receiptFile.ContentType)
                ? ResolveReceiptContentType(extension)
                : receiptFile.ContentType;

            try
            {
                await using var stream = receiptFile.OpenReadStream();
                await _storageService.UploadAsync(objectKey, stream, contentType, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo subir comprobante de transferencia a R2. Reference={Reference}, ObjectKey={ObjectKey}", externalReference, objectKey);

                var message = ex is InvalidOperationException
                    ? "R2 no está configurado en el servidor (AccountId, AccessKeyId, SecretAccessKey y BucketName)."
                    : "No se pudo guardar el comprobante en almacenamiento";

                return new PublicTransferReceiptResponse
                {
                    Success = false,
                    Message = message
                };
            }

            _logger.LogInformation("Comprobante de transferencia almacenado en R2. Reference={Reference}, ObjectKey={ObjectKey}", externalReference, objectKey);

            session.Status = "TransferReceiptSent";

            var photoCount = Math.Max(1, ParsePhotoIdsCsv(session.PhotoIdsCsv).Distinct().Count());

            _context.PhotoSales.Add(new PhotoSale
            {
                UserId = session.PhotographerId,
                PhotographerEventId = session.EventId,
                Quantity = photoCount,
                TotalAmount = session.TotalAmount,
                BuyerName = string.IsNullOrWhiteSpace(session.BuyerName) ? "Comprador" : session.BuyerName!,
                BuyerEmail = session.BuyerEmail,
                PaymentMethod = "transfer",
                Status = "pending_confirmation",
                SoldAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);

            return new PublicTransferReceiptResponse
            {
                Success = true,
                Message = "Comprobante recibido correctamente",
                ExternalReference = externalReference,
                Status = session.Status
            };
        }

        public async Task<PublicCheckoutStatusResponse> GetCheckoutStatusAsync(string externalReference, string buyerEmail, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(externalReference) || string.IsNullOrWhiteSpace(buyerEmail))
            {
                return new PublicCheckoutStatusResponse
                {
                    Success = false,
                    Message = "Referencia o email inválido"
                };
            }

            var normalizedReference = externalReference.Trim();
            var normalizedEmail = buyerEmail.Trim().ToLower();

            var session = await _context.PhotoCheckoutSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.ExternalReference == normalizedReference
                    && s.BuyerEmail.ToLower() == normalizedEmail,
                    cancellationToken);

            if (session == null)
            {
                return new PublicCheckoutStatusResponse
                {
                    Success = false,
                    Message = "No encontramos una compra con esa referencia y email"
                };
            }

            var paymentStatus = session.Status;
            var deliveryStatus = session.DeliveryEmailStatus;
            var isPaid = string.Equals(paymentStatus, "Paid", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(paymentStatus, PaymentProcessingErrorStatus, StringComparison.OrdinalIgnoreCase);
            var isDelivered = string.Equals(deliveryStatus, "Sent", StringComparison.OrdinalIgnoreCase);

            return new PublicCheckoutStatusResponse
            {
                Success = true,
                Message = BuildPublicStatusMessage(paymentStatus, deliveryStatus),
                ExternalReference = session.ExternalReference,
                PaymentStatus = paymentStatus,
                DeliveryStatus = deliveryStatus,
                IsPaid = isPaid,
                IsDelivered = isDelivered,
                DeliveryAttempts = session.DeliveryEmailAttempts,
                PaidAt = session.PaidAt,
                LastDeliveryAttemptAt = session.DeliveryEmailLastAttemptAt,
                DeliverySentAt = session.DeliveryEmailSentAt
            };
        }

        private static string ResolveReceiptContentType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".pdf" => "application/pdf",
                _ => "application/octet-stream"
            };
        }

        private async Task<string?> GetSellerMercadoPagoAccessTokenAsync(int photographerId, CancellationToken cancellationToken)
        {
            var account = await _context.PhotographerMercadoPagoAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.PhotographerId == photographerId && a.IsActive, cancellationToken);

            if (account == null || account.TokenExpiration <= DateTime.UtcNow || string.IsNullOrWhiteSpace(account.AccessToken))
                return null;

            return _secretProtector.Unprotect(account.AccessToken);
        }

        private static bool IsPaymentMethodAllowed(string? configuredMethods, string method)
        {
            return (configuredMethods ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(value => string.Equals(value, method, StringComparison.OrdinalIgnoreCase));
        }

        private static bool LooksLikeBankAccountNumber(string? value)
        {
            var digits = Regex.Replace(value ?? string.Empty, "[^0-9]", string.Empty);
            return digits.Length >= 18;
        }

        private static IEnumerable<int> ParsePhotoIdsCsv(string? csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
                return Array.Empty<int>();

            return csv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => int.TryParse(value, out var id) ? id : 0)
                .Where(id => id > 0);
        }

        private static string SlugifyPathSegment(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "sin-nombre";

            var normalized = raw.Trim().ToLowerInvariant();
            normalized = Regex.Replace(normalized, "[^a-z0-9]+", "-");
            normalized = normalized.Trim('-');
            return string.IsNullOrWhiteSpace(normalized) ? "sin-nombre" : normalized;
        }

        private async Task<PreparedCheckout> PrepareCheckoutAsync(string slug, int eventId, PublicPhotoCheckoutRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.BuyerEmail))
                return PreparedCheckout.Fail("Email de comprador requerido");

            var studio = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.PublicSlug == slug && u.IsActive, cancellationToken);

            if (studio == null)
                return PreparedCheckout.Fail("Perfil público no encontrado");

            var dbEvent = await _context.PhotographerEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == eventId && e.UserId == studio.Id && e.IsPublished, cancellationToken);

            if (dbEvent == null)
                return PreparedCheckout.Fail("Evento no encontrado");

            var uniquePhotoIds = (request.PhotoIds ?? new List<int>()).Where(id => id > 0).Distinct().ToList();
            var availablePhotoIds = uniquePhotoIds.Count > 0
                ? await _context.EventPhotos
                    .AsNoTracking()
                    .Where(p => p.PhotographerEventId == eventId && uniquePhotoIds.Contains(p.Id) && p.IsProcessed)
                    .Select(p => p.Id)
                    .ToListAsync(cancellationToken)
                : new List<int>();

            var isProductCheckout = uniquePhotoIds.Count == 0;

            if (!isProductCheckout && availablePhotoIds.Count == 0)
                return PreparedCheckout.Fail("No hay fotos válidas para comprar");

            var normalizedBuyerEmail = request.BuyerEmail.Trim().ToLower();
            var previousSessions = await _context.PhotoCheckoutSessions
                .AsNoTracking()
                .Where(s =>
                    s.EventId == eventId &&
                    s.BuyerEmail.ToLower() == normalizedBuyerEmail &&
                    (s.Status == "Paid" || s.Status == PaymentProcessingErrorStatus || s.Status == "AwaitingTransfer" || s.Status == "TransferReceiptSent"))
                .Select(s => s.PhotoIdsCsv)
                .ToListAsync(cancellationToken);

            if (!isProductCheckout && previousSessions.Count > 0)
            {
                var alreadyPurchasedPhotoIds = previousSessions
                    .SelectMany(ParsePhotoIdsCsv)
                    .Distinct()
                    .ToHashSet();

                var duplicatePhotoIds = availablePhotoIds
                    .Where(id => alreadyPurchasedPhotoIds.Contains(id))
                    .ToList();

                if (duplicatePhotoIds.Count > 0)
                    return PreparedCheckout.Fail("Ese email ya compró una o más de las fotos seleccionadas para este evento");
            }

            var itemCount = isProductCheckout ? 1 : availablePhotoIds.Count;
            var subtotal = string.Equals(dbEvent.PriceType, "free", StringComparison.OrdinalIgnoreCase)
                ? 0m
                : Math.Round(dbEvent.PricePerPhoto * itemCount, 2, MidpointRounding.AwayFromZero);

            var discountCodeInput = request.DiscountCode?.Trim();
            var configuredDiscountCode = (_paymentSettings.DiscountCode ?? string.Empty).Trim();
            var discountPercent = Math.Clamp(_paymentSettings.DiscountPercent, 0m, 100m);

            var isDiscountApplied =
                !string.IsNullOrWhiteSpace(discountCodeInput)
                && !string.IsNullOrWhiteSpace(configuredDiscountCode)
                && discountPercent > 0
                && string.Equals(discountCodeInput, configuredDiscountCode, StringComparison.OrdinalIgnoreCase);

            var discountAmount = isDiscountApplied
                ? Math.Round(subtotal * (discountPercent / 100m), 2, MidpointRounding.AwayFromZero)
                : 0m;

            var total = Math.Max(0m, subtotal - discountAmount);

            return PreparedCheckout.Ok(
                studio,
                dbEvent,
                availablePhotoIds,
                subtotal,
                discountAmount,
                total,
                isDiscountApplied ? discountCodeInput!.ToUpperInvariant() : null);
        }

        private sealed class PreparedCheckout
        {
            public bool Success { get; private set; }
            public string? ErrorMessage { get; private set; }
            public User? Studio { get; private set; }
            public PhotographerEvent? Event { get; private set; }
            public List<int> AvailablePhotoIds { get; private set; } = new();
            public decimal Subtotal { get; private set; }
            public decimal DiscountAmount { get; private set; }
            public decimal Total { get; private set; }
            public string? AppliedDiscountCode { get; private set; }

            public static PreparedCheckout Fail(string message)
            {
                return new PreparedCheckout { Success = false, ErrorMessage = message };
            }

            public static PreparedCheckout Ok(User studio, PhotographerEvent dbEvent, List<int> availablePhotoIds, decimal subtotal, decimal discountAmount, decimal total, string? appliedDiscountCode)
            {
                return new PreparedCheckout
                {
                    Success = true,
                    Studio = studio,
                    Event = dbEvent,
                    AvailablePhotoIds = availablePhotoIds,
                    Subtotal = subtotal,
                    DiscountAmount = discountAmount,
                    Total = total,
                    AppliedDiscountCode = appliedDiscountCode
                };
            }
        }

        public async Task ProcessMercadoPagoNotificationAsync(string topic, long id, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(topic) || id <= 0)
                return;

            var merchantOrderId = await ResolveMerchantOrderIdAsync(topic, id, cancellationToken);
            if (!merchantOrderId.HasValue || merchantOrderId.Value <= 0)
                return;

            if (string.IsNullOrWhiteSpace(_mercadoPagoSettings.AccessToken))
            {
                _logger.LogWarning("Webhook de fotos ignorado: AccessToken de MercadoPago no configurado");
                return;
            }

            MercadoPagoConfig.AccessToken = _mercadoPagoSettings.AccessToken;
            var merchantOrderClient = new MerchantOrderClient();
            MerchantOrder merchantOrder = merchantOrderClient.Get(merchantOrderId.Value);

            if (merchantOrder == null)
                return;

            var isPaid = string.Equals(merchantOrder.Status, "approved", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(merchantOrder.OrderStatus, "paid", StringComparison.OrdinalIgnoreCase);

            if (!isPaid || string.IsNullOrWhiteSpace(merchantOrder.ExternalReference))
                return;

            if (!merchantOrder.ExternalReference.StartsWith("photo:", StringComparison.OrdinalIgnoreCase))
                return;

            var session = await _context.PhotoCheckoutSessions
                .Include(s => s.Event)
                .FirstOrDefaultAsync(s => s.ExternalReference == merchantOrder.ExternalReference, cancellationToken);

            if (session == null)
            {
                _logger.LogWarning("Session no encontrada para externalReference={ExternalReference}", merchantOrder.ExternalReference);
                return;
            }

            if (string.Equals(session.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(session.DeliveryEmailStatus, "Sent", StringComparison.OrdinalIgnoreCase))
                {
                    var (retrySuccess, retryMessage) = await _photoDeliveryService.SendPurchasedPhotosAsync(session.Id, cancellationToken);
                    if (!retrySuccess)
                    {
                        await NotifyBuyerProcessingIssueAsync(session, retryMessage, cancellationToken);
                    }
                }

                return;
            }

            try
            {
                var now = DateTime.UtcNow;
                var photoIds = session.PhotoIdsCsv
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(value => int.TryParse(value, out var idValue) ? idValue : 0)
                    .Where(idValue => idValue > 0)
                    .Distinct()
                    .ToList();

                var isProductCheckout = photoIds.Count == 0;

                if (isProductCheckout && string.Equals(session.Event?.ProductType, "photo_gallery", StringComparison.OrdinalIgnoreCase))
                {
                    var reason = "No hay fotos válidas asociadas a la compra";
                    await MarkSessionAsProcessingErrorAsync(session, merchantOrderId.Value, reason, cancellationToken);
                    await NotifyBuyerProcessingIssueAsync(session, reason, cancellationToken);
                    _logger.LogWarning("Session de checkout sin fotos válidas. SessionId={SessionId}", session.Id);
                    return;
                }

                var itemCount = Math.Max(1, photoIds.Count);
                var unitTotals = SplitAmount(session.TotalAmount, itemCount);
                var commissionPercent = Math.Clamp(_paymentSettings.CommissionPercent, 0m, 100m);

                for (var index = 0; index < itemCount; index++)
                {
                    var totalAmount = unitTotals[index];
                    var mercadoPagoFee = 0m;
                    var platformCommission = Math.Round(totalAmount * (commissionPercent / 100m), 2, MidpointRounding.AwayFromZero);
                    var photographerNet = Math.Max(0m, totalAmount - platformCommission - mercadoPagoFee);

                    _context.Orders.Add(new Order
                    {
                        PhotographerId = session.PhotographerId,
                        EventId = session.EventId,
                        PhotoId = photoIds.Count > index ? photoIds[index] : null,
                        TotalAmount = totalAmount,
                        PlatformCommission = platformCommission,
                        MercadoPagoFee = mercadoPagoFee,
                        PhotographerNet = photographerNet,
                        Status = PaidOrderStatus,
                        CreatedAt = now,
                        ClearedAt = now.AddHours(72)
                    });
                }

                _context.PhotoSales.Add(new PhotoSale
                {
                    UserId = session.PhotographerId,
                    PhotographerEventId = session.EventId,
                    Quantity = itemCount,
                    TotalAmount = session.TotalAmount,
                    BuyerName = string.IsNullOrWhiteSpace(session.BuyerName) ? "Comprador" : session.BuyerName!,
                    BuyerEmail = session.BuyerEmail,
                    PaymentMethod = "mercadopago",
                    Status = "paid",
                    SoldAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                });

                var balance = await _context.PhotographerBalances
                    .FirstOrDefaultAsync(b => b.PhotographerId == session.PhotographerId, cancellationToken);

                var totalNet = unitTotals
                    .Select(total =>
                    {
                        var platformCommission = Math.Round(total * (commissionPercent / 100m), 2, MidpointRounding.AwayFromZero);
                        return Math.Max(0m, total - platformCommission);
                    })
                    .Sum();

                if (balance == null)
                {
                    balance = new PhotographerBalance
                    {
                        PhotographerId = session.PhotographerId,
                        PendingAmount = totalNet,
                        AvailableAmount = 0m,
                        TotalWithdrawn = 0m
                    };
                    _context.PhotographerBalances.Add(balance);
                }
                else
                {
                    balance.PendingAmount += totalNet;
                }

                session.Status = "Paid";
                session.MerchantOrderId = merchantOrderId.Value;
                session.PaidAt = now;
                session.DeliveryEmailError = null;

                await _context.SaveChangesAsync(cancellationToken);

                await NotifyBuyerPaymentReceivedAsync(session);

                var (deliverySuccess, deliveryMessage) = photoIds.Count > 0
                    ? await _photoDeliveryService.SendPurchasedPhotosAsync(session.Id, cancellationToken)
                    : (true, "Compra de producto registrada");
                if (!deliverySuccess)
                {
                    await NotifyBuyerProcessingIssueAsync(session, deliveryMessage, cancellationToken);
                    _logger.LogWarning("Pago confirmado pero falló email de entrega. SessionId={SessionId}, ExternalReference={ExternalReference}, Message={Message}",
                        session.Id,
                        session.ExternalReference,
                        deliveryMessage);
                }
            }
            catch (Exception ex)
            {
                var reason = $"Error interno procesando el pago confirmado: {ex.Message}";
                await MarkSessionAsProcessingErrorAsync(session, merchantOrderId.Value, reason, cancellationToken);
                await NotifyBuyerProcessingIssueAsync(session, reason, cancellationToken);

                _logger.LogError(ex, "Error procesando sesión de checkout pagada. SessionId={SessionId}, ExternalReference={ExternalReference}, MerchantOrderId={MerchantOrderId}",
                    session.Id,
                    session.ExternalReference,
                    merchantOrderId.Value);
            }
        }

        private async Task NotifyBuyerPaymentReceivedAsync(PhotoCheckoutSession session)
        {
            if (string.IsNullOrWhiteSpace(session.BuyerEmail))
                return;

            var buyerName = string.IsNullOrWhiteSpace(session.BuyerName) ? "Comprador" : session.BuyerName!;

            try
            {
                await _emailService.SendPurchaseProcessingEmailAsync(session.BuyerEmail, buyerName, session.ExternalReference);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "No se pudo enviar email de confirmación de pago al comprador. SessionId={SessionId}, ExternalReference={ExternalReference}",
                    session.Id,
                    session.ExternalReference);
            }
        }

        private async Task<long?> ResolveMerchantOrderIdAsync(string topic, long id, CancellationToken cancellationToken)
        {
            if (string.Equals(topic, "merchant_order", StringComparison.OrdinalIgnoreCase))
                return id;

            if (!string.Equals(topic, "payment", StringComparison.OrdinalIgnoreCase))
                return null;

            if (string.IsNullOrWhiteSpace(_mercadoPagoSettings.AccessToken))
                return null;

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _mercadoPagoSettings.AccessToken);

                var response = await client.GetAsync($"https://api.mercadopago.com/v1/payments/{id}", cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("No se pudo resolver merchant_order desde payment webhook. PaymentId={PaymentId}, StatusCode={StatusCode}", id, response.StatusCode);
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = json.RootElement;

                var status = root.TryGetProperty("status", out var statusNode) && statusNode.ValueKind == JsonValueKind.String
                    ? statusNode.GetString()
                    : null;

                if (!string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Webhook payment recibido pero aún no aprobado. PaymentId={PaymentId}, Status={Status}", id, status);
                    return null;
                }

                if (!root.TryGetProperty("order", out var orderNode) || orderNode.ValueKind != JsonValueKind.Object)
                {
                    _logger.LogWarning("Webhook payment sin objeto order. PaymentId={PaymentId}", id);
                    return null;
                }

                if (orderNode.TryGetProperty("id", out var orderIdNode))
                {
                    if (orderIdNode.ValueKind == JsonValueKind.Number && orderIdNode.TryGetInt64(out var orderIdNumeric) && orderIdNumeric > 0)
                        return orderIdNumeric;

                    if (orderIdNode.ValueKind == JsonValueKind.String && long.TryParse(orderIdNode.GetString(), out var orderIdText) && orderIdText > 0)
                        return orderIdText;
                }

                _logger.LogWarning("Webhook payment sin order.id válido. PaymentId={PaymentId}", id);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resolviendo merchant_order desde payment webhook. PaymentId={PaymentId}", id);
                return null;
            }
        }

        private async Task MarkSessionAsProcessingErrorAsync(PhotoCheckoutSession session, long merchantOrderId, string reason, CancellationToken cancellationToken)
        {
            session.Status = PaymentProcessingErrorStatus;
            session.MerchantOrderId = merchantOrderId;
            session.PaidAt ??= DateTime.UtcNow;
            session.DeliveryEmailStatus = "Failed";
            session.DeliveryEmailError = TruncateError(reason);
            session.DeliveryEmailLastAttemptAt = DateTime.UtcNow;
            session.DeliveryEmailAttempts += 1;

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task NotifyBuyerProcessingIssueAsync(PhotoCheckoutSession session, string reason, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(session.BuyerEmail))
                return;

            var buyerName = string.IsNullOrWhiteSpace(session.BuyerName) ? "Comprador" : session.BuyerName!;
            var safeReason = TruncateError(reason);

            try
            {
                await _emailService.SendPhotoDeliveryIssueEmailAsync(session.BuyerEmail, buyerName, session.ExternalReference, safeReason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo enviar email de incidencia al comprador. SessionId={SessionId}, ExternalReference={ExternalReference}", session.Id, session.ExternalReference);
            }
        }

        private static string TruncateError(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "Error de procesamiento no especificado";

            return raw.Length <= 1000 ? raw : raw[..1000];
        }

        private static string BuildPublicStatusMessage(string? paymentStatus, string? deliveryStatus)
        {
            if (string.Equals(deliveryStatus, "Sent", StringComparison.OrdinalIgnoreCase))
                return "Tu compra está confirmada y el email con las fotos ya fue enviado";

            if (string.Equals(paymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(deliveryStatus, "Failed", StringComparison.OrdinalIgnoreCase))
                    return "Tu pago está confirmado y estamos reintentando el envío de tus fotos";

                return "Tu pago está confirmado y estamos preparando el envío de tus fotos";
            }

            if (string.Equals(paymentStatus, PaymentProcessingErrorStatus, StringComparison.OrdinalIgnoreCase))
                return "Recibimos tu pago, pero estamos revisando un inconveniente para completar la entrega";

            if (string.Equals(paymentStatus, "TransferReceiptSent", StringComparison.OrdinalIgnoreCase))
                return "Recibimos tu comprobante de transferencia y estamos validándolo";

            if (string.Equals(paymentStatus, "AwaitingTransfer", StringComparison.OrdinalIgnoreCase))
                return "Estamos esperando tu comprobante de transferencia";

            return "Tu compra está siendo procesada";
        }

        private static List<decimal> SplitAmount(decimal total, int parts)
        {
            if (parts <= 0)
                return new List<decimal>();

            var results = new List<decimal>(parts);
            var running = 0m;
            for (var i = 0; i < parts; i++)
            {
                var value = Math.Round(total / parts, 2, MidpointRounding.AwayFromZero);
                if (i == parts - 1)
                {
                    value = Math.Round(total - running, 2, MidpointRounding.AwayFromZero);
                }

                running += value;
                results.Add(value);
            }

            return results;
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

        private static string BuildFallbackReturnUrl(string? frontendBaseUrl, string paymentStatus)
        {
            var normalizedBase = NormalizeAbsoluteUrl(frontendBaseUrl) ?? "http://localhost:4200";
            var normalizedRoot = normalizedBase.TrimEnd('/');
            return $"{normalizedRoot}/plans?payment={paymentStatus}";
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
    }
}
