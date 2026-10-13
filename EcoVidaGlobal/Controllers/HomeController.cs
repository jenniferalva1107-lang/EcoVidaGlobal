using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EcoVidaGlobal.Data;

namespace EcoVidaGlobal.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: / (Portada principal limpia)
        public async Task<IActionResult> Index()
        {
            // Cargar hasta 4 productos para la sección "Productos Destacados"
            var productosDestacados = await _context.Productos
                .Include(p => p.Categoria)
                .Take(4)
                .ToListAsync();

            return View(productosDestacados);
        }

        // GET: /Home/Nosotros (Página independiente)
        public IActionResult Nosotros()
        {
            return View();
        }

        // GET: /Home/Contacto (Página independiente)
        public IActionResult Contacto()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}