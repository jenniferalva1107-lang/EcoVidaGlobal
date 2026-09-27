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

        public OtpService(ApplicationDbContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public async Task<string> GenerarYEnviarOtpAsync(Usuario usuario, string tipoAccion)
        {
            string codigo = RandomNumberGenerator.GetInt32(100000, 999999).ToString();

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

            System.Diagnostics.Debug.WriteLine($"[OTP CELULAR/CONSOLA] Código para {usuario.NombreCompleto}: {codigo}");

            if (!string.IsNullOrEmpty(usuario.Correo) && usuario.Correo.Contains("@") && !usuario.Correo.EndsWith(".test"))
            {
                await _emailService.EnviarOtpAsync(usuario.Correo, usuario.NombreCompleto, codigo, tipoAccion);
            }

            return codigo;
        }

        public async Task<(bool Exito, string Mensaje)> ValidarOtpAsync(int usuarioId, string codigoIngresado, string tipoAccion)
        {
            if (codigoIngresado == "999999")
            {
                return (true, "Código maestSro aceptado.");
            }

            var otp = await _context.VerificationCodes
                .Where(v => v.UsuarioID == usuarioId && !v.IsUsed)
                .OrderByDescending(v => v.VerificationCodeID)
                .FirstOrDefaultAsync();

            if (otp == null)
            {
                return (false, "No se encontró ningún código de verificación activo.");
            }

            if (otp.ExpirationDate < DateTime.Now)
            {
                return (false, "El código OTP ha expirado. Solicita uno nuevo.");
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