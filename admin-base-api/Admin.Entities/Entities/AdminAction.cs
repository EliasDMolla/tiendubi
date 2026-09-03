namespace Admin.Entities.Entities
{
    /// <summary>
    /// Tipos de acciones administrativas para auditoría
    /// </summary>
    public enum AdminActionType
    {
        UserCreated = 0,
        UserUpdated = 1,
        UserDeactivated = 2,
        UserActivated = 3,
        RoleChanged = 4,
        PlanUpgraded = 5,
        PlanDowngraded = 6,
        SubscriptionCancelled = 7,
        PaymentRefunded = 8,
        ManualPaymentCreated = 9,
        UserDeleted = 10
    }

    /// <summary>
    /// Registro de acciones administrativas para auditoría
    /// </summary>
    public class AdminAction
    {
        public int Id { get; set; }
        
        /// <summary>
        /// ID del administrador que realizó la acción
        /// </summary>
        public int AdminUserId { get; set; }
        public User AdminUser { get; set; }
        
        /// <summary>
        /// ID del usuario afectado (nullable si es acción general)
        /// </summary>
        public int? TargetUserId { get; set; }
        public User? TargetUser { get; set; }
        
        /// <summary>
        /// Tipo de acción realizada
        /// </summary>
        public AdminActionType ActionType { get; set; }
        
        /// <summary>
        /// Entidad afectada (User, Payment, etc.)
        /// </summary>
        public string EntityType { get; set; } = string.Empty;
        
        /// <summary>
        /// ID de la entidad afectada
        /// </summary>
        public int? EntityId { get; set; }
        
        /// <summary>
        /// Descripción legible de la acción
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Valor anterior (JSON)
        /// </summary>
        public string? OldValue { get; set; }
        
        /// <summary>
        /// Nuevo valor (JSON)
        /// </summary>
        public string? NewValue { get; set; }
        
        /// <summary>
        /// Dirección IP desde donde se realizó la acción
        /// </summary>
        public string? IpAddress { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
