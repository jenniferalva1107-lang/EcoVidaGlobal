namespace EcoVidaGlobal.Models
{
    public class Usuario
    {
        public int UsuarioID { get; set; }
        public string NombreCompleto { get; set; } = string.Empty;
        public string Correo { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? DNI { get; set; }
        public string? Telefono { get; set; }
        public int RolID { get; set; } = 2; // Cliente por defecto
        public string? CodigoVerificacion { get; set; }
        public string EstadoCuenta { get; set; } = "PendienteVerificacion";
        public int PuntosAcumulados { get; set; } = 0;
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        public Rol? Rol { get; set; }
    }
}