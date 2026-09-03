using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace Admin.WebApi.Filters
{
    /// <summary>
    /// Atributo para marcar endpoints que requieren rol Admin o SuperAdmin
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class RequiresAdminAttribute : Attribute, IAsyncActionFilter
    {
        public bool RequiresSuperAdmin { get; }
        public bool RequiresOwnerEmail { get; }

        public RequiresAdminAttribute(bool requiresSuperAdmin = false, bool requiresOwnerEmail = false)
        {
            RequiresSuperAdmin = requiresSuperAdmin;
            RequiresOwnerEmail = requiresOwnerEmail;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (RequiresOwnerEmail)
            {
                var configuration = context.HttpContext.RequestServices.GetService(typeof(IConfiguration)) as IConfiguration;
                var configuredOwnerEmail = (configuration?["OwnerSecurity:OwnerEmail"] ?? string.Empty).Trim().ToLowerInvariant();
                var currentUserEmail = (context.HttpContext.User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty).Trim().ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(configuredOwnerEmail) || currentUserEmail != configuredOwnerEmail)
                {
                    context.Result = new ObjectResult(new { message = "Acceso restringido al usuario propietario del sistema" })
                    {
                        StatusCode = 403
                    };
                    return;
                }

                await next();
                return;
            }

            var roleClaim = context.HttpContext.User.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(roleClaim))
            {
                context.Result = new ForbidResult();
                return;
            }

            var isAdmin = roleClaim == "Admin" || roleClaim == "SuperAdmin";
            var isSuperAdmin = roleClaim == "SuperAdmin";

            if (!isAdmin || (RequiresSuperAdmin && !isSuperAdmin))
            {
                context.Result = new ObjectResult(new { message = "No tenés permisos para acceder a esta funcionalidad" })
                {
                    StatusCode = 403
                };
                return;
            }

            await next();
        }
    }
}
