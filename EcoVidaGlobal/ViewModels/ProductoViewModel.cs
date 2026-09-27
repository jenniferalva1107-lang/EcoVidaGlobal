using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EcoVidaGlobal.ViewModels
{
    public class ProductoViewModel
    {
        public int ProductoID { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;

        public string? Descripcion { get; set; }

        [Required(ErrorMessage = "El precio es obligatorio.")]
        [Range(0.01, 10000.00)]
        public decimal Precio { get; set; }

        [Display(Name = "Precio Anterior (Opcional para Ofertas)")]
        public decimal? PrecioAnterior { get; set; }

        [Required(ErrorMessage = "El stock es obligatorio.")]
        public int Stock { get; set; }

        [Required(ErrorMessage = "Seleccione una categoría.")]
        public int CategoriaID { get; set; }

        public string? ImagenUrl { get; set; }

        public IFormFile? ImagenArchivo { get; set; }
    }
}