namespace Admin.Entities.Entities
{
    /// <summary>
    /// Tipos de plan disponibles
    /// </summary>
    public enum PlanType
    {
        /// <summary>
        /// Plan gratuito con funcionalidades básicas
        /// </summary>
        FREE = 0,

        /// <summary>
        /// Período de prueba Pro de 30 días (una sola vez)
        /// </summary>
        PRO_TRIAL = 1,

        /// <summary>
        /// Plan Pro con todas las funcionalidades
        /// </summary>
        PRO = 2
    }
}
