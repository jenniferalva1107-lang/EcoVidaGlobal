
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcoVidaGlobal.Models
{
    [Table("SolicitudesReposicion")]
    public class SolicitudReposicion
    {
        [Key]
        public int SolicitudID { get; set; }

        public int ProductoID { get; set; }

        public int UsuarioSolicitaID { get; set; }

        public int? UsuarioApruebaID { get; set; }

        [Range(10, int.MaxValue)]
        public int CantidadSolicitada { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioUnitarioReferencia { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal DescuentoPorcentaje { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MontoEstimado { get; set; }

        [Required]
        [StringLength(100)]
        public string Proveedor { get; set; } = "EcoStore";

        [Required]
        [StringLength(150)]
        public string Concepto { get; set; } =
            "Reposición de producto";

        [Required]
        [StringLength(20)]
        public string Estado { get; set; } = "Pendiente";

        public DateTime FechaSolicitud { get; set; } =
            DateTime.Now;

        public DateTime? FechaDecision { get; set; }

        public DateTime? FechaInicioEntrega { get; set; }

        public DateTime? FechaRecepcion { get; set; }

        public int CantidadRecibida { get; set; }

        public int CantidadTransferida { get; set; }

        public int? GastoID { get; set; }
        public Producto? Producto { get; set; }
    }
}
