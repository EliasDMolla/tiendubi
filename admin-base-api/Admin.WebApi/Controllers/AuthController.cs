using Admin.WebApi.Models;
using Admin.WebApi.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Admin.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthController> _logger;
        private readonly IConfiguration _configuration;
        private readonly FeatureSettings _featureSettings;

        public AuthController(IAuthService authService, IEmailService emailService, ILogger<AuthController> logger, IConfiguration configuration, IOptions<FeatureSettings> featureSettings)
        {
            _authService = authService;
            _emailService = emailService;
            _logger = logger;
            _configuration = configuration;
            _featureSettings = featureSettings.Value;
        }

        [EnableRateLimiting("auth")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                // Pre-check: if user exists but email not verified, return specific error
                var preCheck = await _authService.GetUserByEmailAsync(request.Email);
                if (preCheck != null && !preCheck.EmailVerified)
                {
                    return Unauthorized(new { 
                        message = "Tu cuenta aún no fue verificada. Revisá tu email para activarla.", 
                        code = "EMAIL_NOT_VERIFIED",
                        email = request.Email
                    });
                }

                if (preCheck != null && !preCheck.IsActive)
                {
                    return Unauthorized(new
                    {
                        message = "Tu cuenta está pendiente de aprobación del owner. Te avisaremos cuando esté habilitada.",
                        code = "ACCOUNT_PENDING_APPROVAL",
                        email = request.Email
                    });
                }

                var ipAddress = GetIpAddress();
                var response = await _authService.LoginAsync(request, ipAddress);

                if (response == null)
                    return Unauthorized(new { message = "Email o contraseña incorrectos" });

                SetTokenCookie(response.RefreshToken);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en login");
                return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        [EnableRateLimiting("auth")]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!_featureSettings.RegistrationEnabled)
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "El registro está deshabilitado temporalmente" });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                request.Email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
                request.PublicSlug = string.IsNullOrWhiteSpace(request.PublicSlug)
                    ? null
                    : request.PublicSlug.Trim().ToLowerInvariant();

                var result = await _authService.RegisterWithVerificationAsync(request);

                if (!result.success)
                    return BadRequest(new { message = result.message });

                if (!string.IsNullOrWhiteSpace(result.verificationToken))
                {
                    try
                    {
                        await _emailService.SendEmailVerificationAsync(
                            request.Email,
                            result.verificationToken,
                            result.userName ?? request.FullName ?? request.Email
                        );
                    }
                    catch (Exception emailException)
                    {
                        _logger.LogError(emailException, "El usuario se registró, pero no se pudo enviar el email de verificación");

                        var response = new Dictionary<string, object?>
                        {
                            ["message"] = "La cuenta fue creada, pero no pudimos enviar el mail de validación. Configurá SMTP y reenviá el link.",
                            ["code"] = "EMAIL_VERIFICATION_SEND_FAILED",
                            ["email"] = request.Email
                        };

                        if (HttpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
                        {
                            var frontendUrl = (_configuration["AppSettings:FrontendUrl"] ?? "http://localhost:4200").TrimEnd('/');
                            response["verificationUrl"] = $"{frontendUrl}/auth/verify-email?token={result.verificationToken}";
                        }

                        return Ok(response);
                    }
                }

                return Ok(new
                {
                    message = "Listo. Te mandamos un link de acceso para validar la cuenta. Revisá tu mail.",
                    code = "EMAIL_VERIFICATION_SENT",
                    email = request.Email
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en registro");
                return StatusCode(500, new { message = "Error al registrar usuario" });
            }
        }

        [HttpGet("verify-email")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmail([FromQuery] string token)
        {
            if (string.IsNullOrEmpty(token))
                return BadRequest(new { message = "Token requerido" });

            var result = await _authService.VerifyEmailAsync(token);

            if (!result.success)
                return BadRequest(new { message = result.message });

            return Ok(new { message = result.message });
        }

        [EnableRateLimiting("auth")]
        [HttpPost("resend-verification")]
        [AllowAnonymous]
        public async Task<IActionResult> ResendVerification([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                var result = await _authService.ResendVerificationEmailAsync(request.Email);

                if (result.cooldownSeconds.HasValue)
                    return BadRequest(new { message = $"Esperá {result.cooldownSeconds}s antes de reenviar", cooldownSeconds = result.cooldownSeconds.Value });

                if (!result.success)
                    return Ok(new { message = "Si el email existe y no está verificado, se envió un nuevo enlace" });

                if (result.token != null)
                {
                    await _emailService.SendEmailVerificationAsync(
                        request.Email, 
                        result.token, 
                        result.userName ?? "Usuario"
                    );
                }

                return Ok(new { message = "Se envió un nuevo enlace de verificación a tu email", cooldownSeconds = 60 });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reenviando verificación");
                return StatusCode(500, new { message = "Error enviando email de verificación" });
            }
        }

        [EnableRateLimiting("auth")]
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken()
        {
            var refreshToken = Request.Cookies["refreshToken"];

            if (string.IsNullOrEmpty(refreshToken))
                return Unauthorized(new { message = "Refresh token no encontrado" });

            var ipAddress = GetIpAddress();
            var response = await _authService.RefreshTokenAsync(refreshToken, ipAddress);

            if (response == null)
                return Unauthorized(new { message = "Token inválido" });

            SetTokenCookie(response.RefreshToken);

            return Ok(response);
        }

        [Authorize]
        [HttpPost("revoke-token")]
        public async Task<IActionResult> RevokeToken()
        {
            var token = Request.Cookies["refreshToken"];

            if (string.IsNullOrEmpty(token))
                return BadRequest(new { message = "Token requerido" });

            var ipAddress = GetIpAddress();
            await _authService.RevokeTokenAsync(token, ipAddress);

            return Ok(new { message = "Token revocado" });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                return Unauthorized();

            var user = await _authService.GetUserByIdAsync(userId);

            if (user == null)
                return NotFound();

            return Ok(user);
        }

        /// <summary>
        /// Actualizar perfil del usuario autenticado
        /// </summary>
        [Authorize]
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                    return Unauthorized();

                var updatedUser = await _authService.UpdateUserProfileAsync(userId, request);

                if (updatedUser == null)
                    return NotFound(new { message = "Usuario no encontrado" });

                return Ok(updatedUser);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando perfil");
                return StatusCode(500, new { message = "Error actualizando perfil" });
            }
        }

        /// <summary>
        /// Cambiar contraseña del usuario autenticado
        /// </summary>
        [Authorize]
        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
                    return Unauthorized();

                var (success, message) = await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);

                if (!success)
                    return BadRequest(new { message });

                return Ok(new { message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cambiando contraseña");
                return StatusCode(500, new { message = "Error cambiando contraseña" });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            if (!string.IsNullOrEmpty(refreshToken))
            {
                var ipAddress = GetIpAddress();
                await _authService.RevokeTokenAsync(refreshToken, ipAddress);
            }

            Response.Cookies.Delete("refreshToken", new CookieOptions
            {
                HttpOnly = true,
                SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
                Secure = Request.IsHttps,
                Path = "/"
            });

            return Ok(new { message = "Logout exitoso" });
        }

        [HttpGet("config")]
        public IActionResult GetConfig()
        {
            return Ok(new
            {
                googleAuthEnabled = _configuration.GetValue<bool>("Google:Enabled", false),
                registrationEnabled = _featureSettings.RegistrationEnabled
            });
        }

        [HttpGet("availability")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAvailability([FromQuery] string? email, [FromQuery] string? publicSlug)
        {
            var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedPublicSlug = (publicSlug ?? string.Empty).Trim().ToLowerInvariant();

            var response = new AuthAvailabilityResponse
            {
                EmailAvailable = string.IsNullOrWhiteSpace(normalizedEmail)
                    || await _authService.IsEmailAvailableAsync(normalizedEmail),
                PublicSlugAvailable = string.IsNullOrWhiteSpace(normalizedPublicSlug)
                    || await _authService.IsPublicSlugAvailableAsync(normalizedPublicSlug)
            };

            return Ok(response);
        }

        [EnableRateLimiting("auth")]
        [HttpGet("google-login")]
        public IActionResult GoogleLogin()
        {
            var googleAuthEnabled = _configuration.GetValue<bool>("Google:Enabled", false);
            
            if (!googleAuthEnabled)
            {
                return Redirect($"{GetFrontendUrl()}/auth/login?error=google_disabled");
            }

            // RedirectUri: después de que el middleware procese /signin-google, redirige aquí
            var redirectUrl = Url.Action("GoogleCallback", "Auth", null, Request.Scheme);
            _logger.LogInformation("Google login iniciado. RedirectUri={RedirectUri}", redirectUrl);
            
            var properties = new AuthenticationProperties
            {
                RedirectUri = redirectUrl,
                Items = { { "LoginProvider", "Google" } }
            };
            return Challenge(properties, "Google");
        }

        [HttpGet("google-callback")]
        public async Task<IActionResult> GoogleCallback()
        {
            try
            {
                _logger.LogInformation("=== Google callback iniciado ===");
                
                // Autenticar contra el esquema de cookies (donde el middleware de Google guardó la info)
                var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                
                _logger.LogInformation("Cookie auth result: Succeeded={Succeeded}", result.Succeeded);
                
                if (!result.Succeeded)
                {
                    _logger.LogError("Cookie authentication falló. Failure={Failure}", 
                        result.Failure?.Message ?? "Sin mensaje");
                    return Redirect($"{GetFrontendUrl()}/auth/login?error=google_auth_failed");
                }

                // Extraer claims del usuario autenticado por Google
                var email = result.Principal?.FindFirst(ClaimTypes.Email)?.Value;
                var name = result.Principal?.FindFirst(ClaimTypes.Name)?.Value;

                _logger.LogInformation("Claims extraídos. Email={Email}, Name={Name}", email, name);

                if (string.IsNullOrEmpty(email))
                {
                    _logger.LogError("Email no encontrado en claims de Google");
                    return Redirect($"{GetFrontendUrl()}/auth/login?error=no_email");
                }

                var ipAddress = GetIpAddress();
                var response = await _authService.GoogleLoginAsync(email, name, ipAddress);

                _logger.LogInformation("GoogleLoginAsync exitoso para {Email}", email);
                SetTokenCookie(response.RefreshToken);

                // Limpiar la cookie temporal de OAuth
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                var frontendUrl = GetFrontendUrl();
                _logger.LogInformation("Redirigiendo a: {Url}/auth/google-callback?token=...", frontendUrl);
                
                return Redirect($"{frontendUrl}/auth/google-callback?token={response.Token}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en Google callback: {Message}", ex.Message);
                return Redirect($"{GetFrontendUrl()}/auth/login?error=server_error");
            }
        }

        // Métodos auxiliares
        private string GetFrontendUrl()
        {
            var frontendUrl = _configuration["AppSettings:FrontendUrl"];
            return string.IsNullOrEmpty(frontendUrl) ? "http://localhost:4200" : frontendUrl;
        }
        
        private void SetTokenCookie(string token)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTime.UtcNow.AddDays(7),
                SameSite = Request.IsHttps ? SameSiteMode.None : SameSiteMode.Lax,
                Secure = Request.IsHttps
            };

            Response.Cookies.Append("refreshToken", token, cookieOptions);
        }

        private string GetIpAddress()
        {
            if (Request.Headers.ContainsKey("X-Forwarded-For"))
                return Request.Headers["X-Forwarded-For"].ToString();

            return HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "unknown";
        }

        /// <summary>
        /// Solicitar recuperación de contraseña
        /// </summary>
        [EnableRateLimiting("auth")]
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Email))
                    return BadRequest(new { message = "Email requerido" });

                var result = await _authService.RequestPasswordResetAsync(request.Email);

                if (!result.success)
                    return BadRequest(new { message = result.message });

                // Solo enviar email si el token existe (usuario encontrado)
                if (result.token != null)
                {
                    await _emailService.SendPasswordResetEmailAsync(
                        request.Email, 
                        result.token, 
                        result.userName ?? "Usuario"
                    );
                }

                return Ok(new { message = "Se envió un email con instrucciones para restablecer tu contraseña" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en forgot password");
                return StatusCode(500, new { message = "Error enviando email de recuperación", error = ex.Message });
            }
        }

        /// <summary>
        /// Resetear contraseña con token
        /// </summary>
        [EnableRateLimiting("auth")]
        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrWhiteSpace(request.Token))
                    return BadRequest(new { message = "Token requerido" });

                if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
                    return BadRequest(new { message = "La contraseña debe tener al menos 8 caracteres" });

                var result = await _authService.ResetPasswordAsync(request.Token, request.NewPassword);

                if (!result)
                    return BadRequest(new { message = "Token inválido o expirado" });

                return Ok(new { message = "Contraseña restablecida exitosamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en reset password");
                return StatusCode(500, new { message = "Error restableciendo contraseña" });
            }
        }
    }
}
