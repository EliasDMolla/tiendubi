using Admin.Entities;
using Admin.Entities.Entities;
using Admin.WebApi.Infrastructure;
using Admin.WebApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Admin.WebApi.Services
{
    public interface IAuthService
    {
        Task<LoginResponse?> LoginAsync(LoginRequest request, string ipAddress);
        Task<LoginResponse?> RefreshTokenAsync(string refreshToken, string ipAddress);
        Task<UserDto?> RegisterAsync(RegisterRequest request);
        Task RevokeTokenAsync(string refreshToken, string ipAddress);
        Task<UserDto?> GetUserByIdAsync(int userId);
        Task<UserDto?> UpdateUserProfileAsync(int userId, UpdateProfileRequest request);
        Task<LoginResponse> GoogleLoginAsync(string email, string? fullName, string ipAddress);
        Task<(bool success, string? token, string? userName, string? message)> RequestPasswordResetAsync(string email);
        Task<bool> ResetPasswordAsync(string token, string newPassword);
        Task<(bool success, string message, string? verificationToken, string? userName)> RegisterWithVerificationAsync(RegisterRequest request);
        Task<(bool success, string message)> VerifyEmailAsync(string token);
        Task<(bool success, string? token, string? userName, int? cooldownSeconds)> ResendVerificationEmailAsync(string email);
        Task<UserDto?> GetUserByEmailAsync(string email);
        Task<bool> IsEmailAvailableAsync(string email);
        Task<bool> IsPublicSlugAvailableAsync(string publicSlug);
        Task<(bool success, string message)> ChangePasswordAsync(int userId, string currentPassword, string newPassword);
    }

    public class AuthService : IAuthService
    {
        private readonly Context _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(Context context, IConfiguration configuration, ILogger<AuthService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<LoginResponse?> LoginAsync(LoginRequest request, string ipAddress)
        {
            var normalizedEmail = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
            _logger.LogInformation($"Intentando login para: {normalizedEmail}");
            
            var startTime = DateTime.UtcNow;
            
            var user = await ResolveUserByEmailAsync(normalizedEmail, includeNavigationProperties: true);

            _logger.LogInformation($"Query usuario: {(DateTime.UtcNow - startTime).TotalMilliseconds}ms");

            if (user == null)
            {
                _logger.LogWarning($"Usuario no encontrado: {request.Email}");
                return null;
            }

            _logger.LogInformation($"Usuario encontrado, verificando contraseña...");
            var verifyStart = DateTime.UtcNow;
            
            if (!VerifyPassword(request.Password, user.PasswordHash))
            {
                _logger.LogWarning($"Contraseña incorrecta para: {request.Email}");
                return null;
            }
            
            _logger.LogInformation($"Verificación contraseña: {(DateTime.UtcNow - verifyStart).TotalMilliseconds}ms");

            if (!user.IsActive)
            {
                _logger.LogWarning($"Usuario inactivo intentó hacer login: {request.Email}");
                return null;
            }

            if (!user.EmailVerified)
            {
                _logger.LogWarning($"Usuario sin email verificado intentó hacer login: {request.Email}");
                return null;
            }

            if (string.IsNullOrWhiteSpace(user.PublicSlug))
            {
                user.PublicSlug = await GenerateUniquePublicSlugAsync(user.FullName ?? user.Email);
            }

            // Rehashear contraseña si usa workFactor antiguo (optimización)
            if (NeedsRehash(user.PasswordHash))
            {
                _logger.LogInformation($"Rehasheando contraseña para optimizar futuras autenticaciones: {user.Email}");
                var rehashStart = DateTime.UtcNow;
                user.PasswordHash = HashPassword(request.Password);
                _logger.LogInformation($"Rehash completado: {(DateTime.UtcNow - rehashStart).TotalMilliseconds}ms");
            }

            // Actualizar último login
            user.LastLogin = DateTime.UtcNow;

            // Generar tokens
            var tokenStart = DateTime.UtcNow;
            var jwtToken = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken(ipAddress);
            _logger.LogInformation($"Generación tokens: {(DateTime.UtcNow - tokenStart).TotalMilliseconds}ms");

            // Guardar refresh token
            user.RefreshTokens.Add(refreshToken);

            // Remover tokens antiguos
            RemoveOldRefreshTokens(user);

            var saveStart = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            _logger.LogInformation($"SaveChanges: {(DateTime.UtcNow - saveStart).TotalMilliseconds}ms");
            _logger.LogInformation($"Login total: {(DateTime.UtcNow - startTime).TotalMilliseconds}ms");

            return new LoginResponse
            {
                Token = jwtToken,
                RefreshToken = refreshToken.Token,
                User = MapToUserDto(user)
            };
        }

        public async Task<LoginResponse?> RefreshTokenAsync(string token, string ipAddress)
        {
            var user = await _context.Users
                .Include(u => u.RefreshTokens)
                .FirstOrDefaultAsync(u => u.RefreshTokens.Any(t => t.Token == token));

            if (user == null)
                return null;

            var refreshToken = user.RefreshTokens.Single(x => x.Token == token);

            if (!refreshToken.IsActive)
                return null;

            // Generar nuevos tokens
            var newRefreshToken = GenerateRefreshToken(ipAddress);
            refreshToken.RevokedAt = DateTime.UtcNow;
            refreshToken.RevokedByIp = ipAddress;
            refreshToken.ReplacedByToken = newRefreshToken.Token;

            user.RefreshTokens.Add(newRefreshToken);
            RemoveOldRefreshTokens(user);

            await _context.SaveChangesAsync();

            var jwtToken = GenerateJwtToken(user);

            return new LoginResponse
            {
                Token = jwtToken,
                RefreshToken = newRefreshToken.Token,
                User = MapToUserDto(user)
            };
        }

        public async Task<UserDto?> RegisterAsync(RegisterRequest request)
        {
            // Verificar si el email ya existe
            if (await _context.Users.AnyAsync(u => u.Email == request.Email))
            {
                _logger.LogWarning($"Intento de registro con email existente: {request.Email}");
                return null;
            }

            var user = new User
            {
                Email = request.Email,
                PasswordHash = HashPassword(request.Password),
                FullName = request.FullName,
                PublicSlug = await GenerateUniquePublicSlugAsync(request.PublicSlug ?? request.FullName ?? request.Email),
                PhoneNumber = request.PhoneNumber,
                IsActive = false,
                EmailVerified = true,
                PhoneVerified = false,
                EmailVerificationToken = null,
                EmailVerificationTokenExpiry = null,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Nuevo usuario registrado: {user.Email}");

            return MapToUserDto(user);
        }

        public async Task RevokeTokenAsync(string token, string ipAddress)
        {
            var user = await _context.Users
                .Include(u => u.RefreshTokens)
                .FirstOrDefaultAsync(u => u.RefreshTokens.Any(t => t.Token == token));

            if (user == null)
                return;

            var refreshToken = user.RefreshTokens.Single(x => x.Token == token);

            if (!refreshToken.IsActive)
                return;

            refreshToken.RevokedAt = DateTime.UtcNow;
            refreshToken.RevokedByIp = ipAddress;

            await _context.SaveChangesAsync();
        }

        public async Task<UserDto?> GetUserByIdAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            return user != null ? MapToUserDto(user) : null;
        }

        public async Task<UserDto?> GetUserByEmailAsync(string email)
        {
            var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
            var user = await ResolveUserByEmailAsync(normalizedEmail, includeNavigationProperties: false);
            return user != null ? MapToUserDto(user) : null;
        }

        public async Task<bool> IsEmailAvailableAsync(string email)
        {
            var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
            return !await _context.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail);
        }

        public async Task<bool> IsPublicSlugAvailableAsync(string publicSlug)
        {
            var normalizedSlug = (publicSlug ?? string.Empty).Trim().ToLowerInvariant();
            return !await _context.Users.AnyAsync(u => u.PublicSlug == normalizedSlug);
        }

        private Task<User?> ResolveUserByEmailAsync(string normalizedEmail, bool includeNavigationProperties)
        {
            IQueryable<User> query = _context.Users;
            var demoEmail = DemoAccountDefaults.Email.ToLowerInvariant();
            var demoLegacyEmail = DemoAccountDefaults.LegacyEmail.ToLowerInvariant();
            var demoSlug = DemoAccountDefaults.PublicSlug;

            if (includeNavigationProperties)
            {
                query = query
                    .Include(u => u.RefreshTokens)
                    .Include(u => u.UsageType);
            }

            if (DemoAccountDefaults.MatchesEmail(normalizedEmail))
            {
                return query.FirstOrDefaultAsync(u =>
                    u.Email.ToLower() == demoEmail
                    || u.Email.ToLower() == demoLegacyEmail
                    || u.PublicSlug == demoSlug);
            }

            return query.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
        }

        public async Task<UserDto?> UpdateUserProfileAsync(int userId, UpdateProfileRequest request)
        {
            var user = await _context.Users.FindAsync(userId);
            
            if (user == null)
                return null;

            // Actualizar solo los campos permitidos
            if (request.FullName != null)
                user.FullName = string.IsNullOrWhiteSpace(request.FullName) ? null : request.FullName.Trim();
            
            if (request.PhoneNumber != null)
                user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();

            if (request.WithdrawalHolderName != null)
                user.WithdrawalHolderName = string.IsNullOrWhiteSpace(request.WithdrawalHolderName) ? null : request.WithdrawalHolderName.Trim();

            if (request.WithdrawalBankName != null)
                user.WithdrawalBankName = string.IsNullOrWhiteSpace(request.WithdrawalBankName) ? null : request.WithdrawalBankName.Trim();

            if (request.WithdrawalAliasOrCbu != null)
                user.WithdrawalAliasOrCbu = string.IsNullOrWhiteSpace(request.WithdrawalAliasOrCbu) ? null : request.WithdrawalAliasOrCbu.Trim();

            await _context.SaveChangesAsync();

            return MapToUserDto(user);
        }

        public async Task<(bool success, string message)> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
                return (false, "Usuario no encontrado");

            if (string.IsNullOrEmpty(user.PasswordHash))
                return (false, "Este usuario no tiene contraseña configurada");

            if (!VerifyPassword(currentPassword, user.PasswordHash))
                return (false, "La contraseña actual es incorrecta");

            if (newPassword.Length < 8)
                return (false, "La nueva contraseña debe tener al menos 8 caracteres");

            user.PasswordHash = HashPassword(newPassword);
            await _context.SaveChangesAsync();

            return (true, "Contraseña actualizada correctamente");
        }

        public async Task<LoginResponse> GoogleLoginAsync(string email, string? fullName, string ipAddress)
        {
            // Buscar usuario existente
            var user = await _context.Users
                .Include(u => u.RefreshTokens)
                .Include(u => u.UsageType)
                .FirstOrDefaultAsync(u => u.Email == email);

            // Si no existe, crear usuario nuevo
            if (user == null)
            {
                user = new User
                {
                    Email = email,
                    FullName = fullName ?? email.Split('@')[0],
                    PublicSlug = await GenerateUniquePublicSlugAsync(fullName ?? email.Split('@')[0]),
                    PasswordHash = string.Empty, // Google OAuth users don't have password
                    IsActive = true,
                    EmailVerified = true, // Google already verified the email
                    LastLogin = DateTime.UtcNow,
                    Plan = "FREE",
                    SubscriptionStatus = "ACTIVO"
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }
            else
            {
                // Actualizar último login
                user.LastLogin = DateTime.UtcNow;
                if (!user.EmailVerified)
                    user.EmailVerified = true;
            }

            // Generar tokens
            var jwtToken = GenerateJwtToken(user);
            var refreshToken = GenerateRefreshToken(ipAddress);

            // Guardar refresh token
            user.RefreshTokens.Add(refreshToken);

            // Remover tokens antiguos
            RemoveOldRefreshTokens(user);

            await _context.SaveChangesAsync();

            return new LoginResponse
            {
                Token = jwtToken,
                RefreshToken = refreshToken.Token,
                User = MapToUserDto(user)
            };
        }

        // Métodos privados
        private string GenerateJwtToken(User user)
        {
            var isReadOnlyUser = DemoAccountDefaults.IsReadOnlyUser(user);
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Secret"] ?? "your-super-secret-key-change-this-in-production-min-32-chars");
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, user.FullName ?? user.Email),
                    new Claim(ClaimTypes.Role, user.Role.ToString()),
                    new Claim(DemoAccountDefaults.ReadOnlyClaimType, isReadOnlyUser.ToString())
                }),
                Expires = DateTime.UtcNow.AddHours(24), // Token expira en 24 horas
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private RefreshToken GenerateRefreshToken(string ipAddress)
        {
            using var rng = RandomNumberGenerator.Create();
            var randomBytes = new byte[64];
            rng.GetBytes(randomBytes);

            return new RefreshToken
            {
                Token = Convert.ToBase64String(randomBytes),
                ExpiresAt = DateTime.UtcNow.AddDays(7), // Refresh token expira en 7 días
                CreatedAt = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };
        }

        private void RemoveOldRefreshTokens(User user)
        {
            // Remover tokens que expiraron hace más de 30 días
            user.RefreshTokens.Where(x => !x.IsActive && x.CreatedAt.AddDays(30) < DateTime.UtcNow)
                .ToList()
                .ForEach(x => user.RefreshTokens.Remove(x));
        }

        private string HashPassword(string password)
        {
            // Usando BCrypt con workFactor optimizado para mejor performance
            // workFactor 10 = ~0.1s (balance entre seguridad y velocidad)
            return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 10);
        }

        private bool NeedsRehash(string hash)
        {
            // Detectar si el hash fue creado con workFactor > 10
            // El formato BCrypt es: $2a$[workFactor]$...
            try
            {
                var parts = hash.Split('$');
                if (parts.Length >= 3 && int.TryParse(parts[2], out int workFactor))
                {
                    return workFactor > 10;
                }
            }
            catch { }
            return false;
        }

        private bool VerifyPassword(string password, string hash)
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }

        private UserDto MapToUserDto(User user)
        {
            var isReadOnlyUser = DemoAccountDefaults.IsReadOnlyUser(user);

            return new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                PublicSlug = user.PublicSlug,
                PhoneNumber = user.PhoneNumber,
                WithdrawalHolderName = user.WithdrawalHolderName,
                WithdrawalBankName = user.WithdrawalBankName,
                WithdrawalAliasOrCbu = user.WithdrawalAliasOrCbu,
                IsActive = user.IsActive,
                EmailVerified = user.EmailVerified,
                IsReadOnly = isReadOnlyUser,
                LastLogin = user.LastLogin,
                CreatedAt = user.CreatedAt,
                
                // Sistema de roles
                Role = user.Role.ToString(),
                IsAdmin = user.Role == Entities.Entities.UserRole.Admin || user.Role == Entities.Entities.UserRole.SuperAdmin,
                
                // Sistema de planes
                PlanType = user.PlanType.ToString(),
                IsProActive = user.IsProActive,
                TrialUsed = user.TrialUsed,
                CanActivateTrial = user.PlanType == Entities.Entities.PlanType.FREE && !user.TrialUsed,
                TrialStartDate = user.TrialStartDate,
                TrialEndDate = user.TrialEndDate,
                TrialDaysRemaining = user.TrialDaysRemaining,
                ProSubscriptionStartDate = user.ProSubscriptionStartDate,
                ProSubscriptionEndDate = user.ProSubscriptionEndDate,
                
                // Tipo de uso del sistema
                UsageTypeId = user.UsageTypeId,
                UsageTypeName = user.UsageType?.Name ?? "Personal",
                
                // Campos legacy
                Plan = user.Plan,
                ProUpgradeDate = user.ProUpgradeDate,
                SubscriptionStatus = user.SubscriptionStatus
            };
        }

        public async Task<(bool success, string? token, string? userName, string? message)> RequestPasswordResetAsync(string email)
        {
            var user = await _context.Users
                .Include(u => u.UsageType)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                return (false, null, null, "El email no está registrado en el sistema");
            }

            // Generar token único
            var token = Guid.NewGuid().ToString("N"); // 32 caracteres sin guiones

            user.PasswordResetToken = token;
            user.PasswordResetTokenExpiry = DateTime.UtcNow.AddHours(1); // Expira en 1 hora

            await _context.SaveChangesAsync();

            return (true, token, user.FullName ?? user.Email, null);
        }

        public async Task<bool> ResetPasswordAsync(string token, string newPassword)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.PasswordResetToken == token 
                    && u.PasswordResetTokenExpiry > DateTime.UtcNow);

            if (user == null)
                return false;

            // Hashear nueva contraseña
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;

            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<(bool success, string message, string? verificationToken, string? userName)> RegisterWithVerificationAsync(RegisterRequest request)
        {
            var requestedPublicSlug = (request.PublicSlug ?? string.Empty).Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(requestedPublicSlug))
            {
                if (!Regex.IsMatch(requestedPublicSlug, "^[a-z0-9][a-z0-9-_]{1,39}$"))
                    return (false, "El nombre público debe tener entre 2 y 40 caracteres, usando letras, números, guiones o guion bajo", null, null);

                var slugExists = await _context.Users.AnyAsync(u => u.PublicSlug == requestedPublicSlug && u.Email != request.Email);
                if (slugExists)
                    return (false, "El nombre público ya está en uso", null, null);
            }

            // Verificar si el email ya existe
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (existingUser != null)
            {
                // Si existe pero no verificó email, regenerar token
                if (string.IsNullOrWhiteSpace(existingUser.Email) && !existingUser.EmailVerified)
                {
                    var newToken = Guid.NewGuid().ToString("N");
                    existingUser.EmailVerificationToken = newToken;
                    existingUser.EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24);
                    existingUser.PasswordHash = HashPassword(request.Password);
                    existingUser.FullName = request.FullName ?? existingUser.FullName;
                    existingUser.PhoneNumber = request.PhoneNumber ?? existingUser.PhoneNumber;
                    if (!string.IsNullOrWhiteSpace(requestedPublicSlug))
                    {
                        existingUser.PublicSlug = requestedPublicSlug;
                    }
                    else if (string.IsNullOrWhiteSpace(existingUser.PublicSlug))
                    {
                        existingUser.PublicSlug = await GenerateUniquePublicSlugAsync(request.PublicSlug ?? existingUser.FullName ?? existingUser.Email);
                    }
                    await _context.SaveChangesAsync();
                    return (true, "Se reenvió el email de verificación", newToken, existingUser.FullName ?? existingUser.Email);
                }
                return (false, "El email ya está registrado", null, null);
            }

            var verificationToken = Guid.NewGuid().ToString("N");

            var user = new User
            {
                Email = request.Email,
                PasswordHash = HashPassword(request.Password),
                FullName = request.FullName,
                PublicSlug = string.IsNullOrWhiteSpace(requestedPublicSlug)
                    ? await GenerateUniquePublicSlugAsync(request.FullName ?? request.Email)
                    : requestedPublicSlug,
                PhoneNumber = request.PhoneNumber,
                IsActive = true,
                EmailVerified = false,
                PhoneVerified = false,
                EmailVerificationToken = verificationToken,
                EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Nuevo usuario registrado (pendiente verificación): {user.Email}");

            return (true, "Cuenta creada. Revisá tu email para activarla.", verificationToken, user.FullName ?? user.Email);
        }

        public async Task<(bool success, string message)> VerifyEmailAsync(string token)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.EmailVerificationToken == token 
                    && u.EmailVerificationTokenExpiry > DateTime.UtcNow);

            if (user == null)
                return (false, "El enlace de verificación es inválido o ha expirado");

            user.EmailVerified = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenExpiry = null;

            await _context.SaveChangesAsync();

            _logger.LogInformation($"Email verificado exitosamente: {user.Email}");
            return (true, "Email verificado exitosamente. Ya podés iniciar sesión.");
        }

        public async Task<(bool success, string? token, string? userName, int? cooldownSeconds)> ResendVerificationEmailAsync(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
                return (false, null, null, null);

            if (user.EmailVerified)
                return (false, null, null, null);

            // Cooldown: no permitir reenvío si el token fue generado hace menos de 60 segundos
            if (user.EmailVerificationTokenExpiry.HasValue)
            {
                var tokenAge = DateTime.UtcNow - (user.EmailVerificationTokenExpiry.Value.AddHours(-24));
                if (tokenAge.TotalSeconds < 60)
                {
                    var remaining = (int)Math.Ceiling(60 - tokenAge.TotalSeconds);
                    return (false, null, null, remaining);
                }
            }

            var verificationToken = Guid.NewGuid().ToString("N");
            user.EmailVerificationToken = verificationToken;
            user.EmailVerificationTokenExpiry = DateTime.UtcNow.AddHours(24);

            await _context.SaveChangesAsync();

            return (true, verificationToken, user.FullName ?? user.Email, null);
        }

        private async Task<string> GenerateUniquePublicSlugAsync(string source)
        {
            var baseSlug = Slugify(source);
            var candidate = baseSlug;
            var suffix = 2;

            while (await _context.Users.AnyAsync(u => u.PublicSlug == candidate))
            {
                candidate = $"{baseSlug}{suffix}";
                suffix++;
            }

            return candidate;
        }

        private static string Slugify(string value)
        {
            var normalized = value.Trim().ToLowerInvariant();
            normalized = normalized.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();

            foreach (var c in normalized)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory == System.Globalization.UnicodeCategory.NonSpacingMark)
                    continue;

                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append('-');
                }
            }

            var slug = Regex.Replace(sb.ToString(), "-+", "-").Trim('-');

            if (string.IsNullOrWhiteSpace(slug))
                slug = "fotografo";

            if (slug.Length > 70)
                slug = slug.Substring(0, 70).Trim('-');

            return slug;
        }
    }
}
