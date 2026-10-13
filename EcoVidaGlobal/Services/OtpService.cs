
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EcoVidaGlobal.Data;
using EcoVidaGlobal.Models;

namespace EcoVidaGlobal.Services
{
    public class OtpService
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;

        public OtpService(
            ApplicationDbContext context,
            EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public async Task<string> GenerarYEnviarOtpAsync(
            Usuario usuario,
            string tipoAccion,
            string? correoDestino = null)
        {
            string codigo = RandomNumberGenerator
                .GetInt32(100000, 999999)
                .ToString();

            var otpEntity = new VerificationCode
            {
                UsuarioID = usuario.UsuarioID,
                Code = codigo,
                ExpirationDate = DateTime.Now.AddMinutes(10),
                IsUsed = false,
                TipoAccion = tipoAccion
            };

            _context.VerificationCodes.Add(otpEntity);
            await _context.SaveChangesAsync();

            System.Diagnostics.Debug.WriteLine(
                $"[OTP] Código para {usuario.NombreCompleto}: {codigo}");

            // Si se especifica un destino alternativo, se utiliza ese correo.
            // En caso contrario, se conserva el comportamiento habitual.
            string? destino = correoDestino;

            if (string.IsNullOrWhiteSpace(destino))
            {
                if (!string.IsNullOrWhiteSpace(usuario.Correo) &&
                    usuario.Correo.Contains("@") &&
                    !usuario.Correo.EndsWith(
                        ".test",
                        StringComparison.OrdinalIgnoreCase))
                {
                    destino = usuario.Correo;
                }
            }

            if (!string.IsNullOrWhiteSpace(destino))
            {
                await _emailService.EnviarOtpAsync(
                    destino,
                    usuario.NombreCompleto,
                    codigo,
                    tipoAccion);
            }

            return codigo;
        }

        public async Task<(bool Exito, string Mensaje)> ValidarOtpAsync(
            int usuarioId,
            string codigoIngresado,
            string tipoAccion)
        {
            // El código maestro de pruebas no autoriza la recuperación.
            if (codigoIngresado == "999999")
            {
                if (tipoAccion == "RESTABLECER_PASSWORD")
                {
                    return (false,
                        "El código ingresado no es válido para recuperar la contraseña.");
                }

                return (true, "Código maestro aceptado.");
            }

            var otp = await _context.VerificationCodes
                .Where(v =>
                    v.UsuarioID == usuarioId &&
                    v.TipoAccion == tipoAccion &&
                    !v.IsUsed)
                .OrderByDescending(v => v.VerificationCodeID)
                .FirstOrDefaultAsync();

            if (otp == null)
            {
                return (false,
                    "No se encontró ningún código activo para esta operación.");
            }

            if (otp.ExpirationDate < DateTime.Now)
            {
                return (false,
                    "El código OTP ha expirado. Solicita uno nuevo.");
            }

            if (otp.Code != codigoIngresado)
            {
                return (false, "El código de verificación es incorrecto.");
            }

            otp.IsUsed = true;
            await _context.SaveChangesAsync();

            return (true, "Verificación exitosa.");
        }
    }
}
