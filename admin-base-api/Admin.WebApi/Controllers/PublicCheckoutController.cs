using Admin.WebApi.Models;
using Admin.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Admin.WebApi.Controllers
{
    [ApiController]
    [Route("api/public/checkout")]
    [AllowAnonymous]
    public class PublicCheckoutController : ControllerBase
    {
        private readonly IPhotoCheckoutService _photoCheckoutService;

        public PublicCheckoutController(IPhotoCheckoutService photoCheckoutService)
        {
            _photoCheckoutService = photoCheckoutService;
        }

        [HttpPost("{slug}/events/{eventId:int}/mercadopago")]
        public async Task<IActionResult> CreateMercadoPagoCheckout(string slug, int eventId, [FromBody] PublicPhotoCheckoutRequest request, CancellationToken cancellationToken)
        {
            var response = await _photoCheckoutService.CreateCheckoutAsync(slug, eventId, request, cancellationToken);
            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost("{slug}/events/{eventId:int}/transfer")]
        public async Task<IActionResult> CreateTransferCheckout(string slug, int eventId, [FromBody] PublicPhotoCheckoutRequest request, CancellationToken cancellationToken)
        {
            var response = await _photoCheckoutService.CreateTransferCheckoutAsync(slug, eventId, request, cancellationToken);
            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost("{slug}/events/{eventId:int}/free")]
        public async Task<IActionResult> CreateFreeCheckout(string slug, int eventId, [FromBody] PublicPhotoCheckoutRequest request, CancellationToken cancellationToken)
        {
            var response = await _photoCheckoutService.CreateFreeCheckoutAsync(slug, eventId, request, cancellationToken);
            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost("transfer/{externalReference}/receipt")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> UploadTransferReceipt(string externalReference, [FromForm] PublicTransferReceiptUploadRequest request, CancellationToken cancellationToken)
        {
            if (request.Receipt == null || request.Receipt.Length == 0)
            {
                return BadRequest(new PublicTransferReceiptResponse
                {
                    Success = false,
                    Message = "Debes adjuntar el comprobante de transferencia"
                });
            }

            var response = await _photoCheckoutService.SubmitTransferReceiptAsync(externalReference, request.Receipt, cancellationToken);
            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpGet("{externalReference}/status")]
        public async Task<IActionResult> GetCheckoutStatus(string externalReference, [FromQuery] string buyerEmail, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(buyerEmail))
            {
                return BadRequest(new PublicCheckoutStatusResponse
                {
                    Success = false,
                    Message = "Debes indicar el email del comprador"
                });
            }

            var response = await _photoCheckoutService.GetCheckoutStatusAsync(externalReference, buyerEmail, cancellationToken);
            if (!response.Success)
            {
                return NotFound(response);
            }

            return Ok(response);
        }
    }
}
