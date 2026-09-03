using Admin.Entities;
using Admin.Entities.Entities;
using Admin.WebApi.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Net;

namespace Admin.WebApi.Services;

public interface IDemoDataService
{
    Task<DemoSeedResult> SeedDemoPhotographerAsync(bool forceReset, CancellationToken cancellationToken = default);
}

public sealed class DemoSeedResult
{
    public bool Success { get; set; }
    public bool RebuiltData { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Email { get; set; } = DemoAccountDefaults.Email;
    public string Password { get; set; } = DemoAccountDefaults.Password;
    public string PublicSlug { get; set; } = DemoAccountDefaults.PublicSlug;
}

public class DemoDataService : IDemoDataService
{
    private readonly Context _context;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DemoDataService> _logger;

    public DemoDataService(Context context, IWebHostEnvironment environment, ILogger<DemoDataService> logger)
    {
        _context = context;
        _environment = environment;
        _logger = logger;
    }

    public async Task<DemoSeedResult> SeedDemoPhotographerAsync(bool forceReset, CancellationToken cancellationToken = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            var uploadsRoot = Path.Combine(_environment.ContentRootPath, "uploads");
            var demoEmail = DemoAccountDefaults.Email.ToLowerInvariant();
            var demoLegacyEmail = DemoAccountDefaults.LegacyEmail.ToLowerInvariant();
            Directory.CreateDirectory(uploadsRoot);

            var demoUser = await _context.Users.FirstOrDefaultAsync(
                u => u.Email.ToLower() == demoEmail
                     || u.Email.ToLower() == demoLegacyEmail
                     || u.PublicSlug == DemoAccountDefaults.PublicSlug,
                cancellationToken);

            if (demoUser == null)
            {
                demoUser = new User
                {
                    Email = DemoAccountDefaults.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(DemoAccountDefaults.Password, workFactor: 10),
                    FullName = DemoAccountDefaults.FullName,
                    PublicSlug = DemoAccountDefaults.PublicSlug,
                    PhoneNumber = "+54 9 11 5555 1802",
                    WithdrawalHolderName = "Demo Sports Studio SAS",
                    WithdrawalBankName = "Banco Ciudad",
                    WithdrawalAliasOrCbu = "demo.sports.studio",
                    IsActive = true,
                    EmailVerified = true,
                    Role = UserRole.User,
                    PlanType = PlanType.PRO,
                    UsageTypeId = 1,
                    CreatedAt = now,
                    UpdatedAt = now,
#pragma warning disable CS0618
                    Plan = "PRO",
                    SubscriptionStatus = "ACTIVO"
#pragma warning restore CS0618
                };

                _context.Users.Add(demoUser);
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Usuario demo {DemoEmail} creado automáticamente.", DemoAccountDefaults.Email);
            }
            else
            {
                demoUser.Email = DemoAccountDefaults.Email;
                demoUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(DemoAccountDefaults.Password, workFactor: 10);
                demoUser.FullName = DemoAccountDefaults.FullName;
                demoUser.PublicSlug = DemoAccountDefaults.PublicSlug;
                demoUser.PhoneNumber = "+54 9 11 5555 1802";
                demoUser.WithdrawalHolderName = "Demo Sports Studio SAS";
                demoUser.WithdrawalBankName = "Banco Ciudad";
                demoUser.WithdrawalAliasOrCbu = "demo.sports.studio";
                demoUser.IsActive = true;
                demoUser.EmailVerified = true;
                demoUser.Role = UserRole.User;
                demoUser.PlanType = PlanType.PRO;
                demoUser.UsageTypeId = 1;
                demoUser.UpdatedAt = now;
#pragma warning disable CS0618
                demoUser.Plan = "PRO";
                demoUser.SubscriptionStatus = "ACTIVO";
#pragma warning restore CS0618

                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Usuario demo {DemoEmail} actualizado automáticamente.", DemoAccountDefaults.Email);
            }

            var hasDemoData = await _context.PhotographerEvents.CountAsync(e => e.UserId == demoUser.Id, cancellationToken) >= 2
                && await _context.Orders.AnyAsync(o => o.PhotographerId == demoUser.Id, cancellationToken)
                && await _context.EventPhotos.AnyAsync(p => p.PhotographerEvent.UserId == demoUser.Id, cancellationToken);

            if (hasDemoData && !forceReset)
            {
                _logger.LogInformation("Datos demo ya existentes para {DemoEmail}. No se regeneran.", DemoAccountDefaults.Email);
                return new DemoSeedResult
                {
                    Success = true,
                    RebuiltData = false,
                    Message = "Datos demo ya existentes. No se regeneraron."
                };
            }

            var existingEventIds = await _context.PhotographerEvents
                .Where(e => e.UserId == demoUser.Id)
                .Select(e => e.Id)
                .ToListAsync(cancellationToken);

            if (existingEventIds.Count > 0)
            {
                var existingPhotos = await _context.EventPhotos
                    .Where(p => existingEventIds.Contains(p.PhotographerEventId))
                    .ToListAsync(cancellationToken);

                var existingSales = await _context.PhotoSales
                    .Where(s => s.UserId == demoUser.Id)
                    .ToListAsync(cancellationToken);

                var existingOrders = await _context.Orders
                    .Where(o => o.PhotographerId == demoUser.Id)
                    .ToListAsync(cancellationToken);

                var existingCheckoutSessions = await _context.PhotoCheckoutSessions
                    .Where(c => c.PhotographerId == demoUser.Id)
                    .ToListAsync(cancellationToken);

                var existingEvents = await _context.PhotographerEvents
                    .Where(e => e.UserId == demoUser.Id)
                    .ToListAsync(cancellationToken);

                _context.PhotoCheckoutSessions.RemoveRange(existingCheckoutSessions);
                _context.Orders.RemoveRange(existingOrders);
                _context.PhotoSales.RemoveRange(existingSales);
                _context.EventPhotos.RemoveRange(existingPhotos);
                _context.PhotographerEvents.RemoveRange(existingEvents);
            }

            var existingBalance = await _context.PhotographerBalances
                .FirstOrDefaultAsync(b => b.PhotographerId == demoUser.Id, cancellationToken);

            if (existingBalance != null)
            {
                _context.PhotographerBalances.Remove(existingBalance);
            }

            var existingLiquidations = await _context.Liquidations
                .Where(l => l.PhotographerId == demoUser.Id)
                .ToListAsync(cancellationToken);

            _context.Liquidations.RemoveRange(existingLiquidations);
            await _context.SaveChangesAsync(cancellationToken);

            foreach (var existingEventId in existingEventIds)
            {
                var eventFolder = Path.Combine(uploadsRoot, "events", existingEventId.ToString());
                if (Directory.Exists(eventFolder))
                {
                    Directory.Delete(eventFolder, recursive: true);
                }
            }

            var runningEvent = new PhotographerEvent
            {
                UserId = demoUser.Id,
                Name = "Circuito Urbano 10K - Puerto Norte",
                Description = "Cobertura demo de running con dorsales, llegada y podio para explorar busquedas y ventas reales.",
                EventDate = now.AddDays(-6),
                PricePerPhoto = 3200m,
                IsPublished = true,
                CreatedAt = now.AddDays(-8),
                UpdatedAt = now.AddHours(-14)
            };

            var cyclingEvent = new PhotographerEvent
            {
                UserId = demoUser.Id,
                Name = "Gran Fondo Sierra Azul",
                Description = "Evento demo de ciclismo con paisajes, peloton y sprint final para mostrar galerias deportivas.",
                EventDate = now.AddDays(-19),
                PricePerPhoto = 4100m,
                IsPublished = true,
                CreatedAt = now.AddDays(-21),
                UpdatedAt = now.AddDays(-5)
            };

            _context.PhotographerEvents.AddRange(runningEvent, cyclingEvent);
            await _context.SaveChangesAsync(cancellationToken);

            var photoSeedData = new[]
            {
                new DemoPhotoSeed(runningEvent.Id, "run-0182-salida.svg", "Running 10K", "Dorsal 182", "Largada elite", "182,Running,Salida,10K", "#0f4c81", now.AddDays(-7).AddMinutes(15)),
                new DemoPhotoSeed(runningEvent.Id, "run-0182-llegada.svg", "Running 10K", "Dorsal 182", "Sprint final", "182,Running,Llegada,Meta", "#d9485f", now.AddDays(-7).AddMinutes(24)),
                new DemoPhotoSeed(runningEvent.Id, "run-0276-meta.svg", "Running 10K", "Dorsal 276", "Cruce de meta", "276,Running,Meta,10K", "#198754", now.AddDays(-7).AddMinutes(33)),
                new DemoPhotoSeed(runningEvent.Id, "run-0310-rio.svg", "Running 10K", "Dorsal 310", "Costanera norte", "310,Running,Rio,10K", "#3b82f6", now.AddDays(-7).AddMinutes(41)),
                new DemoPhotoSeed(runningEvent.Id, "run-0451-podio.svg", "Running 10K", "Dorsal 451", "Podio general", "451,Running,Podio,10K", "#7c3aed", now.AddDays(-7).AddMinutes(49)),
                new DemoPhotoSeed(runningEvent.Id, "run-0615-pack.svg", "Running 10K", "Dorsal 615", "Peloton medio", "615,Running,Peloton,10K", "#f97316", now.AddDays(-7).AddMinutes(57)),
                new DemoPhotoSeed(cyclingEvent.Id, "bike-021-ascenso.svg", "Gran Fondo", "Rider 021", "Ascenso serrano", "021,Ciclismo,Ascenso,Sierra", "#14532d", now.AddDays(-20).AddMinutes(12)),
                new DemoPhotoSeed(cyclingEvent.Id, "bike-044-peloton.svg", "Gran Fondo", "Rider 044", "Peloton principal", "044,Ciclismo,Peloton,Sierra", "#1d4ed8", now.AddDays(-20).AddMinutes(26)),
                new DemoPhotoSeed(cyclingEvent.Id, "bike-087-sprint.svg", "Gran Fondo", "Rider 087", "Sprint final", "087,Ciclismo,Sprint,Sierra", "#b91c1c", now.AddDays(-20).AddMinutes(39)),
                new DemoPhotoSeed(cyclingEvent.Id, "bike-103-curva.svg", "Gran Fondo", "Rider 103", "Curva tecnica", "103,Ciclismo,Curva,Sierra", "#0f766e", now.AddDays(-20).AddMinutes(53)),
                new DemoPhotoSeed(cyclingEvent.Id, "bike-120-premiacion.svg", "Gran Fondo", "Rider 120", "Premiacion", "120,Ciclismo,Premiacion,Sierra", "#854d0e", now.AddDays(-20).AddMinutes(67))
            };

            var photos = photoSeedData.Select((seed, index) =>
            {
                var baseName = Path.GetFileNameWithoutExtension(seed.Name);
                return new EventPhoto
                {
                    PhotographerEventId = seed.EventId,
                    OriginalFileName = seed.Name,
                    StoredFileName = seed.Name,
                    RelativePath = $"events/{seed.EventId}/originals/{seed.Name}",
                    OriginalPath = $"events/{seed.EventId}/originals/{seed.Name}",
                    ThumbnailPath = $"events/{seed.EventId}/thumbs/{seed.Name}",
                    WatermarkedPath = $"events/{seed.EventId}/watermarked/{seed.Name}",
                    Tags = seed.Tags,
                    SizeBytes = 220_000 + (index * 8_500),
                    IsProcessed = true,
                    ProcessingFailed = false,
                    ProcessingError = null,
                    WatermarkApplied = true,
                    CreatedAt = seed.CreatedAt,
                    UpdatedAt = seed.CreatedAt
                };
            }).ToList();

            _context.EventPhotos.AddRange(photos);
            await _context.SaveChangesAsync(cancellationToken);

            await EnsureDemoAssetsAsync(photoSeedData, cancellationToken);

            var runningPhotos = photos.Where(p => p.PhotographerEventId == runningEvent.Id).ToList();
            var cyclingPhotos = photos.Where(p => p.PhotographerEventId == cyclingEvent.Id).ToList();

            var orders = new List<Order>
            {
                new()
                {
                    PhotographerId = demoUser.Id,
                    EventId = runningEvent.Id,
                    PhotoId = runningPhotos.ElementAtOrDefault(0)?.Id,
                    TotalAmount = 3200m,
                    PlatformCommission = 480m,
                    MercadoPagoFee = 192m,
                    PhotographerNet = 2528m,
                    Status = "Paid",
                    ClearedAt = now.AddDays(2),
                    CreatedAt = now.AddDays(-1),
                    UpdatedAt = now.AddDays(-1)
                },
                new()
                {
                    PhotographerId = demoUser.Id,
                    EventId = runningEvent.Id,
                    PhotoId = runningPhotos.ElementAtOrDefault(2)?.Id,
                    TotalAmount = 6400m,
                    PlatformCommission = 960m,
                    MercadoPagoFee = 384m,
                    PhotographerNet = 5056m,
                    Status = "Paid",
                    ClearedAt = now.AddDays(-1),
                    CreatedAt = now.AddDays(-4),
                    UpdatedAt = now.AddDays(-4)
                },
                new()
                {
                    PhotographerId = demoUser.Id,
                    EventId = runningEvent.Id,
                    PhotoId = runningPhotos.ElementAtOrDefault(4)?.Id,
                    TotalAmount = 9600m,
                    PlatformCommission = 1440m,
                    MercadoPagoFee = 576m,
                    PhotographerNet = 7584m,
                    Status = "Paid",
                    ClearedAt = now.AddDays(-2),
                    CreatedAt = now.AddDays(-3),
                    UpdatedAt = now.AddDays(-3)
                },
                new()
                {
                    PhotographerId = demoUser.Id,
                    EventId = cyclingEvent.Id,
                    PhotoId = cyclingPhotos.ElementAtOrDefault(1)?.Id,
                    TotalAmount = 4100m,
                    PlatformCommission = 615m,
                    MercadoPagoFee = 246m,
                    PhotographerNet = 3239m,
                    Status = "PaidOut",
                    ClearedAt = now.AddDays(-14),
                    PaidOutAt = now.AddDays(-8),
                    CreatedAt = now.AddDays(-17),
                    UpdatedAt = now.AddDays(-8)
                },
                new()
                {
                    PhotographerId = demoUser.Id,
                    EventId = cyclingEvent.Id,
                    PhotoId = cyclingPhotos.ElementAtOrDefault(3)?.Id,
                    TotalAmount = 8200m,
                    PlatformCommission = 1230m,
                    MercadoPagoFee = 492m,
                    PhotographerNet = 6478m,
                    Status = "Paid",
                    ClearedAt = now.AddDays(-4),
                    CreatedAt = now.AddDays(-11),
                    UpdatedAt = now.AddDays(-11)
                },
                new()
                {
                    PhotographerId = demoUser.Id,
                    EventId = cyclingEvent.Id,
                    PhotoId = cyclingPhotos.ElementAtOrDefault(4)?.Id,
                    TotalAmount = 12300m,
                    PlatformCommission = 1845m,
                    MercadoPagoFee = 738m,
                    PhotographerNet = 9717m,
                    Status = "PaidOut",
                    ClearedAt = now.AddDays(-15),
                    PaidOutAt = now.AddDays(-9),
                    CreatedAt = now.AddDays(-18),
                    UpdatedAt = now.AddDays(-9)
                }
            };

            _context.Orders.AddRange(orders);

            _context.PhotoSales.AddRange(
                new PhotoSale
                {
                    UserId = demoUser.Id,
                    PhotographerEventId = runningEvent.Id,
                    Quantity = 5,
                    TotalAmount = 16000m,
                    BuyerName = "Martin Sosa",
                    BuyerEmail = "martin.sosa@example.com",
                    PaymentMethod = "mercadopago",
                    Status = "paid",
                    SoldAt = now.AddDays(-3),
                    CreatedAt = now.AddDays(-3),
                    UpdatedAt = now.AddDays(-3)
                },
                new PhotoSale
                {
                    UserId = demoUser.Id,
                    PhotographerEventId = runningEvent.Id,
                    Quantity = 3,
                    TotalAmount = 9600m,
                    BuyerName = "Sofia Diaz",
                    BuyerEmail = "sofia.diaz@example.com",
                    PaymentMethod = "mercadopago",
                    Status = "paid",
                    SoldAt = now.AddDays(-1),
                    CreatedAt = now.AddDays(-1),
                    UpdatedAt = now.AddDays(-1)
                },
                new PhotoSale
                {
                    UserId = demoUser.Id,
                    PhotographerEventId = cyclingEvent.Id,
                    Quantity = 4,
                    TotalAmount = 16400m,
                    BuyerName = "Equipo Sierra Team",
                    BuyerEmail = "equipo@sierra-team.example",
                    PaymentMethod = "mercadopago",
                    Status = "paid",
                    SoldAt = now.AddDays(-10),
                    CreatedAt = now.AddDays(-10),
                    UpdatedAt = now.AddDays(-10)
                }
            );

            var pendingAmount = orders
                .Where(o => o.Status == "Paid" && o.ClearedAt > now)
                .Sum(o => o.PhotographerNet);

            var availableAmount = orders
                .Where(o => o.Status == "Paid" && o.ClearedAt <= now)
                .Sum(o => o.PhotographerNet);

            var totalWithdrawn = orders
                .Where(o => o.Status == "PaidOut")
                .Sum(o => o.PhotographerNet);

            _context.PhotographerBalances.Add(new PhotographerBalance
            {
                PhotographerId = demoUser.Id,
                PendingAmount = pendingAmount,
                AvailableAmount = availableAmount,
                TotalWithdrawn = totalWithdrawn
            });

            _context.Liquidations.Add(new Liquidation
            {
                PhotographerId = demoUser.Id,
                Amount = totalWithdrawn,
                FromDate = now.AddDays(-30),
                ToDate = now.AddDays(-8),
                CreatedAt = now.AddDays(-8)
            });

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Datos demo para {DemoEmail} generados automáticamente.", DemoAccountDefaults.Email);

            return new DemoSeedResult
            {
                Success = true,
                RebuiltData = true,
                Message = forceReset
                    ? "Datos demo regenerados correctamente."
                    : "Datos demo creados correctamente."
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo crear/actualizar datos demo para {DemoEmail}.", DemoAccountDefaults.Email);
            return new DemoSeedResult
            {
                Success = false,
                RebuiltData = false,
                Message = "No se pudo crear/actualizar datos demo."
            };
        }
    }

    private async Task EnsureDemoAssetsAsync(IEnumerable<DemoPhotoSeed> photoSeeds, CancellationToken cancellationToken)
    {
        foreach (var photo in photoSeeds)
        {
            var originalRelativePath = Path.Combine("events", photo.EventId.ToString(), "originals", photo.Name);
            var thumbRelativePath = Path.Combine("events", photo.EventId.ToString(), "thumbs", photo.Name);
            var watermarkedRelativePath = Path.Combine("events", photo.EventId.ToString(), "watermarked", photo.Name);

            await WriteDemoAssetAsync(originalRelativePath, BuildDemoSvg(photo, includeWatermark: false), cancellationToken);
            await WriteDemoAssetAsync(thumbRelativePath, BuildDemoSvg(photo, includeWatermark: false, compactLayout: true), cancellationToken);
            await WriteDemoAssetAsync(watermarkedRelativePath, BuildDemoSvg(photo, includeWatermark: true), cancellationToken);
        }
    }

    private async Task WriteDemoAssetAsync(string relativePath, string content, CancellationToken cancellationToken)
    {
        var fullPath = Path.Combine(_environment.ContentRootPath, "uploads", relativePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, content, cancellationToken);
    }

    private static string BuildDemoSvg(DemoPhotoSeed photo, bool includeWatermark, bool compactLayout = false)
    {
        var title = WebUtility.HtmlEncode(photo.Title);
        var athlete = WebUtility.HtmlEncode(photo.AthleteLabel);
        var moment = WebUtility.HtmlEncode(photo.MomentLabel);
        var eventDate = WebUtility.HtmlEncode(photo.CreatedAt.ToString("dd MMM yyyy", System.Globalization.CultureInfo.InvariantCulture).ToUpperInvariant());
        var accent = photo.AccentColor;
        var chipOne = WebUtility.HtmlEncode(photo.Tags.Split(',')[0]);
        var chipTwo = WebUtility.HtmlEncode(photo.Tags.Split(',')[1]);
        var chipThree = WebUtility.HtmlEncode(photo.Tags.Split(',')[2]);
        var watermark = includeWatermark
            ? "<text x='1180' y='86' text-anchor='end' fill='rgba(255,255,255,0.55)' font-size='24' font-family='Arial, Helvetica, sans-serif' font-weight='700'>CAPTURAR DEMO</text>"
            : string.Empty;
        var footerHeight = compactLayout ? 220 : 260;
        var titleFont = compactLayout ? 54 : 68;
        var subtitleFont = compactLayout ? 26 : 30;

        return $@"<svg xmlns='http://www.w3.org/2000/svg' width='1400' height='933' viewBox='0 0 1400 933' role='img' aria-label='{title} {athlete}'>
  <defs>
    <linearGradient id='bg' x1='0%' y1='0%' x2='100%' y2='100%'>
      <stop offset='0%' stop-color='#08111d'/>
      <stop offset='55%' stop-color='#12243b'/>
      <stop offset='100%' stop-color='{accent}'/>
    </linearGradient>
    <linearGradient id='overlay' x1='0%' y1='100%' x2='100%' y2='0%'>
      <stop offset='0%' stop-color='rgba(8,17,29,0.94)'/>
      <stop offset='100%' stop-color='rgba(8,17,29,0.18)'/>
    </linearGradient>
    <filter id='blur'>
      <feGaussianBlur stdDeviation='38'/>
    </filter>
  </defs>
  <rect width='1400' height='933' fill='url(#bg)'/>
  <circle cx='238' cy='206' r='158' fill='rgba(255,255,255,0.09)' filter='url(#blur)'/>
  <circle cx='1120' cy='230' r='188' fill='rgba(255,255,255,0.12)' filter='url(#blur)'/>
  <circle cx='1180' cy='700' r='220' fill='rgba(255,255,255,0.08)' filter='url(#blur)'/>
  <rect x='0' y='0' width='1400' height='933' fill='url(#overlay)'/>
  <path d='M86 596 C 300 392, 450 324, 618 314 C 790 304, 918 380, 1082 560 L 1270 740 L 1400 933 L 0 933 Z' fill='rgba(255,255,255,0.08)'/>
  <path d='M642 188 C 682 188, 718 220, 718 260 C 718 290, 702 312, 678 324 L 714 430 L 824 520 L 780 562 L 686 492 L 652 608 L 730 792 L 662 792 L 604 636 L 522 792 L 454 792 L 560 534 L 478 470 L 518 430 L 614 498 L 636 392 L 592 324 C 572 310, 560 286, 560 260 C 560 220, 600 188, 642 188 Z' fill='rgba(255,255,255,0.88)'/>
  <path d='M820 280 C 952 326, 1094 412, 1218 584' stroke='rgba(255,255,255,0.3)' stroke-width='16' stroke-linecap='round' fill='none'/>
  <path d='M168 624 C 294 544, 388 512, 486 500' stroke='rgba(255,255,255,0.18)' stroke-width='12' stroke-linecap='round' fill='none'/>
  <text x='94' y='118' fill='rgba(255,255,255,0.75)' font-size='28' font-family='Arial, Helvetica, sans-serif' font-weight='700'>{eventDate}</text>
  <text x='94' y='164' fill='white' font-size='34' font-family='Arial, Helvetica, sans-serif' font-weight='700'>{title}</text>
  {watermark}
  <text x='94' y='358' fill='rgba(255,255,255,0.18)' font-size='176' font-family='Arial, Helvetica, sans-serif' font-weight='800'>{athlete}</text>
  <rect x='74' y='{933 - footerHeight}' width='1252' height='{footerHeight - 36}' rx='34' fill='rgba(8,17,29,0.62)' stroke='rgba(255,255,255,0.12)'/>
  <text x='118' y='{933 - footerHeight + 92}' fill='white' font-size='{titleFont}' font-family='Arial, Helvetica, sans-serif' font-weight='800'>{moment}</text>
  <text x='118' y='{933 - footerHeight + 142}' fill='rgba(255,255,255,0.7)' font-size='{subtitleFont}' font-family='Arial, Helvetica, sans-serif'>Galeria demo deportiva con tags y resultados consistentes</text>
  <rect x='118' y='{933 - footerHeight + 176}' width='132' height='38' rx='19' fill='rgba(255,255,255,0.12)'/>
  <text x='184' y='{933 - footerHeight + 201}' text-anchor='middle' fill='white' font-size='18' font-family='Arial, Helvetica, sans-serif' font-weight='700'>{chipOne}</text>
  <rect x='266' y='{933 - footerHeight + 176}' width='148' height='38' rx='19' fill='rgba(255,255,255,0.12)'/>
  <text x='340' y='{933 - footerHeight + 201}' text-anchor='middle' fill='white' font-size='18' font-family='Arial, Helvetica, sans-serif' font-weight='700'>{chipTwo}</text>
  <rect x='430' y='{933 - footerHeight + 176}' width='168' height='38' rx='19' fill='rgba(255,255,255,0.12)'/>
  <text x='514' y='{933 - footerHeight + 201}' text-anchor='middle' fill='white' font-size='18' font-family='Arial, Helvetica, sans-serif' font-weight='700'>{chipThree}</text>
  <text x='1180' y='{933 - footerHeight + 200}' text-anchor='end' fill='rgba(255,255,255,0.62)' font-size='22' font-family='Arial, Helvetica, sans-serif' font-weight='700'>solo lectura</text>
</svg>";
    }

    private sealed record DemoPhotoSeed(
        int EventId,
        string Name,
        string Title,
        string AthleteLabel,
        string MomentLabel,
        string Tags,
        string AccentColor,
        DateTime CreatedAt);
}
