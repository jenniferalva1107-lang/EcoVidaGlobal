using Microsoft.AspNetCore.Identity;
using EcoVidaGlobal.Models;

namespace EcoVidaGlobal.Services
{
    public static class PasswordHasherUtil
    {
        private static readonly PasswordHasher<Usuario> _hasher = new PasswordHasher<Usuario>();

        public static string HashPassword(Usuario usuario, string password)
        {
            return _hasher.HashPassword(usuario, password);
        }

        public static bool VerifyPassword(Usuario usuario, string hashedPassword, string providedPassword)
        {
            if (string.IsNullOrEmpty(hashedPassword) || string.IsNullOrEmpty(providedPassword))
            {
                return false;
            }

            // Permite validar contraseñas escritas directamente en SQL Server (texto plano o credencial directa)
            if (hashedPassword == providedPassword)
            {
                return true;
            }

            try
            {
                var result = _hasher.VerifyHashedPassword(usuario, hashedPassword, providedPassword);
                return result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
            }
            catch
            {
                // Si la cadena en base de datos no es un Hash en formato Base64 valido, compara texto plano directamente
                return hashedPassword.Equals(providedPassword);
            }
        }
    }
}