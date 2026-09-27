namespace EcoVidaGlobal.Models
{
    public class Categoria
    {
        public int CategoriaID { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public string? Icono { get; set; }

        public List<Producto>? Productos { get; set; }
    }
}