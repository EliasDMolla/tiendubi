using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Net.Sockets;

namespace Admin.WebApi.HealthChecks
{
    public class SmtpHealthCheck : IHealthCheck
    {
        private readonly IConfiguration _configuration;

        public SmtpHealthCheck(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            var smtpHost = _configuration["Email:SmtpHost"];
            var smtpPortRaw = _configuration["Email:SmtpPort"];

            if (string.IsNullOrWhiteSpace(smtpHost) || !int.TryParse(smtpPortRaw, out var smtpPort))
            {
                return HealthCheckResult.Degraded("SMTP no configurado");
            }

            try
            {
                using var tcpClient = new TcpClient();
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

                await tcpClient.ConnectAsync(smtpHost, smtpPort, timeoutCts.Token);
                return HealthCheckResult.Healthy("SMTP disponible");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("SMTP no disponible", ex);
            }
        }
    }
}
