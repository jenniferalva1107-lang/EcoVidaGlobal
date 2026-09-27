using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EcoVidaGlobal.Data;
using EcoVidaGlobal.Models;
using EcoVidaGlobal.ViewModels;

namespace EcoVidaGlobal.Controllers
{
    public class ProductoController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ProductoController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // GET: /Producto
        public async Task<IActionResult> Index()
        {
            var productos = await _context.Productos
                .Include(p => p.Categoria)
                .ToListAsync();

            // Asignar el precio de lista anterior para visualización de descuento
            foreach (var p in productos)
            {
                p.PrecioAnterior = p.Precio * 1.25m;
            }

            return View(productos);
        }

        // GET: /Producto/Crear
        public async Task<IActionResult> Crear()
        {
            if (!EsAdministrador()) return RedirectToAction("Login", "Cuenta");

            ViewBag.Categorias = new SelectList(await _context.Categorias.ToListAsync(), "CategoriaID", "Nombre");
            return View();
        }

        // POST: /Producto/Crear
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(ProductoViewModel model)
        {
            if (!EsAdministrador()) return RedirectToAction("Login", "Cuenta");

            if (ModelState.IsValid)
            {
                string? nombreImagen = null;

                if (model.ImagenArchivo != null)
                {
                    string carpetaImagenes = Path.Combine(_environment.WebRootPath, "images", "productos");
                    if (!Directory.Exists(carpetaImagenes))
                    {
                        Directory.CreateDirectory(carpetaImagenes);
                    }

                    nombreImagen = Guid.NewGuid().ToString() + Path.GetExtension(model.ImagenArchivo.FileName);
                    string rutaCompleta = Path.Combine(carpetaImagenes, nombreImagen);

                    using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                    {
                        await model.ImagenArchivo.CopyToAsync(stream);
                    }
                }

                var producto = new Producto
                {
                    Nombre = model.Nombre,
                    Descripcion = model.Descripcion,
                    Precio = model.Precio,
                    Stock = model.Stock,
                    CategoriaID = model.CategoriaID,
                    ImagenUrl = nombreImagen != null ? "/images/productos/" + nombreImagen : "/images/no-image.png"
                };

                _context.Productos.Add(producto);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Categorias = new SelectList(await _context.Categorias.ToListAsync(), "CategoriaID", "Nombre", model.CategoriaID);
            return View(model);
        }

        // POST: /Producto/Eliminar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Eliminar(int id)
        {
            if (!EsAdministrador()) return RedirectToAction("Login", "Cuenta");

            var producto = await _context.Productos.FindAsync(id);
            if (producto != null)
            {
                _context.Productos.Remove(producto);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool EsAdministrador()
        {
            var rol = HttpContext.Session.GetString("UsuarioRol");
            return rol == "Administrador";
        }
    }
}