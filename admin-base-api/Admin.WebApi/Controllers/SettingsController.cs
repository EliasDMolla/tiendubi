using System.Security.Claims;
using Admin.Entities;
using Admin.Entities.Entities;
using Admin.WebApi.Models;
using Admin.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Admin.WebApi.Controllers
{
    [ApiController]
    [Route("api/settings")]
    public class SettingsController : ControllerBase
    {
        private readonly Context _context;
        private readonly PaymentSettings _paymentSettings;
        private readonly FeatureSettings _featureSettings;

        public SettingsController(
            Context context,
            IOptions<PaymentSettings> paymentSettings,
            IOptions<FeatureSettings> featureSettings)
        {
            _context = context;
            _paymentSettings = paymentSettings.Value;
            _featureSettings = featureSettings.Value;
        }

        [AllowAnonymous]
        [HttpGet("payment")]
        public IActionResult GetPaymentSettings()
        {
            return Ok(new PaymentPublicSettings
            {
                Enabled = _paymentSettings.Enabled,
                MercadoPagoEnabled = _paymentSettings.MercadoPagoEnabled,
                TransfersEnabled = _paymentSettings.TransfersEnabled,
                CommissionPercent = _paymentSettings.CommissionPercent,
                DiscountCode = _paymentSettings.DiscountCode,
                DiscountPercent = _paymentSettings.DiscountPercent
            });
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult GetLegacyPublicSettings()
        {
            return GetPublicSettings();
        }

        [AllowAnonymous]
        [HttpGet("public")]
        public IActionResult GetPublicSettings()
        {
            return Ok(new PublicSettingsResponse
            {
                Payment = new PaymentPublicSettings
                {
                    Enabled = _paymentSettings.Enabled,
                    MercadoPagoEnabled = _paymentSettings.MercadoPagoEnabled,
                    TransfersEnabled = _paymentSettings.TransfersEnabled,
                    CommissionPercent = _paymentSettings.CommissionPercent,
                    DiscountCode = _paymentSettings.DiscountCode,
                    DiscountPercent = _paymentSettings.DiscountPercent
                },
                Features = new FeaturePublicSettings
                {
                    RegistrationEnabled = _featureSettings.RegistrationEnabled,
                    PhotoUploadEnabled = _featureSettings.PhotoUploadEnabled
                }
            });
        }

        [Authorize]
        [HttpGet("site-theme")]
        public async Task<IActionResult> GetSiteTheme(CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0)
                return Unauthorized(new { message = "Usuario no autenticado" });

            var themeJson = await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.PublicSiteThemeJson)
                .FirstOrDefaultAsync(cancellationToken);

            return Ok(SiteThemeStore.Normalize(SiteThemeStore.Parse(themeJson)));
        }

        [Authorize]
        [HttpPut("site-theme")]
        public async Task<IActionResult> SaveSiteTheme([FromBody] SiteThemeDto request, CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0)
                return Unauthorized(new { message = "Usuario no autenticado" });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user == null)
                return NotFound(new { message = "Usuario no encontrado" });

            var isAdmin = user.Role == UserRole.Admin || user.Role == UserRole.SuperAdmin;
            if (!isAdmin && !user.IsProActive)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "Personalizar los colores del sitio es exclusivo del plan Pro."
                });
            }

            var theme = SiteThemeStore.Normalize(request);
            user.PublicSiteThemeJson = SiteThemeStore.Serialize(theme);
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(theme);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }
    }
}
