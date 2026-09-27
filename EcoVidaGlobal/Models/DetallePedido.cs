using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcoVidaGlobal.Models
{
    public class DetallePedido
    {
        [Key]
        public int DetalleID { get; set; }

        public int PedidoID { get; set; }

        public int ProductoID { get; set; }

        public int Cantidad { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioUnitario { get; set; }

        [ForeignKey("PedidoID")]
        public Pedido? Pedido { get; set; }

        [ForeignKey("ProductoID")]
        public Producto? Producto { get; set; }
    }
}