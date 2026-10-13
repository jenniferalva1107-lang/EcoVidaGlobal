using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcoVidaGlobal.Models
{
    [Table("DetallePedidos")]
    public class DetallePedido
    {
        [Key]
        [Column("DetalleID")] // Indica a Entity Framework que mapee DetallePedidoID a la columna DetalleID de la BD
        public int DetallePedidoID { get; set; }

        public int PedidoID { get; set; }

        public int ProductoID { get; set; }

        public int Cantidad { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioUnitario { get; set; }

        // Propiedades de navegación
        [ForeignKey("PedidoID")]
        public virtual Pedido? Pedido { get; set; }

        [ForeignKey("ProductoID")]
        public virtual Producto? Producto { get; set; }
    }
}