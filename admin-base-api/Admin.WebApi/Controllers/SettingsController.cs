using Admin.WebApi.Models;
using Admin.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Admin.WebApi.Controllers
{
    [ApiController]
    [Route("api/settings")]
    public class SettingsController : ControllerBase
    {
        private readonly PaymentSettings _paymentSettings;
        private readonly FeatureSettings _featureSettings;

        public SettingsController(IOptions<PaymentSettings> paymentSettings, IOptions<FeatureSettings> featureSettings)
        {
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
    }
}
