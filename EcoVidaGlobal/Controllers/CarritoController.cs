using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcoVidaGlobal.Data;
using EcoVidaGlobal.Models;

namespace EcoVidaGlobal.Controllers
{
    public class CarritoController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CarritoController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Carrito
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            int? usuarioId = HttpContext.Session.GetInt32("UsuarioID");

            if (!usuarioId.HasValue)
            {
                return View(new List<CarritoItem>());
            }

            var carrito = await _context.CarritoItems
                .Include(c => c.Producto)
                .Where(c => c.UsuarioID == usuarioId.Value)
                .ToListAsync();

            return View(carrito);
        }

        // POST: /Carrito/Agregar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Agregar(
            int productoId,
            int cantidad = 1)
        {
            int? usuarioId = HttpContext.Session.GetInt32("UsuarioID");

            if (!usuarioId.HasValue)
            {
                return RedirectToAction(
                    "Login",
                    "Cuenta",
                    new
                    {
                        mensaje = "Debes iniciar sesión para agregar productos."
                    });
            }

            if (cantidad <= 0)
            {
                TempData["ErrorCarrito"] =
                    "La cantidad solicitada no es válida.";

                return RedirectToAction(nameof(Index));
            }

            var producto = await _context.Productos
                .FirstOrDefaultAsync(p => p.ProductoID == productoId);

            if (producto == null)
            {
                TempData["ErrorCarrito"] =
                    "El producto seleccionado no existe.";

                return RedirectToAction(nameof(Index));
            }

            if (producto.Stock <= 0)
            {
                TempData["ErrorCarrito"] =
                    "Este producto está agotado.";

                return RedirectToAction(nameof(Index));
            }

            var itemExistente = await _context.CarritoItems
                .FirstOrDefaultAsync(c =>
                    c.UsuarioID == usuarioId.Value &&
                    c.ProductoID == productoId);

            int cantidadFinal =
                (itemExistente?.Cantidad ?? 0) + cantidad;

            if (cantidadFinal > producto.Stock)
            {
                TempData["ErrorCarrito"] =
                    $"Solo hay {producto.Stock} unidades disponibles.";

                return RedirectToAction(nameof(Index));
            }

            if (itemExistente != null)
            {
                itemExistente.Cantidad = cantidadFinal;
            }
            else
            {
                _context.CarritoItems.Add(new CarritoItem
                {
                    UsuarioID = usuarioId.Value,
                    ProductoID = productoId,
                    Cantidad = cantidad,
                    FechaAgregado = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // POST: /Carrito/Eliminar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int productoId)
        {
            int? usuarioId = HttpContext.Session.GetInt32("UsuarioID");

            if (usuarioId.HasValue)
            {
                var item = await _context.CarritoItems
                    .FirstOrDefaultAsync(c =>
                        c.UsuarioID == usuarioId.Value &&
                        c.ProductoID == productoId);

                if (item != null)
                {
                    _context.CarritoItems.Remove(item);
                    await _context.SaveChangesAsync();
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: /Carrito/Checkout
        // GET: /Carrito/ProcesarPedido
        [HttpGet]
        [Route("Carrito/Checkout")]
        [Route("Carrito/ProcesarPedido")]
        public async Task<IActionResult> ProcesarPedido()
        {
            int? usuarioId = HttpContext.Session.GetInt32("UsuarioID");

            if (!usuarioId.HasValue)
            {
                return RedirectToAction("Login", "Cuenta");
            }

            var carrito = await _context.CarritoItems
                .Include(c => c.Producto)
                .Where(c => c.UsuarioID == usuarioId.Value)
                .ToListAsync();

            if (!carrito.Any())
            {
                TempData["ErrorCarrito"] =
                    "Tu carrito está vacío.";

                return RedirectToAction(nameof(Index));
            }

            if (carrito.Any(c => c.Producto == null))
            {
                TempData["ErrorCarrito"] =
                    "Hay un producto que ya no existe en el catálogo. Revisa tu carrito.";

                return RedirectToAction(nameof(Index));
            }

            return View("ProcesarPedido", carrito);
        }

        // POST: /Carrito/ConfirmarPedido
        // POST: /Carrito/ProcesarPedido
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Carrito/Checkout")]
        [Route("Carrito/ConfirmarPedido")]
        [Route("Carrito/ProcesarPedido")]
        public async Task<IActionResult> ConfirmarPedido(
            string? direccion,
            string? distrito,
            string? telefono,
            string? metodoPago,
            string? tipoEntrega)
        {
            int? usuarioId = HttpContext.Session.GetInt32("UsuarioID");

            if (!usuarioId.HasValue)
            {
                return RedirectToAction("Login", "Cuenta");
            }

            // Validar los datos enviados por el formulario.
            if (string.IsNullOrWhiteSpace(direccion) ||
                string.IsNullOrWhiteSpace(distrito) ||
                string.IsNullOrWhiteSpace(telefono) ||
                string.IsNullOrWhiteSpace(metodoPago))
            {
                ViewBag.ErrorCheckout =
                    "Completa la dirección, el distrito, el teléfono y el método de pago.";

                var carritoActual = await _context.CarritoItems
                    .Include(c => c.Producto)
                    .Where(c => c.UsuarioID == usuarioId.Value)
                    .ToListAsync();

                return View("ProcesarPedido", carritoActual);
            }

            tipoEntrega = string.IsNullOrWhiteSpace(tipoEntrega)
                ? "Domicilio"
                : tipoEntrega;

            if (tipoEntrega != "Domicilio" &&
                tipoEntrega != "Recojo")
            {
                return BadRequest("La modalidad de entrega no es válida.");
            }

            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            // Volver a consultar el carrito real del usuario.
            var carrito = await _context.CarritoItems
                .Include(c => c.Producto)
                .Where(c => c.UsuarioID == usuarioId.Value)
                .ToListAsync();

            if (!carrito.Any())
            {
                await transaction.RollbackAsync();

                TempData["ErrorCarrito"] =
                    "Tu carrito está vacío.";

                return RedirectToAction(nameof(Index));
            }

            // Validar los productos y el stock antes de crear el pedido.
            foreach (var item in carrito)
            {
                if (item.Producto == null)
                {
                    await transaction.RollbackAsync();

                    ViewBag.ErrorCheckout =
                        "Uno de los productos ya no existe en el catálogo.";

                    return View("ProcesarPedido", carrito);
                }

                if (item.Cantidad <= 0 ||
                    item.Producto.Stock < item.Cantidad)
                {
                    await transaction.RollbackAsync();

                    ViewBag.ErrorCheckout =
                        $"Stock insuficiente para {item.Producto.Nombre}. " +
                        "Vuelve al carrito y revisa las cantidades.";

                    return View("ProcesarPedido", carrito);
                }
            }

            decimal subtotal = carrito.Sum(i => i.Subtotal);

            decimal costoEnvio =
                tipoEntrega == "Recojo" || subtotal > 100m
                    ? 0m
                    : 10m;

            decimal total = subtotal + costoEnvio;

            var pedido = new Pedido
            {
                UsuarioID = usuarioId.Value,
                FechaPedido = DateTime.Now,
                Estado = "Pendiente",
                Total = total,
                DireccionEnvio = direccion.Trim(),
                Ciudad = distrito.Trim(),
                TelefonoContacto = telefono.Trim(),
                MetodoPago = metodoPago.Trim(),
                Detalles = new List<DetallePedido>()
            };

            foreach (var item in carrito)
            {
                var producto = item.Producto!;

                pedido.Detalles.Add(new DetallePedido
                {
                    ProductoID = item.ProductoID,
                    Cantidad = item.Cantidad,
                    PrecioUnitario = producto.Precio
                });

                // Descontar del inventario lo que se está comprando.
                producto.Stock -= item.Cantidad;
            }

            _context.Pedidos.Add(pedido);

            // Eliminar únicamente los artículos de este usuario.
            _context.CarritoItems.RemoveRange(carrito);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return RedirectToAction(
                nameof(PedidoConfirmado),
                new { id = pedido.PedidoID });
        }

        // GET: /Carrito/PedidoConfirmado
        [HttpGet]
        public async Task<IActionResult> PedidoConfirmado(int id)
        {
            int? usuarioId = HttpContext.Session.GetInt32("UsuarioID");

            if (!usuarioId.HasValue)
            {
                return RedirectToAction("Login", "Cuenta");
            }

            var pedido = await _context.Pedidos
                .Include(p => p.Detalles)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(p =>
                    p.PedidoID == id &&
                    p.UsuarioID == usuarioId.Value);

            if (pedido == null)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(pedido);
        }
    }
}

