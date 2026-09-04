using Admin.WebApi.Models;
using Admin.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Admin.WebApi.Controllers
{
    [ApiController]
    [Route("api/subscription")]
    [Authorize]
    public class SubscriptionController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<SubscriptionController> _logger;

        public SubscriptionController(ISubscriptionService subscriptionService, ILogger<SubscriptionController> logger)
        {
            _subscriptionService = subscriptionService;
            _logger = logger;
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus()
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            var status = await _subscriptionService.GetPlanStatusAsync(userId);
            if (status == null) return NotFound(new { message = "Usuario no encontrado" });

            return Ok(status);
        }

        [HttpPost("activate-trial")]
        public async Task<IActionResult> ActivateTrial()
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            var response = await _subscriptionService.ActivateTrialAsync(userId);
            if (!response.Success) return BadRequest(response);

            return Ok(response);
        }

        [HttpPost("mercadopago/checkout")]
        public async Task<IActionResult> CreateMercadoPagoCheckout([FromBody] CreateMercadoPagoCheckoutRequest? request)
        {
            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            var response = await _subscriptionService.CreateMercadoPagoCheckoutAsync(userId, request?.Months ?? 1);
            if (!response.Success) return BadRequest(response);

            return Ok(response);
        }

        [HttpPost("mercadopago/confirm")]
        public async Task<IActionResult> ConfirmMercadoPagoPayment([FromBody] ConfirmMercadoPagoPaymentRequest request)
        {
            if (request.MerchantOrderId <= 0)
            {
                return BadRequest(new ConfirmMercadoPagoPaymentResponse
                {
                    Success = false,
                    Message = "merchant_order_id inválido"
                });
            }

            var userId = GetCurrentUserId();
            if (userId <= 0) return Unauthorized();

            var response = await _subscriptionService.ConfirmMercadoPagoPaymentAsync(userId, request.MerchantOrderId);
            if (!response.Success) return BadRequest(response);

            return Ok(response);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }
    }
}
