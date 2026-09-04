namespace Admin.Entities.Entities
{
    public class User : Audit
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string? PhoneNumber { get; set; }
        public string? FullName { get; set; }
        public string? PublicSlug { get; set; }

        /// <summary>
        /// Tema del sitio público en formato JSON (colores personalizados, solo plan Pro).
        /// </summary>
        public string? PublicSiteThemeJson { get; set; }

        public string? WithdrawalHolderName { get; set; }
        public string? WithdrawalBankName { get; set; }
        public string? WithdrawalAliasOrCbu { get; set; }
        public bool IsActive { get; set; }
        public bool EmailVerified { get; set; }
        
        // ========================================
        // Sistema de Roles
        // ========================================
        
        /// <summary>
        /// Rol del usuario (User, Admin, SuperAdmin)
        /// </summary>
        public UserRole Role { get; set; } = UserRole.User;
        public bool PhoneVerified { get; set; }
        public DateTime? LastLogin { get; set; }
        
        // ========================================
        // Sistema de Planes y Suscripción
        // ========================================
        
        /// <summary>
        /// Tipo de plan actual del usuario (FREE, PRO_TRIAL, PRO)
        /// </summary>
        public PlanType PlanType { get; set; } = PlanType.FREE;
        
        /// <summary>
        /// Fecha de inicio del trial Pro (nullable)
        /// </summary>
        public DateTime? TrialStartDate { get; set; }
        
        /// <summary>
        /// Fecha de fin del trial Pro (nullable)
        /// </summary>
        public DateTime? TrialEndDate { get; set; }
        
        /// <summary>
        /// Indica si el usuario ya usó su trial gratuito (solo se permite una vez)
        /// </summary>
        public bool TrialUsed { get; set; } = false;
        
        /// <summary>
        /// Fecha de inicio de la suscripción Pro (nullable)
        /// </summary>
        public DateTime? ProSubscriptionStartDate { get; set; }
        
        /// <summary>
        /// Fecha de fin de la suscripción Pro (nullable)
        /// </summary>
        public DateTime? ProSubscriptionEndDate { get; set; }
        
        /// <summary>
        /// Propiedad calculada: indica si el usuario tiene acceso a funciones Pro
        /// </summary>
        public bool IsProActive
        {
            get
            {
                var now = DateTime.UtcNow;
                
                // Un PRO sin vencimiento es una asignación manual/permanente.
                // Si tiene vencimiento, solo permanece activo mientras la fecha sea futura.
                if (PlanType == PlanType.PRO &&
                    (!ProSubscriptionEndDate.HasValue || ProSubscriptionEndDate.Value > now))
                    return true;
                
                // Si está en trial activo
                if (PlanType == PlanType.PRO_TRIAL && TrialEndDate.HasValue && TrialEndDate.Value > now)
                    return true;
                
                return false;
            }
        }
        
        /// <summary>
        /// Días restantes del trial (0 si no está en trial o ya expiró)
        /// </summary>
        public int TrialDaysRemaining
        {
            get
            {
                if (PlanType != PlanType.PRO_TRIAL || !TrialEndDate.HasValue)
                    return 0;
                
                var remaining = (TrialEndDate.Value - DateTime.UtcNow).Days;
                return remaining > 0 ? remaining : 0;
            }
        }
        
        // Campos legacy (mantener por compatibilidad, deprecar gradualmente)
        [Obsolete("Usar PlanType en su lugar")]
        public string Plan { get; set; } = "FREE";
        [Obsolete("Usar ProSubscriptionStartDate en su lugar")]
        public DateTime? ProUpgradeDate { get; set; }
        [Obsolete("Ya no se usa, el estado se deriva de PlanType y fechas")]
        public string SubscriptionStatus { get; set; } = "ACTIVO";
        
        // Para futuros logins externos
        public string? GoogleId { get; set; }
        public string? FacebookId { get; set; }
        
        // Password Reset
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenExpiry { get; set; }
        
        // Email Verification
        public string? EmailVerificationToken { get; set; }
        public DateTime? EmailVerificationTokenExpiry { get; set; }
        
        // ========================================
        // Tipo de Uso del Sistema
        // ========================================
        
        /// <summary>
        /// ID del tipo de uso del sistema (Personal, Administración, Inmobiliario, Otro)
        /// </summary>
        public int UsageTypeId { get; set; } = 1; // Por defecto "Personal"
        
        /// <summary>
        /// Tipo de uso del sistema
        /// </summary>
        public UsageType UsageType { get; set; }
        
        // ========================================
        // Relaciones
        // ========================================
        
        // Tokens de refresh
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        
        // Acciones administrativas realizadas
        public ICollection<AdminAction> AdminActionsPerformed { get; set; } = new List<AdminAction>();
        
        // Acciones administrativas recibidas
        public ICollection<AdminAction> AdminActionsReceived { get; set; } = new List<AdminAction>();

        // Eventos del fotógrafo
        public ICollection<PhotographerEvent> PhotographerEvents { get; set; } = new List<PhotographerEvent>();

        // Ventas del fotógrafo
        public ICollection<PhotoSale> PhotoSales { get; set; } = new List<PhotoSale>();

        public ICollection<Order> Orders { get; set; } = new List<Order>();

        public PhotographerBalance? PhotographerBalance { get; set; }

        public ICollection<Liquidation> Liquidations { get; set; } = new List<Liquidation>();

        public ICollection<PhotoCheckoutSession> PhotoCheckoutSessions { get; set; } = new List<PhotoCheckoutSession>();

        public PhotographerMercadoPagoAccount? PhotographerMercadoPagoAccount { get; set; }
    }
}
