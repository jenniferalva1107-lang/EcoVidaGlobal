using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcoVidaGlobal.Models
{
    public class Producto
    {
        [Key]
        public int ProductoID { get; set; }

        [Required]
        [StringLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Descripcion { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Precio { get; set; }

        [Required]
        public int Stock { get; set; }

        public string? ImagenUrl { get; set; }

        public int CategoriaID { get; set; }

        [ForeignKey("CategoriaID")]
        public Categoria? Categoria { get; set; }

        // Propiedad no mapeada a SQL Server con lectura y escritura
        [NotMapped]
        public decimal? PrecioAnterior { get; set; }
    }
}