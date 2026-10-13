
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcoVidaGlobal.Models
{
    public class Pedido
    {
        [Key]
        public int PedidoID { get; set; }

        public int UsuarioID { get; set; }
        public Usuario? Usuario { get; set; }

        public DateTime FechaPedido { get; set; } = DateTime.Now;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }

        // Estado de preparación y entrega del pedido
        [Required, StringLength(30)]
        public string Estado { get; set; } = "Pendiente";

        // Estado del pago: Pendiente o Pagado
        [Required, StringLength(20)]
        public string EstadoPago { get; set; } = "Pendiente";

        public DateTime? FechaPago { get; set; }

        [Required, StringLength(200)]
        public string DireccionEnvio { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string Ciudad { get; set; } = string.Empty;

        [Required, StringLength(20)]
        public string TelefonoContacto { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string MetodoPago { get; set; } = "Tarjeta";

        public List<DetallePedido> Detalles { get; set; }
            = new List<DetallePedido>();
    }
}