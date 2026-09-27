using System;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using EcoVidaGlobal.Models;

namespace EcoVidaGlobal.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<bool> EnviarOtpAsync(string correoDestino, string nombreUsuario, string codigo, string tipoAccion)
        {
            try
            {
                var smtpServer = _configuration["EmailSettings:Server"] ?? "smtp.gmail.com";
                var port = int.Parse(_configuration["EmailSettings:Port"] ?? "587");
                var senderEmail = _configuration["EmailSettings:SenderEmail"];
                var senderName = _configuration["EmailSettings:SenderName"] ?? "EcoVidaGlobal Oficial";
                var username = _configuration["EmailSettings:Username"];
                var password = _configuration["EmailSettings:Password"];

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail!, senderName),
                    Subject = $"{codigo} es tu código de verificación - EcoVidaGlobal",
                    Body = $@"
                        <div style='font-family: Arial, sans-serif; padding: 20px; background-color: #ffffff; color: #333333;'>
                            <div style='max-width: 500px; margin: 0 auto; border: 1px solid #e0e0e0; padding: 25px; border-radius: 8px;'>
                                <h2 style='color: #198754; margin-top: 0;'>🌱 EcoVidaGlobal</h2>
                                <p>Hola <strong>{nombreUsuario}</strong>,</p>
                                <p>Tu código de seguridad para {tipoAccion.ToLower()} es:</p>
                                <div style='background-color: #f4fdf7; border: 1px solid #198754; padding: 15px; text-align: center; font-size: 32px; font-weight: bold; color: #198754; letter-spacing: 4px; margin: 20px 0;'>
                                    {codigo}
                                </div>
                                <p style='font-size: 12px; color: #777;'>Si no solicitaste este código, ignora este mensaje.</p>
                            </div>
                        </div>",
                    IsBodyHtml = true,
                    Priority = MailPriority.High
                };

                mailMessage.Headers.Add("X-Priority", "1");
                mailMessage.To.Add(new MailAddress(correoDestino, nombreUsuario));

                using var client = new SmtpClient(smtpServer, port)
                {
                    Credentials = new NetworkCredential(username, password),
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 8000
                };

                await client.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR SMTP] {ex.Message}");
                return false;
            }
        }

        public async Task<bool> EnviarComprobanteCompraAsync(string correoDestino, string nombreUsuario, Pedido pedido)
        {
            try
            {
                var smtpServer = _configuration["EmailSettings:Server"] ?? "smtp.gmail.com";
                var port = int.Parse(_configuration["EmailSettings:Port"] ?? "587");
                var senderEmail = _configuration["EmailSettings:SenderEmail"];
                var senderName = _configuration["EmailSettings:SenderName"] ?? "EcoVidaGlobal Compras";
                var username = _configuration["EmailSettings:Username"];
                var password = _configuration["EmailSettings:Password"];

                string filasHtml = "";
                foreach (var d in pedido.Detalles)
                {
                    filasHtml += $@"
                        <tr>
                            <td style='padding: 8px; border-bottom: 1px solid #eee;'>{d.Producto?.Nombre}</td>
                            <td style='padding: 8px; border-bottom: 1px solid #eee; text-align: center;'>{d.Cantidad}</td>
                            <td style='padding: 8px; border-bottom: 1px solid #eee; text-align: right;'>S/ {(d.PrecioUnitario * d.Cantidad):F2}</td>
                        </tr>";
                }

                using var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail!, senderName),
                    Subject = $"🧾 Comprobante de Compra EcoVidaGlobal - Pedido #{pedido.PedidoID}",
                    Body = $@"
                        <div style='font-family: Arial, sans-serif; padding: 20px; background-color: #f4f6f8;'>
                            <div style='max-width: 600px; margin: 0 auto; background: #ffffff; padding: 25px; border-radius: 8px; border: 1px solid #e0e0e0;'>
                                <h2 style='color: #198754; margin-top: 0;'>🌱 EcoVidaGlobal - Comprobante de Compra</h2>
                                <p>Gracias por tu compra, <strong>{nombreUsuario}</strong>. Adjuntamos el resumen de tu pedido:</p>
                                
                                <p style='font-size: 14px; color: #555;'>
                                    <strong>N° Pedido:</strong> #{pedido.PedidoID}<br/>
                                    <strong>Fecha:</strong> {pedido.FechaPedido:dd/MM/yyyy HH:mm}<br/>
                                    <strong>Dirección de Envío:</strong> {pedido.DireccionEnvio}
                                </p>

                                <table style='width: 100%; border-collapse: collapse; margin-top: 15px;'>
                                    <thead>
                                        <tr style='background-color: #e8f5e9; color: #198754;'>
                                            <th style='padding: 8px; text-align: left;'>Producto</th>
                                            <th style='padding: 8px; text-align: center;'>Cant.</th>
                                            <th style='padding: 8px; text-align: right;'>Subtotal</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {filasHtml}
                                    </tbody>
                                </table>

                                <h3 style='text-align: right; color: #198754; margin-top: 20px;'>Total Pagado: S/ {pedido.Total:F2}</h3>
                                <hr style='border: none; border-top: 1px solid #eee;' />
                                <p style='font-size: 12px; color: #888; text-align: center;'>Gracias por contribuir con un planeta más sostenible 🌿</p>
                            </div>
                        </div>",
                    IsBodyHtml = true
                };

                mailMessage.To.Add(new MailAddress(correoDestino));

                using var client = new SmtpClient(smtpServer, port)
                {
                    Credentials = new NetworkCredential(username, password),
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false
                };

                await client.SendMailAsync(mailMessage);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR SMTP] No se pudo enviar el comprobante a {correoDestino}: {ex.Message}");
                return false;
            }
        }
    }
}