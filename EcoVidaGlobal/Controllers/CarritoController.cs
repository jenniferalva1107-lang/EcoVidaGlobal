using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcoVidaGlobal.Data;
using EcoVidaGlobal.Models;
using EcoVidaGlobal.Extensions;
using EcoVidaGlobal.Services;

namespace EcoVidaGlobal.Controllers
{
    public class CarritoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private const string CARRITO_SESSION_KEY = "CarritoCompras";

        public CarritoController(ApplicationDbContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // GET: /Carrito
        public IActionResult Index()
        {
            var carrito = ObtenerCarrito();
            return View(carrito);
        }

        // POST: /Carrito/Agregar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Agregar(int productoId, int cantidad = 1, string returnUrl = "/Producto")
        {
            var usuarioNombre = HttpContext.Session.GetString("UsuarioNombre");
            if (string.IsNullOrEmpty(usuarioNombre))
            {
                return RedirectToAction("Login", "Cuenta", new { mensaje = "Debes Iniciar Sesión primero para realizar tus compras." });
            }

            var producto = await _context.Productos.FindAsync(productoId);
            if (producto == null) return NotFound();

            var carrito = ObtenerCarrito();
            var itemExistente = carrito.FirstOrDefault(c => c.ProductoID == productoId);

            if (itemExistente != null)
            {
                itemExistente.Cantidad += cantidad;
            }
            else
            {
                carrito.Add(new ItemCarrito
                {
                    ProductoID = producto.ProductoID,
                    Nombre = producto.Nombre,
                    Precio = producto.Precio,
                    Cantidad = cantidad,
                    ImagenUrl = producto.ImagenUrl
                });
            }

            GuardarCarrito(carrito);
            return Redirect(returnUrl);
        }

        // POST: /Carrito/Eliminar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Eliminar(int productoId, string returnUrl = "/Carrito")
        {
            var carrito = ObtenerCarrito();
            carrito.RemoveAll(c => c.ProductoID == productoId);
            GuardarCarrito(carrito);
            return Redirect(returnUrl);
        }

        // POST: /Carrito/Vaciar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Vaciar()
        {
            HttpContext.Session.Remove(CARRITO_SESSION_KEY);
            return RedirectToAction("Index", "Producto");
        }

        // POST: /Carrito/ProcesarPago
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcesarPago()
        {
            int? usuarioId = HttpContext.Session.GetInt32("UsuarioID");
            if (!usuarioId.HasValue)
            {
                return RedirectToAction("Login", "Cuenta", new { mensaje = "Debes Iniciar Sesión para procesar el pago." });
            }

            var usuario = await _context.Usuarios.FindAsync(usuarioId.Value);
            var carrito = ObtenerCarrito();

            if (usuario == null || !carrito.Any())
            {
                return RedirectToAction(nameof(Index));
            }

            // 1. Crear el Pedido
            var nuevoPedido = new Pedido
            {
                UsuarioID = usuario.UsuarioID,
                FechaPedido = DateTime.Now,
                Total = carrito.Sum(c => c.Subtotal),
                Estado = "Completado"
            };

            _context.Pedidos.Add(nuevoPedido);
            await _context.SaveChangesAsync();

            // 2. Guardar Detalles y actualizar Stock
            foreach (var item in carrito)
            {
                var producto = await _context.Productos.FindAsync(item.ProductoID);
                if (producto != null)
                {
                    producto.Stock -= item.Cantidad;
                    if (producto.Stock < 0) producto.Stock = 0;
                }

                var detalle = new DetallePedido
                {
                    PedidoID = nuevoPedido.PedidoID,
                    ProductoID = item.ProductoID,
                    Cantidad = item.Cantidad,
                    PrecioUnitario = item.Precio
                };

                _context.DetallePedidos.Add(detalle);
            }

            await _context.SaveChangesAsync();

            // 3. Cargar datos completos (incluyendo Usuario)
            var pedidoCompleto = await _context.Pedidos
                .Include(p => p.Usuario)
                .Include(p => p.Detalles)
                .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(p => p.PedidoID == nuevoPedido.PedidoID);

            if (pedidoCompleto != null)
            {
                // Esperar el envío real del correo por SMTP
                await _emailService.EnviarComprobanteCompraAsync(usuario.Correo, usuario.NombreCompleto, pedidoCompleto);
            }

            // 4. Limpiar Carrito de la sesión
            HttpContext.Session.Remove(CARRITO_SESSION_KEY);

            return RedirectToAction(nameof(Confirmacion), new { id = nuevoPedido.PedidoID });
        }

        // GET: /Carrito/Confirmacion/5
        public async Task<IActionResult> Confirmacion(int id)
        {
            var pedido = await _context.Pedidos
                .Include(p => p.Usuario)
                .Include(p => p.Detalles)
                .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(p => p.PedidoID == id);

            if (pedido == null) return NotFound();

            return View(pedido);
        }

        private List<ItemCarrito> ObtenerCarrito()
        {
            return HttpContext.Session.GetObjectFromJson<List<ItemCarrito>>(CARRITO_SESSION_KEY) ?? new List<ItemCarrito>();
        }

        private void GuardarCarrito(List<ItemCarrito> carrito)
        {
            HttpContext.Session.SetObjectAsJson(CARRITO_SESSION_KEY, carrito);
        }
    }
}