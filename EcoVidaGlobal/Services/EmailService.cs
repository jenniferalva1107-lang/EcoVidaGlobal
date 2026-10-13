
using System;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace EcoVidaGlobal.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Método general para enviar correos.
        public async Task EnviarCorreoAsync(
            string destino,
            string asunto,
            string mensaje)
        {
            if (string.IsNullOrWhiteSpace(destino))
            {
                throw new ArgumentException(
                    "No se especificó el correo destinatario.",
                    nameof(destino));
            }

            destino = destino.Trim();

            if (!MailAddress.TryCreate(destino, out var direccionDestino))
            {
                throw new ArgumentException(
                    "El correo destinatario no tiene un formato válido.",
                    nameof(destino));
            }


            string? server = _configuration["EmailSettings:Server"];
            string? portTexto = _configuration["EmailSettings:Port"];
            string? senderEmail = _configuration["EmailSettings:SenderEmail"];
            string? username = _configuration["EmailSettings:Username"];
            string? password = _configuration["EmailSettings:Password"];

            if (string.IsNullOrWhiteSpace(server) ||
                !int.TryParse(portTexto, out int port) ||
                string.IsNullOrWhiteSpace(senderEmail) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "Falta configurar el servidor SMTP, el puerto, " +
                    "el remitente, el usuario o la contraseña en EmailSettings.");
            }


            senderEmail = senderEmail.Trim();

            if (!MailAddress.TryCreate(senderEmail, out var direccionRemitente))
            {
                throw new InvalidOperationException(
                    "EmailSettings:SenderEmail no contiene una dirección válida.");
            }

            using var smtpClient = new SmtpClient(server)
            {
                Port = port,
                Credentials = new NetworkCredential(username, password),
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Timeout = 15000
            };

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(
                    direccionRemitente.Address,
                    "EcoVidaGlobal"),
                Subject = asunto,
                Body = mensaje,
                IsBodyHtml = true
            };

            mailMessage.To.Add(direccionDestino);

            await smtpClient.SendMailAsync(mailMessage);
        }

        // Sobrecarga de dos argumentos.
        public async Task EnviarOtpAsync(
            string destino,
            string codigoOtp)
        {
            await EnviarOtpAsync(
                destino,
                "Usuario",
                codigoOtp,
                "verificación");
        }

        // Sobrecarga utilizada por OtpService.
        public async Task EnviarOtpAsync(
            string destino,
            string nombre,
            string codigoOtp,
            string motivo)
        {
            string asunto = "Tu código de verificación - EcoVidaGlobal";

            string mensaje = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>
                    <h2 style='color: #198754;'>Código de Verificación</h2>
                    <p>Hola <strong>{WebUtility.HtmlEncode(nombre)}</strong>,</p>
                    <p>Tu código para
                       <strong>{WebUtility.HtmlEncode(motivo)}</strong>
                       en EcoVidaGlobal es:</p>
                    <div style='background-color: #f4f4f4; padding: 15px;
                                font-size: 24px; font-weight: bold;
                                letter-spacing: 5px; text-align: center;
                                color: #198754; margin: 15px 0;'>
                        {WebUtility.HtmlEncode(codigoOtp)}
                    </div>
                    <p>Este código vencerá en unos minutos.
                       Si no lo solicitaste, ignora este mensaje.</p>
                </div>";

            await EnviarCorreoAsync(destino, asunto, mensaje);
        }
    }
}
