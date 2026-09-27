namespace EcoVidaGlobal.Models
{
    public class ItemCarrito
    {
        public int ProductoID { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal Precio { get; set; }
        public int Cantidad { get; set; }
        public string? ImagenUrl { get; set; }

        public decimal Subtotal => Precio * Cantidad;
    }
}