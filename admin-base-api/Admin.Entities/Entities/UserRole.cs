namespace Admin.Entities.Entities
{
    /// <summary>
    /// Roles de usuario en el sistema
    /// </summary>
    public enum UserRole
    {
        /// <summary>
        /// Usuario regular
        /// </summary>
        User = 0,

        /// <summary>
        /// Administrador con acceso al panel admin
        /// </summary>
        Admin = 1,

        /// <summary>
        /// Super administrador con acceso total
        /// </summary>
        SuperAdmin = 2
    }
}
