using Admin.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Admin.WebApi.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
        {
            var photographerId = GetPhotographerId();
            if (photographerId == null)
            {
                return Unauthorized();
            }

            var summary = await _dashboardService.GetDashboardSummaryAsync(photographerId.Value, cancellationToken);
            return Ok(summary);
        }

        [HttpGet("sales-by-event")]
        public async Task<IActionResult> GetSalesByEvent(CancellationToken cancellationToken)
        {
            var photographerId = GetPhotographerId();
            if (photographerId == null)
            {
                return Unauthorized();
            }

            var salesByEvent = await _dashboardService.GetSalesByEventAsync(photographerId.Value, cancellationToken);
            return Ok(salesByEvent);
        }

        [HttpGet("sale-details")]
        public async Task<IActionResult> GetSaleDetails([FromQuery] int take = 50, CancellationToken cancellationToken = default)
        {
            var photographerId = GetPhotographerId();
            if (photographerId == null)
            {
                return Unauthorized();
            }

            var saleDetails = await _dashboardService.GetRecentSaleDetailsAsync(photographerId.Value, take, cancellationToken);
            return Ok(saleDetails);
        }

        private int? GetPhotographerId()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userId, out var parsed) ? parsed : null;
        }
    }
}