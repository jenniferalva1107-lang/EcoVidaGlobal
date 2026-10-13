using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcoVidaGlobal.Data;

namespace EcoVidaGlobal.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

    public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        private bool EsAdmin()
        {
            int? rolId = HttpContext.Session.GetInt32("UsuarioRolID");
            return rolId.HasValue && rolId.Value == 1;
        }

        private IActionResult RedirigirSinAcceso()
        {
            return RedirectToAction("Login", "Cuenta", new
            {
                mensaje = "Acceso restringido únicamente para Administradores."
            });
        }

        // 1. Dashboard Principal
        public async Task<IActionResult> Dashboard()
        {
            if (!EsAdmin())
                return RedirigirSinAcceso();

            var hoy = DateTime.Today;
            var manana = hoy.AddDays(1);

            var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
            var inicioMesSiguiente = inicioMes.AddMonths(1);

            var inicioAnio = new DateTime(hoy.Year, 1, 1);
            var inicioAnioSiguiente = inicioAnio.AddYears(1);

            // Las ventas se calculan a partir de los pagos confirmados.
            var ventasRealizadas = _context.Pedidos
                .Where(p => p.EstadoPago == "Pagado");

            // Ventas del día según la fecha de confirmación del pago.
            ViewBag.VentasDia = await ventasRealizadas
                .Where(p => p.FechaPago.HasValue &&
                            p.FechaPago.Value >= hoy &&
                            p.FechaPago.Value < manana)
                .SumAsync(p => (decimal?)p.Total) ?? 0m;

            // Ventas del mes según la fecha de confirmación del pago.
            ViewBag.VentasMes = await ventasRealizadas
                .Where(p => p.FechaPago.HasValue &&
                            p.FechaPago.Value >= inicioMes &&
                            p.FechaPago.Value < inicioMesSiguiente)
                .SumAsync(p => (decimal?)p.Total) ?? 0m;

            // Indicadores de pedidos según su estado logístico.
            ViewBag.PedidosPendientes =
                await _context.Pedidos.CountAsync(
                    p => p.Estado == "Pendiente");

            ViewBag.PedidosCompletados =
                await _context.Pedidos.CountAsync(
                    p => p.Estado == "Completado" ||
                         p.Estado == "Entregado");

            // Clientes y productos.
            ViewBag.ClientesRegistrados =
                await _context.Usuarios.CountAsync(
                    u => u.RolID == 2);

            ViewBag.ProductosRegistrados =
                await _context.Productos.CountAsync();

            ViewBag.ProductosStockBajo =
                await _context.Productos.CountAsync(
                    p => p.Stock > 0 && p.Stock <= 5);

            // Incluye existencias en cero o negativas.
            ViewBag.ProductosAgotados =
                await _context.Productos.CountAsync(
                    p => p.Stock <= 0);

            // Pedidos pagados durante el año actual.
            // Se utiliza FechaPago, no FechaPedido.
            var pedidosAnuales = await ventasRealizadas
                .Where(p => p.FechaPago.HasValue &&
                            p.FechaPago.Value >= inicioAnio &&
                            p.FechaPago.Value < inicioAnioSiguiente)
                .Select(p => new
                {
                    FechaPago = p.FechaPago.Value,
                    p.Total
                })
                .ToListAsync();

            var nombresMeses = new[]
            {
            "Ene", "Feb", "Mar", "Abr",
            "May", "Jun", "Jul", "Ago",
            "Sep", "Oct", "Nov", "Dic"
        };

            var ventasMensuales = Enumerable.Range(1, 12)
                .Select(mes => pedidosAnuales
                    .Where(p => p.FechaPago.Month == mes)
                    .Sum(p => p.Total))
                .ToArray();

            ViewBag.VentasMensualesLabels = nombresMeses;
            ViewBag.VentasMensualesValores = ventasMensuales;

            // Nuevos clientes durante las últimas cuatro semanas.
            var inicioSemanas = hoy.AddDays(-27);

            var registrosClientes = await _context.Usuarios
                .Where(u => u.RolID == 2 &&
                            u.FechaRegistro >= inicioSemanas &&
                            u.FechaRegistro < manana)
                .Select(u => u.FechaRegistro)
                .ToListAsync();

            var nuevosClientesSemanas = Enumerable.Range(0, 4)
                .Select(semana =>
                {
                    var inicio = inicioSemanas.AddDays(semana * 7);
                    var fin = inicio.AddDays(7);

                    return registrosClientes.Count(fecha =>
                        fecha >= inicio && fecha < fin);
                })
                .ToArray();

            ViewBag.NuevosClientesLabels = new[]
            {
            "Semana 1", "Semana 2",
            "Semana 3", "Semana 4"
        };

            ViewBag.NuevosClientesValores = nuevosClientesSemanas;

            return View();
        }

        // 2. Rutas del menú

        public async Task<IActionResult> Usuarios()
        {
            if (!EsAdmin()) return RedirigirSinAcceso();

            var usuarios = await _context.Usuarios
                .Include(u => u.Rol)
                .ToListAsync();

            return View(usuarios);
        }

        public async Task<IActionResult> Roles()
        {
            if (!EsAdmin()) return RedirigirSinAcceso();

            var roles = await _context.Roles.ToListAsync();
            return View(roles);
        }

        public async Task<IActionResult> Productos()
        {
            if (!EsAdmin()) return RedirigirSinAcceso();

            var productos = await _context.Productos
                .Include(p => p.Categoria)
                .ToListAsync();

            return View(productos);
        }

        public async Task<IActionResult> Categorias()
        {
            if (!EsAdmin()) return RedirigirSinAcceso();

            var categorias = await _context.Categorias.ToListAsync();
            return View(categorias);
        }

        public async Task<IActionResult> Inventario()
        {
            if (!EsAdmin()) return RedirigirSinAcceso();

            var productos = await _context.Productos
                .Include(p => p.Categoria)
                .ToListAsync();

            return View(productos);
        }

        public async Task<IActionResult> Pedidos()
        {
            if (!EsAdmin()) return RedirigirSinAcceso();

            var pedidos = await _context.Pedidos
                .Include(p => p.Usuario)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                .OrderByDescending(p => p.FechaPedido)
                .ToListAsync();

            return View(pedidos);
        }

        public async Task<IActionResult> Ventas()
        {
            if (!EsAdmin()) return RedirigirSinAcceso();

            var pedidos = await _context.Pedidos
                .Include(p => p.Usuario)
                .OrderByDescending(p => p.FechaPedido)
                .ToListAsync();

            return View(pedidos);
        }

        public async Task<IActionResult> Clientes()
        {
            if (!EsAdmin()) return RedirigirSinAcceso();

            var clientes = await _context.Usuarios
                .Where(u => u.RolID == 2)
                .ToListAsync();

            return View(clientes);
        }

        public IActionResult Proveedores() =>
            EsAdmin() ? View() : RedirigirSinAcceso();

        public IActionResult Compras() =>
            EsAdmin() ? View() : RedirigirSinAcceso();

        public IActionResult Tiendas() =>
            EsAdmin() ? View() : RedirigirSinAcceso();

        public IActionResult Promociones() =>
            EsAdmin() ? View() : RedirigirSinAcceso();

        public IActionResult Devoluciones() =>
            EsAdmin() ? View() : RedirigirSinAcceso();

        public IActionResult Reclamos() =>
            EsAdmin() ? View() : RedirigirSinAcceso();

        public async Task<IActionResult> Contabilidad()
        {
            if (!EsAdmin()) return RedirigirSinAcceso();

            // Solo se suman pedidos con pago confirmado.
            ViewBag.TotalIngresos = await _context.Pedidos
                .Where(p => p.EstadoPago == "Pagado")
                .SumAsync(p => (decimal?)p.Total) ?? 0m;

            return View();
        }

        public async Task<IActionResult> EcoBank()
        {
            if (!EsAdmin()) return RedirigirSinAcceso();

            // Se evita sumar pedidos pendientes de pago.
            ViewBag.SaldoGlobal = await _context.Pedidos
                .Where(p => p.EstadoPago == "Pagado")
                .SumAsync(p => (decimal?)p.Total) ?? 0m;

            return View();
        }

        public IActionResult Reportes() =>
            EsAdmin() ? View() : RedirigirSinAcceso();

        public IActionResult Auditoria() =>
            EsAdmin() ? View() : RedirigirSinAcceso();

        public IActionResult Configuracion() =>
            EsAdmin() ? View() : RedirigirSinAcceso();

        // 3. Método POST para guardar cambios en Roles

        [HttpPost]
        public async Task<IActionResult> GuardarRol(
            int rolId, string nombreRol)
        {
            if (!EsAdmin())
                return Json(new
                {
                    exito = false,
                    mensaje = "Sin autorización"
                });

            if (string.IsNullOrWhiteSpace(nombreRol))
            {
                return Json(new
                {
                    exito = false,
                    mensaje = "El nombre del rol no puede estar vacío."
                });
            }

            var rol = await _context.Roles.FindAsync(rolId);

            if (rol == null)
            {
                return Json(new
                {
                    exito = false,
                    mensaje = "El rol no existe."
                });
            }

            rol.NombreRol = nombreRol.Trim();
            await _context.SaveChangesAsync();

            return Json(new
            {
                exito = true,
                mensaje = "Rol actualizado correctamente en SQL Server."
            });
        }

        // 4. Métodos POST para gestión de Usuarios

        [HttpPost]
        public async Task<IActionResult> CrearUsuario(
            string nombreCompleto,
            string correo,
            string password,
            int rolId,
            string dni,
            string telefono)
        {
            if (!EsAdmin())
                return Json(new
                {
                    exito = false,
                    mensaje = "Sin autorización"
                });

            if (await _context.Usuarios.AnyAsync(u => u.Correo == correo))
            {
                return Json(new
                {
                    exito = false,
                    mensaje = "El correo ya está registrado."
                });
            }

            var nuevoUsuario = new EcoVidaGlobal.Models.Usuario
            {
                NombreCompleto = nombreCompleto.Trim(),
                Correo = correo.Trim(),
                PasswordHash = password.Trim(),
                RolID = rolId,
                DNI = dni,
                Telefono = telefono,
                EstadoCuenta = "Activo",
                FechaRegistro = DateTime.Now
            };

            _context.Usuarios.Add(nuevoUsuario);
            await _context.SaveChangesAsync();

            return Json(new
            {
                exito = true,
                mensaje = "Usuario creado correctamente."
            });
        }


        //cambiar estado usuario
        [HttpPost]
        public async Task<IActionResult> CambiarEstadoUsuario(int usuarioId)
        {
            if (!EsAdmin())
                return Json(new
                {
                    exito = false,
                    mensaje = "Sin autorización"
                });

            var usuario = await _context.Usuarios.FindAsync(usuarioId);

            if (usuario == null)
                return Json(new
                {
                    exito = false,
                    mensaje = "Usuario no encontrado."
                });

            // Evita que el administrador desactive su propia cuenta.
            int? usuarioActualId = HttpContext.Session.GetInt32("UsuarioID");

            if (usuarioActualId == usuarioId)
                return Json(new
                {
                    exito = false,
                    mensaje = "No puedes desactivar tu propia cuenta."
                });

            usuario.EstadoCuenta =
                usuario.EstadoCuenta == "Activo" ? "Inactivo" : "Activo";

            await _context.SaveChangesAsync();

            return Json(new
            {
                exito = true,
                nuevoEstado = usuario.EstadoCuenta
            });
        }


        //Eliminarusuario
        [HttpPost]
        public async Task<IActionResult> EliminarUsuario(int usuarioId)
        {
            if (!EsAdmin())
                return Json(new
                {
                    exito = false,
                    mensaje = "Sin autorización"
                });

            var usuario = await _context.Usuarios.FindAsync(usuarioId);

            if (usuario == null)
                return Json(new
                {
                    exito = false,
                    mensaje = "Usuario no encontrado."
                });

            // Evita que el administrador elimine su propia cuenta.
            int? usuarioActualId = HttpContext.Session.GetInt32("UsuarioID");

            if (usuarioActualId == usuarioId)
                return Json(new
                {
                    exito = false,
                    mensaje = "No puedes eliminar tu propia cuenta."
                });

            _context.Usuarios.Remove(usuario);
            await _context.SaveChangesAsync();

            return Json(new
            {
                exito = true,
                mensaje = "Usuario eliminado permanentemente."
            });
        }





    }

}
