using Microsoft.EntityFrameworkCore;
using EcoVidaGlobal.Models;

namespace EcoVidaGlobal.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Rol> Roles { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<VerificationCode> VerificationCodes { get; set; }
        public DbSet<Pedido> Pedidos { get; set; }
        public DbSet<DetallePedido> DetallePedidos { get; set; }
        public DbSet<CarritoItem> CarritoItems { get; set; }
        public DbSet<Gasto> Gastos { get; set; }
        public DbSet<SolicitudReposicion> SolicitudesReposicion { get; set; }

    }
}