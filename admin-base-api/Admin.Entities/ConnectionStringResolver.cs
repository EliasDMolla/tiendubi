using Microsoft.Extensions.Configuration;

namespace Admin.Entities
{
    public static class ConnectionStringResolver
    {
        public static string ResolvePostgresConnectionString(IConfiguration configuration)
        {
            var baseConnectionString = configuration.GetConnectionString("PostgresConnection")
                ?? configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("No se encontró la connection string. Definí 'ConnectionStrings:PostgresConnection' o 'ConnectionStrings:DefaultConnection'.");

            return baseConnectionString;
        }
    }
}
