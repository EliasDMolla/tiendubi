using System.Security.Claims;
using Admin.WebApi.Models.Payments;
using Admin.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Admin.WebApi.Controllers
{
    [ApiController]
    [Route("api/payments/mercadopago")]
    public class PaymentsMercadoPagoController : ControllerBase
    {
        private readonly IMercadoPagoService _mercadoPagoService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentsMercadoPagoController> _logger;

        public PaymentsMercadoPagoController(
            IMercadoPagoService mercadoPagoService,
            IConfiguration configuration,
            ILogger<PaymentsMercadoPagoController> logger)
        {
            _mercadoPagoService = mercadoPagoService;
            _configuration = configuration;
            _logger = logger;
        }

        [Authorize]
        [HttpGet("connect")]
        public async Task<IActionResult> Connect(CancellationToken cancellationToken)
        {
            var photographerId = GetCurrentUserId();
            if (photographerId <= 0)
                return Unauthorized(new { message = "Usuario no autenticado" });

            var response = await _mercadoPagoService.BuildConnectUrlAsync(photographerId, cancellationToken);
            return Ok(response);
        }

        [AllowAnonymous]
        [HttpGet("callback")]
        public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state, [FromQuery] string? error, CancellationToken cancellationToken)
        {
            var frontendBase = (_configuration["AppSettings:FrontendUrl"] ?? "http://localhost:4200").TrimEnd('/');

            if (!string.IsNullOrWhiteSpace(error))
            {
                var deniedUrl = $"{frontendBase}/mercadopago/callback?status=error&message={Uri.EscapeDataString("El usuario canceló la autorización en MercadoPago.")}";
                return Redirect(deniedUrl);
            }

            var result = await _mercadoPagoService.HandleOAuthCallbackAsync(code, state, cancellationToken);
            var status = result.Success ? "success" : "error";
            var redirectUrl = $"{frontendBase}/mercadopago/callback?status={status}&message={Uri.EscapeDataString(result.Message)}";

            return Redirect(redirectUrl);
        }

        [Authorize]
        [HttpGet("status")]
        public async Task<IActionResult> Status(CancellationToken cancellationToken)
        {
            var photographerId = GetCurrentUserId();
            if (photographerId <= 0)
                return Unauthorized(new { message = "Usuario no autenticado" });

            var status = await _mercadoPagoService.GetConnectionStatusAsync(photographerId, cancellationToken);
            return Ok(status);
        }

        [Authorize]
        [HttpPost("payment")]
        public async Task<IActionResult> CreatePayment([FromBody] MercadoPagoCreatePaymentRequest request, CancellationToken cancellationToken)
        {
            var photographerId = GetCurrentUserId();
            if (photographerId <= 0)
                return Unauthorized(new { message = "Usuario no autenticado" });

            if (request.PhotographerId != photographerId)
                return Forbid();

            var response = await _mercadoPagoService.CreatePaymentAsync(request, cancellationToken);
            if (!response.Success)
                return BadRequest(response);

            return Ok(response);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }
    }
}
