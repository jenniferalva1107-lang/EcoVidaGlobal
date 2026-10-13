using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcoVidaGlobal.Data;
using EcoVidaGlobal.Models;
using EcoVidaGlobal.Services;
using System;
using System.Collections.Generic;

namespace EcoVidaGlobal.Controllers
{
    public class CuentaController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly OtpService _otpService;

        public CuentaController(ApplicationDbContext context, OtpService otpService)
        {
            _context = context;
            _otpService = otpService;
        }

        // Método auxiliar para redirigir según el rol del usuario

        private IActionResult RedirigirPorRol(int rolId)
        {
            return rolId switch
            {
                1 => RedirectToAction("Dashboard", "Admin"),
                2 => RedirectToAction("Index", "Producto"),
                3 => RedirectToAction("Dashboard", "CallCenter"),
                4 => RedirectToAction("Dashboard", "Contabilidad"),
                5 => RedirectToAction("Dashboard", "Sistemas"),
                6 => RedirectToAction("Dashboard", "Almacen"),
                7 => RedirectToAction("Dashboard", "Vendedor"),
                _ => RedirectToAction("Login", "Cuenta")
            };
        }

        // GET: /Cuenta/Login
        public IActionResult Login(string? mensaje)
        {
            ViewBag.Mensaje = mensaje;
            return View();
        }

        // POST: /Cuenta/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string correo, string password)
        {
            var usuario = await _context.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.Correo == correo);

            bool passwordValida = false;
            if (usuario != null)
            {
                passwordValida = usuario.PasswordHash == password ||
                                 PasswordHasherUtil.VerifyPassword(usuario, usuario.PasswordHash, password);
            }

            if (usuario == null || !passwordValida)
            {
                ViewBag.Error = "Correo o contraseña incorrectos.";
                return View();
            }

            // Bloquear cuentas desactivadas antes de cualquier autenticación.
            if (usuario.EstadoCuenta != "Activo")
            {
                ViewBag.Error =
                    "Tu cuenta no está activa. Contacta con el administrador.";
                return View();
            }

            // SI ES ADMINISTRADOR: OMITIR OTP E INICIAR SESIÓN DIRECTAMENTE EN SU PANEL
            if (usuario.RolID == 1 || usuario.Correo == "jenniferalva1107@gmail.com")
            {
                HttpContext.Session.SetInt32("UsuarioID", usuario.UsuarioID);
                HttpContext.Session.SetString("UsuarioNombre", usuario.NombreCompleto);
                HttpContext.Session.SetString("UsuarioRol", usuario.Rol?.NombreRol ?? "Administrador");
                HttpContext.Session.SetInt32("UsuarioRolID", 1);

                return RedirectToAction("Dashboard", "Admin");
            }

            // PARA OTROS ROLES: SOLICITAR CÓDIGO OTP POR CORREO
            await _otpService.GenerarYEnviarOtpAsync(usuario, "LOGIN");

            HttpContext.Session.SetInt32("UsuarioID_PendienteOTP", usuario.UsuarioID);
            HttpContext.Session.SetString("Correo_PendienteOTP", usuario.Correo);
            HttpContext.Session.SetString("TipoOtpPendiente", "LOGIN");

            return RedirectToAction(nameof(VerificarOtp));
        }

        // GET: /Cuenta/Registro
        public IActionResult Registro()
        {
            return View();
        }

        // POST: /Cuenta/Registro
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registro(Usuario usuario, string password)
        {
            if (await _context.Usuarios.AnyAsync(u => u.Correo == usuario.Correo))
            {
                ViewBag.Error = "El correo ya se encuentra registrado.";
                return View(usuario);
            }

            usuario.CodigoUsuario = await GenerarCodigoUsuarioAsync();
            usuario.PasswordHash = PasswordHasherUtil.HashPassword(usuario, password);
            usuario.RolID = 2;
            usuario.EstadoCuenta = "PendienteVerificacion";

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            await _otpService.GenerarYEnviarOtpAsync(usuario, "REGISTRO");

            HttpContext.Session.SetInt32(
                "UsuarioID_PendienteOTP", usuario.UsuarioID);

            HttpContext.Session.SetString(
                "Correo_PendienteOTP", usuario.Correo);

            HttpContext.Session.SetString(
                "TipoOtpPendiente", "REGISTRO");

            return RedirectToAction(nameof(VerificarOtp));
        }

        // GET: /Cuenta/GoogleLogin
        public IActionResult GoogleLogin()
        {
            var properties = new AuthenticationProperties
            {
                RedirectUri = Url.Action("GoogleResponse")
            };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        // GET: /Cuenta/GoogleResponse
        public async Task<IActionResult> GoogleResponse()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (!result.Succeeded) return RedirectToAction(nameof(Login));

            var claims = result.Principal.Identities.FirstOrDefault()?.Claims;
            var correo = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            var nombre = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

            if (string.IsNullOrEmpty(correo)) return RedirectToAction(nameof(Login));

            var usuario = await _context.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.Correo == correo);

            if (usuario == null)
            {
                HttpContext.Session.SetString("Google_Correo", correo);
                HttpContext.Session.SetString("Google_Nombre", nombre ?? "Usuario Google");
                return RedirectToAction(nameof(CompletarDatosGoogle));
            }

            if (string.IsNullOrEmpty(usuario.DNI) || string.IsNullOrEmpty(usuario.Telefono))
            {
                HttpContext.Session.SetInt32("UsuarioID_Incompleto", usuario.UsuarioID);
                return RedirectToAction(nameof(CompletarDatosGoogle));
            }

            await _otpService.GenerarYEnviarOtpAsync(usuario, "LOGIN_GOOGLE");

            HttpContext.Session.SetInt32("UsuarioID_PendienteOTP", usuario.UsuarioID);
            HttpContext.Session.SetString("Correo_PendienteOTP", usuario.Correo);
            HttpContext.Session.SetString("TipoOtpPendiente", "LOGIN_GOOGLE");

            return RedirectToAction(nameof(VerificarOtp));
        }

        // GET: /Cuenta/CompletarDatosGoogle
        public IActionResult CompletarDatosGoogle()
        {
            return View();
        }

        // POST: /Cuenta/CompletarDatosGoogle
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompletarDatosGoogle(string dni, string telefono)
        {
            var correo = HttpContext.Session.GetString("Google_Correo");
            var nombre = HttpContext.Session.GetString("Google_Nombre");
            int? usuarioIdIncompleto = HttpContext.Session.GetInt32("UsuarioID_Incompleto");

            Usuario? usuario = null;

            if (usuarioIdIncompleto.HasValue)
            {
                usuario = await _context.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.UsuarioID == usuarioIdIncompleto.Value);
                if (usuario != null)
                {
                    usuario.DNI = dni;
                    usuario.Telefono = telefono;
                    await _context.SaveChangesAsync();
                }
            }
            else if (!string.IsNullOrEmpty(correo))
            {
                usuario = new Usuario
                {
                    CodigoUsuario = await GenerarCodigoUsuarioAsync(),
                    NombreCompleto = nombre!,
                    Correo = correo,
                    DNI = dni,
                    Telefono = telefono,
                    PasswordHash = "GOOGLE_OAUTH_ACCOUNT",
                    RolID = 2,
                    EstadoCuenta = "PendienteVerificacion"
                };

                _context.Usuarios.Add(usuario);
                await _context.SaveChangesAsync();
            }
            else
            {
                return RedirectToAction(nameof(Login));
            }

            if (usuario != null)
            {
                await _otpService.GenerarYEnviarOtpAsync(usuario, "REGISTRO_GOOGLE");

                HttpContext.Session.SetInt32("UsuarioID_PendienteOTP", usuario.UsuarioID);
                HttpContext.Session.SetString("Correo_PendienteOTP", usuario.Correo);
                HttpContext.Session.SetString("TipoOtpPendiente", "REGISTRO_GOOGLE");

                return RedirectToAction(nameof(VerificarOtp));
            }

            return RedirectToAction(nameof(Login));
        }


        //Olvide Password

        // GET: /Cuenta/OlvidePassword
        [HttpGet]
        public IActionResult OlvidePassword()
        {
            return View();
        }

        // POST: /Cuenta/OlvidePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OlvidePassword(
            string codigoUsuario,
            string? correoTemporal)
        {
            if (string.IsNullOrWhiteSpace(codigoUsuario))
            {
                ViewBag.Error = "Ingresa tu código de usuario.";
                return View();
            }

            string codigoBuscado = codigoUsuario.Trim();

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u =>
                    u.CodigoUsuario == codigoBuscado);

            if (usuario == null)
            {
                ViewBag.Error = "No se encontró una cuenta con ese código.";
                return View();
            }

            bool requiereCorreoTemporal =
                !string.IsNullOrWhiteSpace(usuario.Correo) &&
                usuario.Correo.EndsWith(
                    ".test",
                    StringComparison.OrdinalIgnoreCase);

            string? destinoOtp = null;

            if (requiereCorreoTemporal)
            {
                if (string.IsNullOrWhiteSpace(correoTemporal))
                {
                    ViewBag.PedirCorreoTemporal = true;
                    ViewBag.CodigoUsuario = codigoBuscado;
                    return View();
                }

                correoTemporal = correoTemporal.Trim();

                bool correoValido =
                    new System.ComponentModel.DataAnnotations
                        .EmailAddressAttribute()
                        .IsValid(correoTemporal);

                if (!correoValido ||
                    correoTemporal.EndsWith(
                        ".test",
                        StringComparison.OrdinalIgnoreCase))
                {
                    ViewBag.Error =
                        "Ingresa un correo temporal válido.";
                    ViewBag.PedirCorreoTemporal = true;
                    ViewBag.CodigoUsuario = codigoBuscado;
                    return View();
                }

                destinoOtp = correoTemporal;
            }

            // Para cuentas normales se utiliza el correo registrado.
            // Para cuentas .test se utiliza el destino temporal.
            await _otpService.GenerarYEnviarOtpAsync(
                usuario,
                "RESTABLECER_PASSWORD",
                destinoOtp);

            HttpContext.Session.SetInt32(
                "UsuarioID_PendienteOTP", usuario.UsuarioID);

            HttpContext.Session.SetString(
                "Correo_PendienteOTP",
                destinoOtp ?? usuario.Correo);

            HttpContext.Session.SetString(
                "TipoOtpPendiente", "RESTABLECER_PASSWORD");

            HttpContext.Session.SetString(
                "FlujoRestablecer", "TRUE");

            HttpContext.Session.Remove("FlujoRestablecerVerificado");

            return RedirectToAction(nameof(VerificarOtp));
        }





        // GET: /Cuenta/VerificarOtp
        [HttpGet]
        [Route("Cuenta/VerificarOtp")]
        [Route("Cuenta/ConfirmarOtp")]
        public IActionResult VerificarOtp()
        {
            var correo = HttpContext.Session.GetString(
                "Correo_PendienteOTP");

            var tipoAccion = HttpContext.Session.GetString(
                "TipoOtpPendiente");

            if (string.IsNullOrEmpty(correo) ||
                string.IsNullOrEmpty(tipoAccion))
            {
                return RedirectToAction(nameof(Login));
            }

            ViewBag.Correo = correo;
            ViewBag.EsRecuperacion =
                tipoAccion == "RESTABLECER_PASSWORD";

            return View("VerificarOtp");
        }

        // POST: /Cuenta/VerificarOtp
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Cuenta/VerificarOtp")]
        [Route("Cuenta/ConfirmarOtp")]
        public async Task<IActionResult> VerificarOtp(string codigo)
        {
            int? usuarioId = HttpContext.Session.GetInt32(
                "UsuarioID_PendienteOTP");

            string? tipoAccion = HttpContext.Session.GetString(
                "TipoOtpPendiente");

            if (!usuarioId.HasValue ||
                string.IsNullOrEmpty(tipoAccion))
            {
                return RedirectToAction(nameof(Login));
            }

            var resultado = await _otpService.ValidarOtpAsync(
                usuarioId.Value,
                codigo,
                tipoAccion);

            if (!resultado.Exito)
            {
                ViewBag.Error = resultado.Mensaje;
                ViewBag.Correo = HttpContext.Session.GetString(
                    "Correo_PendienteOTP");
                ViewBag.EsRecuperacion =
                    tipoAccion == "RESTABLECER_PASSWORD";

                return View("VerificarOtp");
            }

            var usuario = await _context.Usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(
                    u => u.UsuarioID == usuarioId.Value);

            if (usuario == null)
            {
                return RedirectToAction(nameof(Login));
            }

            if (tipoAccion == "RESTABLECER_PASSWORD")
            {
                HttpContext.Session.SetString(
                    "FlujoRestablecerVerificado", "TRUE");

                HttpContext.Session.Remove("FlujoRestablecer");

                return RedirectToAction(nameof(RestablecerPassword));
            }

            usuario.EstadoCuenta = "Activo";
            await _context.SaveChangesAsync();

            HttpContext.Session.SetInt32(
                "UsuarioID", usuario.UsuarioID);

            HttpContext.Session.SetString(
                "UsuarioNombre", usuario.NombreCompleto);

            HttpContext.Session.SetString(
                "UsuarioRol",
                usuario.Rol?.NombreRol ?? "Cliente");

            HttpContext.Session.SetInt32(
                "UsuarioRolID", usuario.RolID);

            HttpContext.Session.Remove("UsuarioID_PendienteOTP");
            HttpContext.Session.Remove("Correo_PendienteOTP");
            HttpContext.Session.Remove("TipoOtpPendiente");

            return RedirigirPorRol(usuario.RolID);
        }



        // GET: /Cuenta/RestablecerPassword
        [HttpGet]
        public IActionResult RestablecerPassword()
        {
            if (HttpContext.Session.GetString(
                    "FlujoRestablecerVerificado") != "TRUE" ||
                !HttpContext.Session.GetInt32(
                    "UsuarioID_PendienteOTP").HasValue)
            {
                return RedirectToAction(nameof(Login));
            }

            return View();
        }

        // POST: /Cuenta/RestablecerPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestablecerPassword(
            string nuevaPassword)
        {
            if (HttpContext.Session.GetString(
                    "FlujoRestablecerVerificado") != "TRUE")
            {
                return RedirectToAction(nameof(Login));
            }

            int? usuarioId = HttpContext.Session.GetInt32(
                "UsuarioID_PendienteOTP");

            if (!usuarioId.HasValue)
            {
                return RedirectToAction(nameof(Login));
            }

            if (string.IsNullOrWhiteSpace(nuevaPassword) ||
                nuevaPassword.Length < 6)
            {
                ViewBag.Error =
                    "La contraseña debe tener al menos 6 caracteres.";

                return View();
            }

            var usuario = await _context.Usuarios.FindAsync(
                usuarioId.Value);

            if (usuario == null)
            {
                return RedirectToAction(nameof(Login));
            }

            usuario.PasswordHash =
                PasswordHasherUtil.HashPassword(
                    usuario, nuevaPassword);

            await _context.SaveChangesAsync();

            HttpContext.Session.Remove("UsuarioID_PendienteOTP");
            HttpContext.Session.Remove("Correo_PendienteOTP");
            HttpContext.Session.Remove("TipoOtpPendiente");
            HttpContext.Session.Remove("FlujoRestablecer");
            HttpContext.Session.Remove("FlujoRestablecerVerificado");

            return RedirectToAction(
                nameof(Login),
                new { mensaje = "Contraseña actualizada correctamente." });
        }


        // GET: /Cuenta/MisPedidos
        [HttpGet]
        public async Task<IActionResult> MisPedidos()
        {
            int? usuarioId = HttpContext.Session.GetInt32("UsuarioID");

            if (!usuarioId.HasValue)
            {
                return RedirectToAction("Login", "Cuenta", new { mensaje = "Debes iniciar sesión para consultar tus pedidos." });
            }

            var pedidos = await _context.Pedidos
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                .Where(p => p.UsuarioID == usuarioId.Value)
                .OrderByDescending(p => p.FechaPedido)
                .ToListAsync();

            return View(pedidos);
        }

        // GET: /Cuenta/Logout
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Cuenta");
        }

        //EstadoPago
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarPago(int pedidoId)
        {
            // Comprobar que el usuario haya iniciado sesión.
            int? usuarioId = HttpContext.Session.GetInt32("UsuarioID");

            if (!usuarioId.HasValue)
            {
                return RedirectToAction("Login", "Cuenta");
            }

            // Buscar únicamente un pedido que pertenezca al usuario conectado.
            var pedido = await _context.Pedidos
                .FirstOrDefaultAsync(p =>
                    p.PedidoID == pedidoId &&
                    p.UsuarioID == usuarioId.Value);

            if (pedido == null)
            {
                return NotFound();
            }

            // No permitir confirmar el pago de un pedido cancelado.
            if (pedido.Estado == "Cancelado")
            {
                TempData["MensajePago"] =
                    "No se puede confirmar el pago de un pedido cancelado.";

                return RedirectToAction(nameof(MisPedidos));
            }

            // Actualizar el estado del pago en la base de datos.
            if (pedido.EstadoPago != "Pagado")
            {
                pedido.EstadoPago = "Pagado";
                pedido.FechaPago = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["MensajePago"] =
                    "Pago confirmado en la simulación académica.";
            }

            return RedirectToAction(nameof(MisPedidos));
        }


        //GenerarCodigoUsuario:
        private async Task<string> GenerarCodigoUsuarioAsync()
        {
            var codigosExistentes = await _context.Usuarios
                .Select(u => u.CodigoUsuario)
                .ToListAsync();

            var numerosUsados = new HashSet<int>();

            foreach (var codigo in codigosExistentes)
            {
                if (codigo != null &&
                    codigo.StartsWith("EVG-", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(codigo.Substring(4), out int numero))
                {
                    numerosUsados.Add(numero);
                }
            }

            int siguiente = 1;

            while (numerosUsados.Contains(siguiente))
            {
                siguiente++;
            }

            return $"EVG-{siguiente:D2}";
        }



    }
}