using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Admin.Entities
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<Context>
    {
        public Context CreateDbContext(string[] args)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var appSettingsPath = ResolveAppSettingsPath(currentDirectory);

            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.GetDirectoryName(appSettingsPath)!)
                .AddJsonFile(Path.GetFileName(appSettingsPath), optional: false)
                .AddEnvironmentVariables()
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<Context>();
            var connectionString = ConnectionStringResolver.ResolvePostgresConnectionString(configuration);

            optionsBuilder.UseNpgsql(connectionString); 

            return new Context(optionsBuilder.Options);
        }

        private static string ResolveAppSettingsPath(string currentDirectory)
        {
            var candidates = new[]
            {
                Path.Combine(currentDirectory, "appsettings.json"),
                Path.Combine(currentDirectory, "..", "Admin.WebApi", "appsettings.json")
            };

            foreach (var path in candidates)
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(fullPath))
                    return fullPath;
            }

            throw new FileNotFoundException("No se encontró appsettings.json para el design-time DbContext. Verificá que exista en Admin.WebApi.");
        }
    }
}
