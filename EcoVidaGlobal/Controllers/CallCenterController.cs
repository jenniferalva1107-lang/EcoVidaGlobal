using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcoVidaGlobal.Data;

namespace EcoVidaGlobal.Controllers
{
    public class CallCenterController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CallCenterController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool EsCallCenter()
        {
            int? rolId = HttpContext.Session.GetInt32("UsuarioRolID");
            // Permite acceso a Call Center (RolID = 3) y Administrador (RolID = 1)
            return rolId.HasValue && (rolId.Value == 3 || rolId.Value == 1);
        }

        private IActionResult RedirigirSinAcceso()
        {
            return RedirectToAction("Login", "Cuenta", new { mensaje = "Acceso restringido únicamente para Call Center." });
        }

        // 1. Dashboard de Call Center
        public async Task<IActionResult> Dashboard()
        {
            if (!EsCallCenter()) return RedirigirSinAcceso();

            ViewBag.LlamadasAtendidas = 42; // Simulado para métricas de atención
            ViewBag.TicketsPendientes = 5;
            ViewBag.TicketsResueltos = 28;
            ViewBag.PedidosEnProceso = await _context.Pedidos.CountAsync(p => p.Estado == "Pendiente");

            return View();
        }

        // 2. Buscar Cliente e Historial
        public async Task<IActionResult> BuscarCliente(string? criterio)
        {
            if (!EsCallCenter()) return RedirigirSinAcceso();

            var clientes = new List<EcoVidaGlobal.Models.Usuario>();

            if (!string.IsNullOrWhiteSpace(criterio))
            {
                criterio = criterio.Trim().ToLower();
                clientes = await _context.Usuarios
                    .Where(u => u.RolID == 2 && (
                        u.NombreCompleto.ToLower().Contains(criterio) ||
                        u.Correo.ToLower().Contains(criterio) ||
                        (u.DNI != null && u.DNI.Contains(criterio)) ||
                        (u.Telefono != null && u.Telefono.Contains(criterio))
                    ))
                    .ToListAsync();
            }

            ViewBag.Criterio = criterio;
            return View(clientes);
        }

        // 3. Consultar Pedidos
        public async Task<IActionResult> Pedidos()
        {
            if (!EsCallCenter()) return RedirigirSinAcceso();

            var pedidos = await _context.Pedidos
                .Include(p => p.Usuario)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                .OrderByDescending(p => p.FechaPedido)
                .ToListAsync();

            return View(pedidos);
        }

        // 4. Tickets y Reclamos
        public IActionResult Tickets()
        {
            if (!EsCallCenter()) return RedirigirSinAcceso();
            return View();
        }

        // 5. Crear Ticket
        public IActionResult CrearTicket()
        {
            if (!EsCallCenter()) return RedirigirSinAcceso();
            return View();
        }
    }
}