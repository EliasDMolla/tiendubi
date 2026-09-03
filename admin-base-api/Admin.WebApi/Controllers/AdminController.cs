using Admin.Entities.Entities;
using Admin.WebApi.Filters;
using Admin.WebApi.Models;
using Admin.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Admin.WebApi.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize]
    [RequiresAdmin(requiresOwnerEmail: true)]
    public class AdminController : ControllerBase
    {
        private readonly IAdminService _adminService;
        private readonly IDemoDataService _demoDataService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(IAdminService adminService, IDemoDataService demoDataService, ILogger<AdminController> logger)
        {
            _adminService = adminService;
            _demoDataService = demoDataService;
            _logger = logger;
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(userIdClaim, out var userId) ? userId : 0;
        }

        private string GetIpAddress()
        {
            return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        // ========================================
        // Dashboard
        // ========================================

        /// <summary>
        /// Obtener métricas del dashboard de administración
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            try
            {
                var metrics = await _adminService.GetDashboardMetricsAsync();
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo métricas del dashboard admin");
                return StatusCode(500, new { message = "Error obteniendo métricas" });
            }
        }

        // ========================================
        // Owner Finance
        // ========================================

        [HttpGet("owner/sales-summary")]
        public async Task<IActionResult> GetOwnerSalesSummary(CancellationToken cancellationToken)
        {
            try
            {
                var summary = await _adminService.GetOwnerGlobalSalesSummaryAsync(cancellationToken);
                return Ok(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo ventas globales owner");
                return StatusCode(500, new { message = "Error obteniendo ventas globales" });
            }
        }

        [HttpGet("owner/transfer-approvals")]
        public async Task<IActionResult> GetPendingTransferApprovals(CancellationToken cancellationToken)
        {
            try
            {
                var pending = await _adminService.GetPendingTransferApprovalsAsync(cancellationToken);
                return Ok(pending);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo transferencias pendientes para aprobación");
                return StatusCode(500, new { message = "Error obteniendo transferencias pendientes" });
            }
        }

        [HttpGet("owner/accreditations")]
        public async Task<IActionResult> GetOwnerAccreditations(CancellationToken cancellationToken)
        {
            try
            {
                var result = await _adminService.GetOwnerAccreditationsAsync(cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo acreditaciones owner");
                return StatusCode(500, new { message = "Error obteniendo acreditaciones" });
            }
        }

        [HttpPost("owner/accreditations/{photographerId}/mark-paidout")]
        public async Task<IActionResult> MarkAccreditationPaidOut(int photographerId, [FromBody] MarkAccreditationPaidOutRequest? request, CancellationToken cancellationToken)
        {
            try
            {
                var adminId = GetCurrentUserId();
                if (adminId <= 0)
                    return Unauthorized();

                var (success, message) = await _adminService.MarkPhotographerAccreditationPaidOutAsync(
                    adminId,
                    photographerId,
                    request?.Note,
                    GetIpAddress(),
                    cancellationToken);

                if (!success)
                    return BadRequest(new { message });

                return Ok(new { message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registrando acreditación pagada para fotógrafo {PhotographerId}", photographerId);
                return StatusCode(500, new { message = "Error registrando acreditación" });
            }
        }

        [HttpPost("owner/transfer-approvals/{externalReference}/approve")]
        public async Task<IActionResult> ApproveTransferSale(string externalReference, [FromBody] ApproveTransferSaleRequest? request, CancellationToken cancellationToken)
        {
            try
            {
                var adminId = GetCurrentUserId();
                if (adminId <= 0)
                    return Unauthorized();

                var clearanceHours = request?.ClearanceHours ?? 72;
                var (success, message) = await _adminService.ApproveTransferSaleAsync(adminId, externalReference, clearanceHours, GetIpAddress(), cancellationToken);

                if (!success)
                    return BadRequest(new { message });

                return Ok(new { message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error aprobando transferencia {ExternalReference}", externalReference);
                return StatusCode(500, new { message = "Error aprobando transferencia" });
            }
        }

        [HttpGet("owner/photo-deliveries/failed")]
        public async Task<IActionResult> GetFailedPhotoDeliveries(CancellationToken cancellationToken)
        {
            try
            {
                var result = await _adminService.GetFailedPhotoDeliveriesAsync(cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo entregas de fotos fallidas");
                return StatusCode(500, new { message = "Error obteniendo entregas fallidas" });
            }
        }

        [HttpPost("owner/photo-deliveries/{externalReference}/retry")]
        public async Task<IActionResult> RetryPhotoDelivery(string externalReference, CancellationToken cancellationToken)
        {
            try
            {
                var (success, message) = await _adminService.RetryPhotoDeliveryAsync(externalReference, cancellationToken);

                if (!success)
                    return BadRequest(new { message });

                return Ok(new { message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reintentando entrega de fotos para {ExternalReference}", externalReference);
                return StatusCode(500, new { message = "Error reintentando entrega" });
            }
        }

        // ========================================
        // User Management
        // ========================================

        /// <summary>
        /// Listar usuarios con paginación y filtros
        /// </summary>
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? plan = null,
            [FromQuery] string? role = null,
            [FromQuery] string? status = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortDir = null)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 20;

                var result = await _adminService.GetUsersAsync(page, pageSize, search, plan, role, status, sortBy, sortDir);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listando usuarios");
                return StatusCode(500, new { message = "Error listando usuarios" });
            }
        }

        /// <summary>
        /// Ver detalle de un usuario específico
        /// </summary>
        [HttpGet("users/{id}")]
        public async Task<IActionResult> GetUserDetail(int id)
        {
            try
            {
                var user = await _adminService.GetUserDetailAsync(id);
                if (user == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error obteniendo detalle del usuario {id}");
                return StatusCode(500, new { message = "Error obteniendo detalle del usuario" });
            }
        }

        /// <summary>
        /// Activar o desactivar un usuario
        /// </summary>
        [HttpPut("users/{id}/status")]
        public async Task<IActionResult> ToggleUserStatus(int id, [FromBody] ToggleUserStatusRequest request)
        {
            try
            {
                var adminId = GetCurrentUserId();
                
                if (id == adminId)
                    return BadRequest(new { message = "No podés desactivarte a vos mismo" });

                var success = await _adminService.ToggleUserStatusAsync(adminId, id, request.Activate, GetIpAddress());
                
                if (!success)
                    return NotFound(new { message = "Usuario no encontrado" });

                return Ok(new { message = request.Activate ? "Usuario activado" : "Usuario desactivado" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cambiando estado del usuario {id}");
                return StatusCode(500, new { message = "Error cambiando estado del usuario" });
            }
        }

        /// <summary>
        /// Cambiar rol de un usuario
        /// </summary>
        [HttpPut("users/{id}/role")]
        [RequiresAdmin(requiresSuperAdmin: true)]
        public async Task<IActionResult> ChangeUserRole(int id, [FromBody] ChangeUserRoleRequest request)
        {
            try
            {
                var adminId = GetCurrentUserId();
                
                if (id == adminId)
                    return BadRequest(new { message = "No podés cambiar tu propio rol" });

                if (!Enum.TryParse<UserRole>(request.Role, true, out var newRole))
                    return BadRequest(new { message = "Rol inválido. Valores válidos: User, Admin, SuperAdmin" });

                var success = await _adminService.ChangeUserRoleAsync(adminId, id, newRole, GetIpAddress());
                
                if (!success)
                    return NotFound(new { message = "Usuario no encontrado" });

                return Ok(new { message = $"Rol cambiado a {newRole}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cambiando rol del usuario {id}");
                return StatusCode(500, new { message = "Error cambiando rol" });
            }
        }

        /// <summary>
        /// Cambiar plan de un usuario manualmente
        /// </summary>
        [HttpPut("users/{id}/plan")]
        public async Task<IActionResult> ChangeUserPlan(int id, [FromBody] ChangeUserPlanRequest request)
        {
            try
            {
                var adminId = GetCurrentUserId();

                if (!Enum.TryParse<PlanType>(request.PlanType, true, out var newPlan))
                    return BadRequest(new { message = "Plan inválido. Valores válidos: FREE, PRO_TRIAL, PRO" });

                var success = await _adminService.ChangeUserPlanAsync(adminId, id, newPlan, GetIpAddress());
                
                if (!success)
                    return NotFound(new { message = "Usuario no encontrado" });

                return Ok(new { message = $"Plan cambiado a {newPlan}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cambiando plan del usuario {id}");
                return StatusCode(500, new { message = "Error cambiando plan" });
            }
        }

        // ========================================
        // User Deletion
        // ========================================

        /// <summary>
        /// Eliminar un usuario completamente (irreversible)
        /// </summary>
        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var adminId = GetCurrentUserId();

                if (id == adminId)
                    return BadRequest(new { message = "No podés eliminarte a vos mismo" });

                var (success, message) = await _adminService.DeleteUserAsync(adminId, id, GetIpAddress());

                if (!success)
                    return BadRequest(new { message });

                return Ok(new { message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error eliminando usuario {id}");
                return StatusCode(500, new { message = "Error eliminando usuario" });
            }
        }

        // ========================================
        // Audit
        // ========================================

        /// <summary>
        /// Obtener acciones administrativas recientes
        /// </summary>
        [HttpGet("actions")]
        public async Task<IActionResult> GetRecentActions([FromQuery] int count = 20)
        {
            try
            {
                if (count < 1 || count > 100) count = 20;
                var actions = await _adminService.GetRecentActionsAsync(count);
                return Ok(actions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo acciones administrativas");
                return StatusCode(500, new { message = "Error obteniendo acciones" });
            }
        }

        /// <summary>
        /// Regenerar datos demo de demo1802 (usuario + eventos + fotos + ventas)
        /// </summary>
        [HttpPost("demo/reset")]
        [RequiresAdmin(requiresSuperAdmin: true)]
        public async Task<IActionResult> ResetDemoData(CancellationToken cancellationToken)
        {
            try
            {
                var result = await _demoDataService.SeedDemoPhotographerAsync(forceReset: true, cancellationToken);

                if (!result.Success)
                {
                    return StatusCode(500, new { message = result.Message });
                }

                return Ok(new
                {
                    message = result.Message,
                    credentials = new
                    {
                        email = result.Email,
                        password = result.Password,
                        slug = result.PublicSlug
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error regenerando datos demo");
                return StatusCode(500, new { message = "Error regenerando datos demo" });
            }
        }
    }
}
