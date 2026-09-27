using System;
using System.Collections.Generic;

namespace EcoVidaGlobal.Models
{
    public class Pedido
    {
        public int PedidoID { get; set; }
        public int UsuarioID { get; set; }
        public DateTime FechaPedido { get; set; }
        public decimal Total { get; set; }
        public string Estado { get; set; } = "Pendiente";

        // AGREGAR ESTA LÍNEA QUE FALTABA:
        public string? DireccionEnvio { get; set; }

        public virtual Usuario? Usuario { get; set; }
        public virtual ICollection<DetallePedido> Detalles { get; set; } = new List<DetallePedido>();
    }
}