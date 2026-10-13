using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcoVidaGlobal.Models
{
    [Table("CarritoItems")]
    public class CarritoItem
    {
        [Key]
        public int CarritoItemID { get; set; }

        public int UsuarioID { get; set; }

        public int ProductoID { get; set; }

        public int Cantidad { get; set; }

        public DateTime FechaAgregado { get; set; } = DateTime.Now;

        // Propiedad de navegación
        [ForeignKey("ProductoID")]
        public virtual Producto? Producto { get; set; }

        // Propiedades de conveniencia (no se guardan en la BD)
        [NotMapped]
        public string Nombre => Producto?.Nombre ?? "Producto";

        [NotMapped]
        public decimal Precio => Producto?.Precio ?? 0m;

        [NotMapped]
        public string ImagenUrl => Producto?.ImagenUrl ?? "";

        [NotMapped]
        public decimal Subtotal => Precio * Cantidad;
    }
}