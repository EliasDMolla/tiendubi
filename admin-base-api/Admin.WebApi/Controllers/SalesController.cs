using Admin.WebApi.Models;
using Admin.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Admin.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SalesController : ControllerBase
    {
        private readonly ISalesService _salesService;
        private readonly IPhotoCheckoutService _photoCheckoutService;

        public SalesController(ISalesService salesService, IPhotoCheckoutService photoCheckoutService)
        {
            _salesService = salesService;
            _photoCheckoutService = photoCheckoutService;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSalesSummary(CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var summary = await _salesService.GetSalesSummaryAsync(userId.Value, cancellationToken);
            return Ok(summary);
        }

        [HttpGet("list")]
        public async Task<IActionResult> GetSalesList([FromQuery] SalesListQuery query, CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var result = await _salesService.GetSalesListAsync(userId.Value, query, cancellationToken);
            return Ok(result);
        }

        [HttpGet("liquidations")]
        public async Task<IActionResult> GetLiquidations(CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var liquidations = await _salesService.GetLiquidationsAsync(userId.Value, cancellationToken);
            return Ok(liquidations);
        }

        [HttpPost("withdraw")]
        public async Task<IActionResult> WithdrawAvailable(CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var result = await _salesService.WithdrawAvailableAsync(userId.Value, cancellationToken);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpPost("approve-transfer/{externalReference}")]
        public async Task<IActionResult> ApproveTransfer(string externalReference, CancellationToken cancellationToken)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized();

            var result = await _photoCheckoutService.ApproveTransferAsync(userId.Value, externalReference, cancellationToken);
            if (!result.Success)
                return BadRequest(result);

            return Ok(result);
        }

        private int? GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(claim, out var id))
                return id;
            return null;
        }
    }
}