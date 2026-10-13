using EcoVidaGlobal.Data;
using EcoVidaGlobal.Models;
using EcoVidaGlobal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EcoVidaGlobal.Controllers
{
    public class AlmacenController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;

        public AlmacenController(ApplicationDbContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        private bool EsAlmacen()
        {
            int? rolId = HttpContext.Session.GetInt32("UsuarioRolID");
            return rolId.HasValue && (rolId.Value == 6 || rolId.Value == 1);
        }

        private IActionResult RedirigirSinAcceso()
        {
            return RedirectToAction("Login", "Cuenta", new { mensaje = "Acceso restringido únicamente para personal de Almacén." });
        }

        // GET: /Almacen/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            if (!EsAlmacen()) return RedirigirSinAcceso();

            ViewBag.TotalProductos = await _context.Productos.CountAsync();
            ViewBag.StockBajo = await _context.Productos.CountAsync(p => p.Stock > 0 && p.Stock <= 5);
            ViewBag.Agotados = await _context.Productos.CountAsync(p => p.Stock == 0);
            ViewBag.PedidosPorDespachar = await _context.Pedidos.CountAsync(p => p.Estado == "Pendiente" || p.Estado == "En Preparacion" || p.Estado == "Empaquetado");

            return View();
        }


        // GET: /Almacen/Inventario
        public async Task<IActionResult> Inventario()
        {
            if (!EsAlmacen()) return RedirigirSinAcceso();

            // Si una entrega venció mientras la página estaba cerrada,
            // la recepción se podrá registrar desde la interfaz.
            var productos = await _context.Productos
                .Include(p => p.Categoria)
                .ToListAsync();

            ViewBag.SolicitudesReposicion = await _context.SolicitudesReposicion
                .Include(s => s.Producto)
                .Where(s => s.Estado == "Aprobada"
                         || s.Estado == "EnTransito"
                         || s.Estado == "Recibida")
                .OrderBy(s => s.FechaSolicitud)
                .ToListAsync();

            return View(productos);
        }


        // GET: /Almacen/Movimientos
        public async Task<IActionResult> Movimientos()
        {
            if (!EsAlmacen()) return RedirigirSinAcceso();

            var pedidos = await _context.Pedidos
                .Include(p => p.Usuario)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                .OrderByDescending(p => p.FechaPedido)
                .ToListAsync();

            return View(pedidos);
        }

        // GET: /Almacen/Despachos
        public async Task<IActionResult> Despachos()
        {
            if (!EsAlmacen()) return RedirigirSinAcceso();

            var pedidos = await _context.Pedidos
                .Include(p => p.Usuario)
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                .OrderByDescending(p => p.FechaPedido)
                .ToListAsync();

            return View(pedidos);
        }

        // GET: /Almacen/Reposicion
        public async Task<IActionResult> Reposicion()
        {
            if (!EsAlmacen()) return RedirigirSinAcceso();

            var productosBajos = await _context.Productos
                .Include(p => p.Categoria)
                .Where(p => p.Stock <= 10)
                .ToListAsync();

            return View(productosBajos);
        }

        // POST: /Almacen/ActualizarStock
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ActualizarStock(int productoId, int nuevoStock)
        {
            if (!EsAlmacen())
                return Unauthorized();

            // El stock de tienda ya no se modifica directamente.
            // Debe cambiar mediante una transferencia desde StockAlmacen.
            return BadRequest(new
            {
                exito = false,
                mensaje = "Utiliza la transferencia desde el stock del almacén."
            });
        }

        // POST: /Almacen/CambiarEstadoPedido
        [HttpPost]
        public async Task<IActionResult> CambiarEstadoPedido(int pedidoId, string nuevoEstado)
        {
            if (!EsAlmacen())
                return Json(new { exito = false, mensaje = "Sin autorización para realizar esta acción." });

            // 1. DATO DEL EJECUTOR (El trabajador de almacén que hace clic)
            int? trabajadorId = HttpContext.Session.GetInt32("UsuarioID");
            string trabajadorNombre = HttpContext.Session.GetString("UsuarioNombre") ?? "Trabajador de Almacén";

            // 2. OBTENER EL PEDIDO Y NAVEGAR DIRECTAMENTE A SU CLIENTE DUENIO
            var pedido = await _context.Pedidos
                .Include(p => p.Usuario) // Cliente propietario del pedido
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(p => p.PedidoID == pedidoId);

            if (pedido == null)
            {
                return Json(new { exito = false, mensaje = "El pedido no fue encontrado en la base de datos." });
            }

            string estadoAnterior = pedido.Estado;

            // 3. ACTUALIZAR ESTADO DEL PEDIDO EN LA BD
            pedido.Estado = nuevoEstado;
            await _context.SaveChangesAsync();

            // 4. EXTRAER DATOS DINÁMICOS DEL CLIENTE PROPIETARIO DEL PEDIDO (NUNCA DE LA SESIÓN)
            if (pedido.Usuario == null || string.IsNullOrWhiteSpace(pedido.Usuario.Correo))
            {
                // Si la navegación fue nula por desajuste de ID, buscamos explícitamente en BD al cliente de ese pedido
                pedido.Usuario = await _context.Usuarios.FindAsync(pedido.UsuarioID);
            }

            if (pedido.Usuario == null || string.IsNullOrWhiteSpace(pedido.Usuario.Correo))
            {
                return Json(new
                {
                    exito = true,
                    mensaje = $"Estado cambiado a '{nuevoEstado}'. No se pudo enviar correo porque el cliente propietario (ID: {pedido.UsuarioID}) no posee un email válido registrado."
                });
            }

            string correoClientePropietario = pedido.Usuario.Correo;
            string nombreClientePropietario = pedido.Usuario.NombreCompleto ?? "Estimado(a) Cliente";

            // 5. REGISTRO DE AUDITORÍA (Separación explícita entre Ejecutor y Cliente)
            Console.WriteLine($"[AUDITORÍA LOG] Fecha: {DateTime.Now} | Ejecutor (Almacén): ID {trabajadorId} ({trabajadorNombre}) | Acción: Cambio de estado de '{estadoAnterior}' a '{nuevoEstado}' | PedidoID: {pedido.PedidoID} | Cliente Destinatario: {correoClientePropietario}");

            // 6. ENVIAR CORREO EXCLUSIVAMENTE AL CLIENTE PROPIETARIO
            string icono = nuevoEstado switch
            {
                "En Preparacion" => "📦",
                "Empaquetado" => "🏷️",
                "En Camino" => "🚚",
                "Entregado" => "✅",
                "Cancelado" => "❌",
                _ => "🌱"
            };

            string asunto = $"{icono} Tu pedido #{pedido.PedidoID} se encuentra en estado: {nuevoEstado} - EcoVidaGlobal";
            string mensajeHtml = $@"
                <div style='font-family: Arial, sans-serif; padding: 20px; color: #333; max-width: 600px; border: 1px solid #e0e0e0; border-radius: 8px;'>
                    <h2 style='color: #198754; margin-top: 0;'>🌱 EcoVidaGlobal Enterprise</h2>
                    <p>Hola <strong>{nombreClientePropietario}</strong>,</p>
                    <p>Te notificamos que tu pedido <strong>#EV-2026-{pedido.PedidoID:D6}</strong> ha sido actualizado:</p>
                    
                    <div style='background-color: #e8f5e9; padding: 15px; border-left: 5px solid #198754; font-size: 1.2em; font-weight: bold; color: #198754; margin: 20px 0;'>
                        {icono} Nuevo Estado: {nuevoEstado}
                    </div>

                    <p><strong>Detalle del Envío:</strong></p>
                    <ul>
                        <li><strong>Dirección:</strong> {pedido.DireccionEnvio ?? "Dirección registrada"}</li>
                        <li><strong>Ciudad / Distrito:</strong> {pedido.Ciudad ?? "San Luis"}</li>
                        <li><strong>Monto Total:</strong> S/ {pedido.Total:F2}</li>
                    </ul>
                    <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'/>
                    <p style='font-size: 0.9em; color: #666;'>Puedes consultar el historial completo ingresando a la sección <strong>Mis Pedidos</strong> en nuestra plataforma web.</p>
                    <p style='font-size: 0.9em; color: #198754; font-weight: bold;'>¡Gracias por formar parte del cambio sostenible! 🌱</p>
                </div>";

            try
            {
                await _emailService.EnviarCorreoAsync(correoClientePropietario, asunto, mensajeHtml);

                return Json(new
                {
                    exito = true,
                    mensaje = $"Estado actualizado a '{nuevoEstado}'. Notificación enviada al cliente propietario: {correoClientePropietario}"
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    exito = true,
                    mensaje = $"Estado actualizado a '{nuevoEstado}' en la base de datos. (Aviso correo cliente: {ex.Message})"
                });
            }
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearSolicitudReposicion(
            int productoId,
            int cantidadSolicitada)
        {
            if (!EsAlmacen())
                return Unauthorized();

            if (cantidadSolicitada < 10)
            {
                TempData["Error"] =
                    "La solicitud debe ser de al menos 10 unidades.";

                return RedirectToAction(nameof(Reposicion));
            }

            var usuarioID = HttpContext.Session.GetInt32("UsuarioID");

            if (usuarioID == null)
                return Unauthorized();

            var producto = await _context.Productos
                .FirstOrDefaultAsync(p => p.ProductoID == productoId);

            if (producto == null)
            {
                TempData["Error"] = "El producto no existe.";
                return RedirectToAction(nameof(Reposicion));
            }

            if (producto.Stock > 10)
            {
                TempData["Error"] =
                    "El producto ya no cumple el límite de reposición.";

                return RedirectToAction(nameof(Reposicion));
            }

            decimal descuento;

            if (cantidadSolicitada < 20)
                descuento = 5m;
            else if (cantidadSolicitada < 50)
                descuento = 10m;
            else
                descuento = 20m;

            decimal montoEstimado = Math.Round(
                producto.Precio * cantidadSolicitada
                * (1m - descuento / 100m),
                2);

            var solicitud = new SolicitudReposicion
            {
                ProductoID = producto.ProductoID,
                UsuarioSolicitaID = usuarioID.Value,
                CantidadSolicitada = cantidadSolicitada,
                PrecioUnitarioReferencia = producto.Precio,
                DescuentoPorcentaje = descuento,
                MontoEstimado = montoEstimado,
                Proveedor = "EcoStore",
                Concepto = "Reposición de producto",
                Estado = "Pendiente",
                FechaSolicitud = DateTime.Now
            };

            _context.SolicitudesReposicion.Add(solicitud);
            await _context.SaveChangesAsync();

            TempData["Mensaje"] =
                $"Solicitud #{solicitud.SolicitudID} creada correctamente.";

            return RedirectToAction(nameof(Reposicion));
        }


        // POST: /Almacen/ConfirmarCompra
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmarCompra(int solicitudId)
        {
            if (!EsAlmacen())
                return Unauthorized();

            await using var transaccion =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var solicitud = await _context.SolicitudesReposicion
                    .FirstOrDefaultAsync(s => s.SolicitudID == solicitudId);

                if (solicitud == null || solicitud.Estado != "Aprobada"
                    || solicitud.GastoID != null)
                {
                    TempData["Error"] =
                        "La solicitud no existe, no está aprobada o ya tiene una compra registrada.";

                    await transaccion.RollbackAsync();
                    return RedirectToAction(nameof(Inventario));
                }

                // Registrar el gasto únicamente al confirmar la compra.
                var gasto = new Gasto
                {
                    Concepto = $"Reposición: {solicitud.Concepto} (Solicitud #{solicitud.SolicitudID})",
                    Categoria = "Inventario y Reposición",
                    Monto = solicitud.MontoEstimado,
                    FechaRegistro = DateTime.Now
                };

                _context.Gastos.Add(gasto);
                await _context.SaveChangesAsync();

                solicitud.GastoID = gasto.GastoID;
                solicitud.FechaInicioEntrega = DateTime.Now;
                solicitud.Estado = "EnTransito";

                await _context.SaveChangesAsync();
                await transaccion.CommitAsync();

                TempData["Mensaje"] =
                    $"Compra de la solicitud #{solicitud.SolicitudID} confirmada. La entrega tarda 30 segundos.";

                return RedirectToAction(nameof(Inventario));
            }
            catch
            {
                await transaccion.RollbackAsync();

                TempData["Error"] =
                    "No se pudo confirmar la compra. Verifica el estado de la solicitud y la conexión con la base de datos.";

                return RedirectToAction(nameof(Inventario));
            }
        }



        // POST: /Almacen/RecibirSolicitud
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecibirSolicitud(int solicitudId)
        {
            if (!EsAlmacen())
                return Unauthorized();

            await using var transaccion =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var solicitud = await _context.SolicitudesReposicion
                    .FirstOrDefaultAsync(s => s.SolicitudID == solicitudId);

                if (solicitud == null
                    || solicitud.Estado != "EnTransito"
                    || solicitud.FechaInicioEntrega == null)
                {
                    TempData["Error"] =
                        "La solicitud no está en tránsito o ya fue recibida.";

                    await transaccion.RollbackAsync();
                    return RedirectToAction(nameof(Inventario));
                }

                // La validación del plazo se hace en el servidor.
                if ((DateTime.Now - solicitud.FechaInicioEntrega.Value).TotalSeconds < 30)
                {
                    TempData["Error"] =
                        "Todavía no han transcurrido los 30 segundos de entrega.";

                    await transaccion.RollbackAsync();
                    return RedirectToAction(nameof(Inventario));
                }

                var producto = await _context.Productos
                    .FirstOrDefaultAsync(p => p.ProductoID == solicitud.ProductoID);

                if (producto == null)
                {
                    TempData["Error"] =
                        "No se encontró el producto asociado a la solicitud.";

                    await transaccion.RollbackAsync();
                    return RedirectToAction(nameof(Inventario));
                }

                // Las unidades recibidas ingresan al almacén, no a la tienda.
                producto.StockAlmacen += solicitud.CantidadSolicitada;

                solicitud.CantidadRecibida = solicitud.CantidadSolicitada;
                solicitud.FechaRecepcion = DateTime.Now;
                solicitud.Estado = "Recibida";

                await _context.SaveChangesAsync();
                await transaccion.CommitAsync();

                TempData["Mensaje"] =
                    $"Solicitud #{solicitud.SolicitudID} recibida. Se agregaron {solicitud.CantidadRecibida} unidades al stock del almacén.";

                return RedirectToAction(nameof(Inventario));
            }
            catch
            {
                await transaccion.RollbackAsync();

                TempData["Error"] =
                    "No se pudo registrar la recepción de la mercancía.";

                return RedirectToAction(nameof(Inventario));
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TransferirATienda(int productoId, int cantidad)
        {
            // 1. Verificar permisos del usuario
            if (!EsAlmacen())
            {
                return RedirigirSinAcceso();
            }

            // 2. Validar la cantidad solicitada
            if (cantidad <= 0)
            {
                TempData["Error"] =
                    "La cantidad a transferir debe ser mayor que cero.";

                return RedirectToAction(nameof(Inventario));
            }

            // 3. Iniciar una transacción para proteger ambos stocks
            await using var transaccion =
                await _context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

            try
            {
                // 4. Buscar el producto
                var producto = await _context.Productos
                    .FirstOrDefaultAsync(p => p.ProductoID == productoId);

                if (producto == null)
                {
                    await transaccion.RollbackAsync();

                    TempData["Error"] =
                        "No se encontró el producto seleccionado.";

                    return RedirectToAction(nameof(Inventario));
                }

                // 5. Verificar que haya suficientes unidades en el almacén
                if (producto.StockAlmacen < cantidad)
                {
                    await transaccion.RollbackAsync();

                    TempData["Error"] =
                        $"Stock insuficiente en el almacén. " +
                        $"Disponibles: {producto.StockAlmacen} unidades.";

                    return RedirectToAction(nameof(Inventario));
                }

                // 6. Evitar superar el límite del tipo int en el stock de tienda
                if (producto.Stock > int.MaxValue - cantidad)
                {
                    await transaccion.RollbackAsync();

                    TempData["Error"] =
                        "La transferencia supera el límite permitido del stock.";

                    return RedirectToAction(nameof(Inventario));
                }

                // 7. Descontar del almacén y sumar a la tienda
                producto.StockAlmacen -= cantidad;
                producto.Stock += cantidad;

                // 8. Guardar ambos cambios
                await _context.SaveChangesAsync();

                // 9. Confirmar la transacción
                await transaccion.CommitAsync();

                TempData["Mensaje"] =
                    $"Se transfirieron {cantidad} unidades de " +
                    $"'{producto.Nombre}' a la tienda correctamente.";

                return RedirectToAction(nameof(Inventario));
            }
            catch (Exception)
            {
                await transaccion.RollbackAsync();

                TempData["Error"] =
                    "No se pudo completar la transferencia. " +
                    "Verifica el inventario e inténtalo nuevamente.";

                return RedirectToAction(nameof(Inventario));
            }
        }





    }
}