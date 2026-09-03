using Admin.WebApi.Infrastructure;

namespace Admin.WebApi.Infrastructure;

public sealed class ReadOnlyDemoMiddleware
{
    private static readonly HashSet<string> AllowedWritePaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/logout",
        "/api/auth/refresh-token",
        "/api/auth/revoke-token"
    };

    private static readonly HashSet<string> BlockedReadOnlyPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/payments/mercadopago/connect"
    };

    private readonly RequestDelegate _next;

    public ReadOnlyDemoMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsBlockedWriteAttempt(context))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new
            {
                message = "La cuenta demo esta en modo solo lectura.",
                code = "demo_read_only"
            });
            return;
        }

        await _next(context);
    }

    private static bool IsBlockedWriteAttempt(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (BlockedReadOnlyPaths.Contains(path))
        {
            return true;
        }

        if (HttpMethods.IsGet(context.Request.Method)
            || HttpMethods.IsHead(context.Request.Method)
            || HttpMethods.IsOptions(context.Request.Method))
        {
            return false;
        }

        if (AllowedWritePaths.Contains(path))
        {
            return false;
        }

        var readOnlyClaim = context.User.FindFirst(DemoAccountDefaults.ReadOnlyClaimType)?.Value;
        return string.Equals(readOnlyClaim, bool.TrueString, StringComparison.OrdinalIgnoreCase);
    }
}