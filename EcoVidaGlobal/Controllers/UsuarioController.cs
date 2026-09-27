using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EcoVidaGlobal.Data;
using EcoVidaGlobal.Models;
using EcoVidaGlobal.Services;

namespace EcoVidaGlobal.Controllers
{
    public class UsuarioController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsuarioController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Usuario
        public async Task<IActionResult> Index(int? rolFiltro)
        {
            if (!EsAdmin()) return RedirectToAction("Login", "Cuenta");

            var query = _context.Usuarios.Include(u => u.Rol).AsQueryable();

            if (rolFiltro.HasValue && rolFiltro.Value > 0)
            {
                query = query.Where(u => u.RolID == rolFiltro.Value);
            }

            var usuarios = await query.ToListAsync();
            ViewBag.Roles = await _context.Roles.ToListAsync();
            ViewBag.RolSeleccionado = rolFiltro ?? 0;

            return View(usuarios);
        }

        // GET: /Usuario/Crear
        public async Task<IActionResult> Crear()
        {
            if (!EsAdmin()) return RedirectToAction("Login", "Cuenta");

            ViewBag.Roles = new SelectList(await _context.Roles.ToListAsync(), "RolID", "NombreRol");
            return View();
        }

        // POST: /Usuario/Crear
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(Usuario usuario, string password)
        {
            if (!EsAdmin()) return RedirectToAction("Login", "Cuenta");

            // Validar si el correo ya existe en la base de datos
            if (await _context.Usuarios.AnyAsync(u => u.Correo == usuario.Correo))
            {
                ModelState.AddModelError("Correo", "El correo ingresado ya se encuentra registrado.");
            }

            if (ModelState.IsValid)
            {
                usuario.PasswordHash = PasswordHasherUtil.HashPassword(usuario, password);
                usuario.EstadoCuenta = "Activo";
                _context.Usuarios.Add(usuario);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Roles = new SelectList(await _context.Roles.ToListAsync(), "RolID", "NombreRol", usuario.RolID);
            return View(usuario);
        }

        // GET: /Usuario/Editar/5
        public async Task<IActionResult> Editar(int id)
        {
            if (!EsAdmin()) return RedirectToAction("Login", "Cuenta");

            var usuario = await _context.Usuarios.FindAsync(id);
            if (usuario == null) return NotFound();

            ViewBag.Roles = new SelectList(await _context.Roles.ToListAsync(), "RolID", "NombreRol", usuario.RolID);
            return View(usuario);
        }

        // POST: /Usuario/Editar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Editar(Usuario model)
        {
            if (!EsAdmin()) return RedirectToAction("Login", "Cuenta");

            var usuario = await _context.Usuarios.FindAsync(model.UsuarioID);
            if (usuario == null) return NotFound();

            usuario.NombreCompleto = model.NombreCompleto;
            usuario.Correo = model.Correo;
            usuario.DNI = model.DNI;
            usuario.Telefono = model.Telefono;
            usuario.RolID = model.RolID;
            usuario.EstadoCuenta = model.EstadoCuenta;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool EsAdmin()
        {
            return HttpContext.Session.GetString("UsuarioRol") == "Administrador";
        }
    }
}