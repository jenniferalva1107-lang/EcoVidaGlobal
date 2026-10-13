using EcoVidaGlobal.Models;

namespace EcoVidaGlobal.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(ApplicationDbContext context)
        {
            // Crear la base de datos si aún no existe
            await context.Database.EnsureCreatedAsync();

            // 1. Agregar Categorías si la tabla está vacía
            if (!context.Categorias.Any())
            {
                var categorias = new List<Categoria>
                {
                    new Categoria { Nombre = "Cuidado Personal", Descripcion = "Productos de aseo e higiene personal libres de plástico." },
                    new Categoria { Nombre = "Hogar Sostenible", Descripcion = "Artículos ecológicos para la cocina, limpieza y hogar." },
                    new Categoria { Nombre = "Reutilizables", Descripcion = "Pajillas, termos, bolsas y cubiertos reutilizables." }
                };

                await context.Categorias.AddRangeAsync(categorias);
                await context.SaveChangesAsync();
            }

            // 2. Agregar Productos si la tabla está vacía
            if (!context.Productos.Any())
            {
                var catCuidado = context.Categorias.FirstOrDefault(c => c.Nombre == "Cuidado Personal");
                var catHogar = context.Categorias.FirstOrDefault(c => c.Nombre == "Hogar Sostenible");
                var catReutilizable = context.Categorias.FirstOrDefault(c => c.Nombre == "Reutilizables");

                var productos = new List<Producto>
                {
                    new Producto
                    {
                        Nombre = "Cepillo de Dientes de Bambú",
                        Descripcion = "Cepillo con cerdas suaves biodegradables y mango de bambú 100% compostable.",
                        Precio = 12.50m,
                        Stock = 50,
                        CategoriaID = catCuidado?.CategoriaID ?? 1,
                        ImagenUrl = "https://images.unsplash.com/photo-1607613009820-a29f7bb81c04?auto=format&fit=crop&w=500&q=80"
                    },
                    new Producto
                    {
                        Nombre = "Shampoo Sólido de Orgánico",
                        Descripcion = "Barra de shampoo artesanal libre de sulfatos, parabenos y empaques plásticos.",
                        Precio = 28.00m,
                        Stock = 30,
                        CategoriaID = catCuidado?.CategoriaID ?? 1,
                        ImagenUrl = "https://images.unsplash.com/photo-1608248597279-f99d160bfcbc?auto=format&fit=crop&w=500&q=80"
                    },
                    new Producto
                    {
                        Nombre = "Esponja Vegetal Luffa",
                        Descripcion = "Esponja 100% natural y biodegradable para baño o lavado de vajilla.",
                        Precio = 9.90m,
                        Stock = 40,
                        CategoriaID = catHogar?.CategoriaID ?? 2,
                        ImagenUrl = "https://images.unsplash.com/photo-1584308666744-24d5c474f2ae?auto=format&fit=crop&w=500&q=80"
                    },
                    new Producto
                    {
                        Nombre = "Botella Térmica de Acero Inoxidable",
                        Descripcion = "Botella reutilizable de 750ml que mantiene frío durante 24h y calor por 12h.",
                        Precio = 45.00m,
                        Stock = 25,
                        CategoriaID = catReutilizable?.CategoriaID ?? 3,
                        ImagenUrl = "https://images.unsplash.com/photo-1602143407151-7111542de6e8?auto=format&fit=crop&w=500&q=80"
                    },
                    new Producto
                    {
                        Nombre = "Set de Cubiertos de Bambú",
                        Descripcion = "Incluye tenedor, cuchillo, cuchara, sorbete y cepillo con funda de tela ecológica.",
                        Precio = 22.00m,
                        Stock = 35,
                        CategoriaID = catReutilizable?.CategoriaID ?? 3,
                        ImagenUrl = "https://images.unsplash.com/photo-1584345604325-f5091269a0d1?auto=format&fit=crop&w=500&q=80"
                    }
                };

                await context.Productos.AddRangeAsync(productos);
                await context.SaveChangesAsync();
            }
        }
    }
}