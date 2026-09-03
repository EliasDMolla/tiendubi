using Admin.Entities;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Admin.WebApi.HealthChecks
{
    public class PostgresHealthCheck : IHealthCheck
    {
        private readonly IConfiguration _configuration;

        public PostgresHealthCheck(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var connString = ConnectionStringResolver.ResolvePostgresConnectionString(_configuration);
                await using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync(cancellationToken);

                await using var cmd = new NpgsqlCommand("SELECT 1", conn);
                var result = await cmd.ExecuteScalarAsync(cancellationToken);

                return result is not null
                    ? HealthCheckResult.Healthy("PostgreSQL disponible")
                    : HealthCheckResult.Unhealthy("PostgreSQL respondió sin resultado");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("PostgreSQL no disponible", ex);
            }
        }
    }
}
