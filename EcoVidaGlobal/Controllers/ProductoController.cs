using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcoVidaGlobal.Data;
using EcoVidaGlobal.Models;

namespace EcoVidaGlobal.Controllers
{
    public class ProductoController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductoController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Producto (Catálogo general)
        public async Task<IActionResult> Index(string? categoria)
        {
            var query = _context.Productos.Include(p => p.Categoria).AsQueryable();

            if (!string.IsNullOrEmpty(categoria))
            {
                query = query.Where(p => p.Categoria != null && p.Categoria.Nombre.ToLower() == categoria.ToLower());
                ViewBag.CategoriaSeleccionada = categoria;
            }
            else
            {
                ViewBag.CategoriaSeleccionada = "Todas";
            }

            var productos = await query.ToListAsync();
            return View(productos);
        }

        // GET: /Producto/PorCategoria?nombreCategoria=Cuidado Personal
        public async Task<IActionResult> PorCategoria(string nombreCategoria)
        {
            var productos = await _context.Productos
                .Include(p => p.Categoria)
                .Where(p => p.Categoria != null && p.Categoria.Nombre.ToLower() == nombreCategoria.ToLower())
                .ToListAsync();

            ViewBag.CategoriaSeleccionada = nombreCategoria;
            return View("Index", productos);
        }
    }
}