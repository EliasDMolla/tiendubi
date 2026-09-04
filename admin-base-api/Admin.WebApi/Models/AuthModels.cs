using Admin.Entities.Entities;

namespace Admin.WebApi.Models
{
    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class LoginResponse
    {
        public string Token { get; set; }
        public string RefreshToken { get; set; }
        public UserDto User { get; set; }
    }

    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; }
    }

    public class RegisterRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? PublicSlug { get; set; }
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string? FullName { get; set; }
        public string? PublicSlug { get; set; }
        public string? PhoneNumber { get; set; }
        public string? WithdrawalHolderName { get; set; }
        public string? WithdrawalBankName { get; set; }
        public string? WithdrawalAliasOrCbu { get; set; }
        public bool IsActive { get; set; }
        public bool EmailVerified { get; set; }
        public bool IsReadOnly { get; set; }
        public DateTime? LastLogin { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Sistema de roles
        public string Role { get; set; } = "User";
        public bool IsAdmin { get; set; }
        
        // Sistema de planes
        public string PlanType { get; set; } = "FREE";
        public bool IsProActive { get; set; }
        public bool TrialUsed { get; set; }
        public bool CanActivateTrial { get; set; }
        
        // Fechas del trial
        public DateTime? TrialStartDate { get; set; }
        public DateTime? TrialEndDate { get; set; }
        public int TrialDaysRemaining { get; set; }
        
        // Fechas de suscripción Pro
        public DateTime? ProSubscriptionStartDate { get; set; }
        public DateTime? ProSubscriptionEndDate { get; set; }
        
        // Tipo de uso del sistema
        public int UsageTypeId { get; set; }
        public string UsageTypeName { get; set; }
        
        // Campos legacy (para compatibilidad)
        [Obsolete("Usar PlanType")]
        public string Plan { get; set; } = "FREE";
        [Obsolete("Usar ProSubscriptionStartDate")]
        public DateTime? ProUpgradeDate { get; set; }
        [Obsolete("Ya no se usa")]
        public string SubscriptionStatus { get; set; } = "ACTIVO";
    }

    public class ForgotPasswordRequest
    {
        public string Email { get; set; }
    }

    public class ResetPasswordRequest
    {
        public string Token { get; set; }
        public string NewPassword { get; set; }
    }

    public class UpdateProfileRequest
    {
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? WithdrawalHolderName { get; set; }
        public string? WithdrawalBankName { get; set; }
        public string? WithdrawalAliasOrCbu { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class AuthAvailabilityResponse
    {
        public bool EmailAvailable { get; set; }
        public bool PublicSlugAvailable { get; set; }
    }
}
