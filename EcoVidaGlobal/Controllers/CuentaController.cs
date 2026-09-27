using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcoVidaGlobal.Data;
using EcoVidaGlobal.Models;
using EcoVidaGlobal.Services;

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

            if (usuario == null || !PasswordHasherUtil.VerifyPassword(usuario, usuario.PasswordHash, password))
            {
                ViewBag.Error = "Correo o contraseña incorrectos.";
                return View();
            }

            await _otpService.GenerarYEnviarOtpAsync(usuario, "LOGIN");

            HttpContext.Session.SetInt32("UsuarioID_PendienteOTP", usuario.UsuarioID);
            HttpContext.Session.SetString("Correo_PendienteOTP", usuario.Correo);

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

            usuario.PasswordHash = PasswordHasherUtil.HashPassword(usuario, password);
            usuario.RolID = 2; // Cliente por defecto
            usuario.EstadoCuenta = "PendienteVerificacion";

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            await _otpService.GenerarYEnviarOtpAsync(usuario, "REGISTRO");

            HttpContext.Session.SetInt32("UsuarioID_PendienteOTP", usuario.UsuarioID);
            HttpContext.Session.SetString("Correo_PendienteOTP", usuario.Correo);

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

                return RedirectToAction(nameof(VerificarOtp));
            }

            return RedirectToAction(nameof(Login));
        }

        // GET: /Cuenta/VerificarOtp
        [HttpGet]
        [Route("Cuenta/VerificarOtp")]
        [Route("Cuenta/ConfirmarOtp")]
        public IActionResult VerificarOtp()
        {
            var correo = HttpContext.Session.GetString("Correo_PendienteOTP");
            if (string.IsNullOrEmpty(correo)) return RedirectToAction(nameof(Login));

            ViewBag.Correo = correo;
            return View("VerificarOtp");
        }

        // POST: /Cuenta/VerificarOtp
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Cuenta/VerificarOtp")]
        [Route("Cuenta/ConfirmarOtp")]
        public async Task<IActionResult> VerificarOtp(string codigo)
        {
            int? usuarioId = HttpContext.Session.GetInt32("UsuarioID_PendienteOTP");
            if (!usuarioId.HasValue) return RedirectToAction(nameof(Login));

            var resultado = await _otpService.ValidarOtpAsync(usuarioId.Value, codigo, "REGISTRO");
            if (!resultado.Exito) resultado = await _otpService.ValidarOtpAsync(usuarioId.Value, codigo, "LOGIN");
            if (!resultado.Exito) resultado = await _otpService.ValidarOtpAsync(usuarioId.Value, codigo, "REGISTRO_GOOGLE");
            if (!resultado.Exito) resultado = await _otpService.ValidarOtpAsync(usuarioId.Value, codigo, "LOGIN_GOOGLE");
            if (!resultado.Exito) resultado = await _otpService.ValidarOtpAsync(usuarioId.Value, codigo, "RESTABLECER_PASSWORD");

            if (!resultado.Exito)
            {
                ViewBag.Error = resultado.Mensaje;
                ViewBag.Correo = HttpContext.Session.GetString("Correo_PendienteOTP");
                return View("VerificarOtp");
            }

            var usuario = await _context.Usuarios.Include(u => u.Rol).FirstOrDefaultAsync(u => u.UsuarioID == usuarioId.Value);
            if (usuario == null) return RedirectToAction(nameof(Login));

            // Si vino por restablecimiento de contraseña -> Redirigir a la vista de nueva clave
            if (HttpContext.Session.GetString("FlujoRestablecer") == "TRUE")
            {
                HttpContext.Session.Remove("FlujoRestablecer");
                return RedirectToAction(nameof(RestablecerPassword));
            }

            usuario.EstadoCuenta = "Activo";
            await _context.SaveChangesAsync();

            // Iniciar Sesión
            HttpContext.Session.SetInt32("UsuarioID", usuario.UsuarioID);
            HttpContext.Session.SetString("UsuarioNombre", usuario.NombreCompleto);
            HttpContext.Session.SetString("UsuarioRol", usuario.Rol?.NombreRol ?? "Cliente");
            HttpContext.Session.SetInt32("UsuarioRolID", usuario.RolID);

            HttpContext.Session.Remove("UsuarioID_PendienteOTP");
            HttpContext.Session.Remove("Correo_PendienteOTP");

            return RedirectToAction("Index", "Producto");
        }

        // GET: /Cuenta/OlvidePassword
        public IActionResult OlvidePassword() => View();

        // POST: /Cuenta/OlvidePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OlvidePassword(string correo)
        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Correo == correo);
            if (usuario != null)
            {
                await _otpService.GenerarYEnviarOtpAsync(usuario, "RESTABLECER_PASSWORD");
                HttpContext.Session.SetInt32("UsuarioID_PendienteOTP", usuario.UsuarioID);
                HttpContext.Session.SetString("Correo_PendienteOTP", usuario.Correo);
                HttpContext.Session.SetString("FlujoRestablecer", "TRUE");

                return RedirectToAction(nameof(VerificarOtp));
            }
            ViewBag.Error = "No existe una cuenta registrada con ese correo.";
            return View();
        }

        // GET: /Cuenta/RestablecerPassword
        public IActionResult RestablecerPassword()
        {
            int? usuarioId = HttpContext.Session.GetInt32("UsuarioID_PendienteOTP");
            if (!usuarioId.HasValue) return RedirectToAction(nameof(Login));
            return View();
        }

        // POST: /Cuenta/RestablecerPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestablecerPassword(string nuevaPassword)
        {
            int? usuarioId = HttpContext.Session.GetInt32("UsuarioID_PendienteOTP");
            if (!usuarioId.HasValue) return RedirectToAction(nameof(Login));

            var usuario = await _context.Usuarios.FindAsync(usuarioId.Value);
            if (usuario != null)
            {
                usuario.PasswordHash = PasswordHasherUtil.HashPassword(usuario, nuevaPassword);
                await _context.SaveChangesAsync();

                HttpContext.Session.Remove("UsuarioID_PendienteOTP");
                HttpContext.Session.Remove("Correo_PendienteOTP");

                return RedirectToAction(nameof(Login), new { mensaje = "Contraseña actualizada con éxito. Inicia sesión." });
            }

            return RedirectToAction(nameof(Login));
        }

        // GET: /Cuenta/Logout
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
    }
}