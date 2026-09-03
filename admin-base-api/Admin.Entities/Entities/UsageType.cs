namespace Admin.Entities.Entities
{
    /// <summary>
    /// Tipo de uso del sistema por parte del usuario
    /// </summary>
    public class UsageType : Audit
    {
        public int Id { get; set; }
        
        /// <summary>
        /// Nombre del tipo de uso (Personal, Administración, Inmobiliario, Otro)
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Descripción del tipo de uso
        /// </summary>
        public string? Description { get; set; }
        
        /// <summary>
        /// Indica si es el tipo por defecto para nuevos usuarios
        /// </summary>
        public bool IsDefault { get; set; } = false;
        
        /// <summary>
        /// Usuarios que tienen este tipo de uso
        /// </summary>
        public ICollection<User> Users { get; set; } = new List<User>();
    }
}
