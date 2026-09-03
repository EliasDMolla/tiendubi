using Admin.Entities;
using Admin.Entities.Entities;
using Admin.WebApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Admin.WebApi.HealthChecks;
using Admin.WebApi.Infrastructure;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.DataProtection;
using System.Text;
using Admin.WebApi.Infrastructure.MercadoPago;
using Admin.WebApi.Repositories;

var builder = WebApplication.CreateBuilder(args);

// ---------------------
// Forwarded Headers (para HTTPS detrás de proxy - Render)
// ---------------------
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ---------------------
// Data Protection
// ---------------------
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Environment.GetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH")
    ?? "/app/keys";
Directory.CreateDirectory(dataProtectionKeysPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));

// ---------------------
// EF Core PostgreSQL
// ---------------------
var postgresConnectionString = ConnectionStringResolver.ResolvePostgresConnectionString(builder.Configuration);

builder.Services.AddDbContext<Context>(options =>
    options.UseNpgsql(postgresConnectionString, npgsqlOptions =>
    {
        npgsqlOptions.MigrationsAssembly(typeof(Context).Assembly.GetName().Name);
        npgsqlOptions.CommandTimeout(600);
    }));

// ---------------------
// Servicios
// ---------------------
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Tiendubi API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header usando el esquema Bearer. Ejemplo: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<IMercadoPagoClient, MercadoPagoClient>(client =>
{
    client.BaseAddress = new Uri("https://api.mercadopago.com/");
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres")
    .AddCheck<SmtpHealthCheck>("smtp");

// ---------------------
// CORS flexible en desarrollo
// ---------------------
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
            policy.WithOrigins(
                "http://localhost:4200",
                "https://localhost:4200")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials());
    });
}
else
{
    var allowedOrigins = GetAllowedCorsOrigins(builder.Configuration);

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials());
    });
}

// ---------------------
// JWT Authentication
// ---------------------
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? "your-super-secret-key-change-this-in-production-min-32-chars";
var key = Encoding.ASCII.GetBytes(jwtSecret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };
})
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Cookie.SameSite = builder.Environment.IsDevelopment() 
        ? SameSiteMode.Lax 
        : SameSiteMode.None; // None para cross-origin en producción
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.None
        : CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Google OAuth opcional
var googleClientId = builder.Configuration["Google:ClientId"];
var googleClientSecret = builder.Configuration["Google:ClientSecret"];
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId;
            options.ClientSecret = googleClientSecret;
            options.CallbackPath = "/signin-google"; // Middleware intercepta esta ruta (NO es un endpoint del controller)
            options.SaveTokens = true;
            options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.CorrelationCookie.SameSite = builder.Environment.IsDevelopment() 
                ? SameSiteMode.Lax 
                : SameSiteMode.None;
            options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.Always;
        });
}

builder.Services.AddAuthorization();

builder.Services.Configure<FeatureSettings>(builder.Configuration.GetSection("Features"));
builder.Services.Configure<PaymentSettings>(builder.Configuration.GetSection("Payment"));
builder.Services.Configure<MercadoPagoSettings>(builder.Configuration.GetSection("MercadoPagoSettings"));
builder.Services.Configure<R2StorageOptions>(builder.Configuration.GetSection("R2"));
builder.Services.Configure<PhotoDeliveryRetrySettings>(builder.Configuration.GetSection("PhotoDeliveryRetry"));

// ---------------------
// Servicios de negocio
// ---------------------
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<ISalesService, SalesService>();
builder.Services.AddScoped<IPhotoCheckoutService, PhotoCheckoutService>();
builder.Services.AddScoped<IPhotoDeliveryService, PhotoDeliveryService>();
builder.Services.AddScoped<IDemoDataService, DemoDataService>();
builder.Services.AddScoped<IR2StorageService, R2StorageService>();
builder.Services.AddScoped<IMercadoPagoService, MercadoPagoService>();
builder.Services.AddScoped<IPhotographerMercadoPagoAccountRepository, PhotographerMercadoPagoAccountRepository>();
builder.Services.AddSingleton<IMercadoPagoOAuthStateService, MercadoPagoOAuthStateService>();
builder.Services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
builder.Services.AddScoped<IPhotoImageProcessor, PhotoImageProcessor>();
builder.Services.AddSingleton<IPhotoProcessingQueue, PhotoProcessingQueue>();
builder.Services.AddHostedService<PhotoProcessingWorker>();
builder.Services.AddHostedService<PhotoDeliveryRetryWorker>();

// ---------------------
// Puerto dinámico (Docker, Render o IIS)
// ---------------------
var configuredUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
if (string.IsNullOrWhiteSpace(configuredUrls))
{
    var port = Environment.GetEnvironmentVariable("PORT");
    configuredUrls = string.IsNullOrWhiteSpace(port)
        ? "https://localhost:44349;http://localhost:44348"
        : $"http://0.0.0.0:{port}";
}
builder.WebHost.UseUrls(configuredUrls);

var app = builder.Build();

var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadsPath);

await EnsureDatabaseMigratedAsync(app);
await EnsureDefaultAdminUserAsync(app);
await EnsureConfiguredOwnerUserAsync(app);
await EnsureDemoPhotographerDataAsync(app);

// ---------------------
// Configurar pipeline HTTP
// ---------------------
app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (path.StartsWith("/uploads/events/", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/uploads/comprobantes/", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsync("Acceso denegado");
        return;
    }

    await next();
});

app.UseCors("AllowAll");
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<ReadOnlyDemoMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();

static string[] GetAllowedCorsOrigins(IConfiguration configuration)
{
    var defaultOrigins = new[]
    {
        "https://capturar.netlify.app",
        "https://capturar.ordenapp.ar",
        "http://localhost:4200",
        "https://localhost:4200"
    };

    var rawOrigins = configuration["Cors:AllowedOrigins"];

    var parsedOrigins = (rawOrigins ?? string.Empty)
        .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    foreach (var defaultOrigin in defaultOrigins)
    {
        if (!parsedOrigins.Contains(defaultOrigin, StringComparer.OrdinalIgnoreCase))
        {
            parsedOrigins.Add(defaultOrigin);
        }
    }

    var frontendUrl = configuration["AppSettings:FrontendUrl"];
    if (!string.IsNullOrWhiteSpace(frontendUrl) &&
        !parsedOrigins.Contains(frontendUrl, StringComparer.OrdinalIgnoreCase))
    {
        parsedOrigins.Add(frontendUrl);
    }

    return parsedOrigins.ToArray();
}

static async Task EnsureDatabaseMigratedAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<Context>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    const int maxAttempts = 12;
    var delay = TimeSpan.FromSeconds(5);

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            logger.LogInformation("Aplicando migraciones de base de datos. Attempt={Attempt}/{MaxAttempts}", attempt, maxAttempts);

            var availableMigrations = context.Database.GetMigrations().ToList();
            if (availableMigrations.Count == 0)
            {
                logger.LogWarning("No se detectaron migraciones en runtime. Ejecutando EnsureCreated como fallback para crear esquema.");
                await context.Database.EnsureCreatedAsync();
                logger.LogInformation("Esquema creado con EnsureCreated.");
                return;
            }

            await context.Database.MigrateAsync();
            logger.LogInformation("Migraciones aplicadas correctamente.");
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            logger.LogWarning(ex, "No se pudo aplicar migraciones (intento {Attempt}/{MaxAttempts}). Reintentando en {DelaySeconds}s...", attempt, maxAttempts, delay.TotalSeconds);
            await Task.Delay(delay);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error aplicando migraciones de base de datos al iniciar la aplicación luego de {MaxAttempts} intentos.", maxAttempts);
            throw;
        }
    }
}

static async Task EnsureDefaultAdminUserAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<Context>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    try
    {
        var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Email == "admin");

        if (adminUser == null)
        {
            adminUser = new User
            {
                Email = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin", workFactor: 10),
                FullName = "Administrador",
                IsActive = true,
                EmailVerified = true,
                Role = UserRole.SuperAdmin,
                PlanType = PlanType.PRO,
                UsageTypeId = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
#pragma warning disable CS0618
                Plan = "PRO",
                SubscriptionStatus = "ACTIVO"
#pragma warning restore CS0618
            };

            context.Users.Add(adminUser);
            await context.SaveChangesAsync();
            logger.LogInformation("Usuario de prueba admin/admin creado automáticamente.");
            return;
        }

        adminUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin", workFactor: 10);
        adminUser.IsActive = true;
        adminUser.EmailVerified = true;
        adminUser.Role = UserRole.SuperAdmin;
        adminUser.PlanType = PlanType.PRO;
        adminUser.UsageTypeId = 1;
        adminUser.UpdatedAt = DateTime.UtcNow;
#pragma warning disable CS0618
        adminUser.Plan = "PRO";
        adminUser.SubscriptionStatus = "ACTIVO";
#pragma warning restore CS0618

        await context.SaveChangesAsync();
        logger.LogInformation("Usuario de prueba admin/admin actualizado automáticamente.");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "No se pudo crear/actualizar usuario admin/admin en startup.");
    }
}

static async Task EnsureConfiguredOwnerUserAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<Context>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    try
    {
        var ownerEmail = (configuration["OwnerSecurity:OwnerEmail"] ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ownerEmail))
        {
            return;
        }

        var ownerUser = await context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == ownerEmail);
        if (ownerUser == null)
        {
            logger.LogWarning("OwnerSecurity:OwnerEmail configurado pero no existe usuario con ese email: {OwnerEmail}", ownerEmail);
            return;
        }

        var changed = false;
        if (ownerUser.Role != UserRole.SuperAdmin)
        {
            ownerUser.Role = UserRole.SuperAdmin;
            changed = true;
        }

        if (!ownerUser.IsActive)
        {
            ownerUser.IsActive = true;
            changed = true;
        }

        if (!ownerUser.EmailVerified)
        {
            ownerUser.EmailVerified = true;
            changed = true;
        }

        if (changed)
        {
            ownerUser.UpdatedAt = DateTime.UtcNow;
            await context.SaveChangesAsync();
            logger.LogInformation("Usuario owner promovido/activado como SuperAdmin: {OwnerEmail}", ownerEmail);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error asegurando usuario owner configurado");
    }
}

static async Task EnsureDemoPhotographerDataAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var demoDataService = scope.ServiceProvider.GetRequiredService<IDemoDataService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    try
    {
        var result = await demoDataService.SeedDemoPhotographerAsync(forceReset: false);
        logger.LogInformation("Seed demo startup: {Message}", result.Message);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "No se pudo ejecutar seed demo en startup.");
    }
}
