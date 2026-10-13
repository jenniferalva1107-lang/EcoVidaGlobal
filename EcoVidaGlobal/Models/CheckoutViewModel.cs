using System.ComponentModel.DataAnnotations;

namespace EcoVidaGlobal.Models
{
    public class CheckoutViewModel
    {
        public List<CarritoItem> ItemsCarrito { get; set; } = new List<CarritoItem>();

        public decimal Subtotal => ItemsCarrito.Sum(i => (i.Producto?.Precio ?? 0) * i.Cantidad);
        public decimal CostoEnvio => Subtotal > 100 || Subtotal == 0 ? 0 : 10.00m; // Envíos gratis arriba de S/. 100
        public decimal Total => Subtotal + CostoEnvio;

        [Required(ErrorMessage = "La dirección de envío es obligatoria.")]
        [Display(Name = "Dirección de Envío")]
        public string DireccionEnvio { get; set; } = string.Empty;

        [Required(ErrorMessage = "La ciudad es obligatoria.")]
        [Display(Name = "Ciudad / Distrito")]
        public string Ciudad { get; set; } = string.Empty;

        [Required(ErrorMessage = "El teléfono de contacto es obligatorio.")]
        [Phone(ErrorMessage = "Ingrese un número telefónico válido.")]
        [Display(Name = "Teléfono de Contacto")]
        public string TelefonoContacto { get; set; } = string.Empty;

        [Required(ErrorMessage = "Seleccione un método de pago.")]
        [Display(Name = "Método de Pago")]
        public string MetodoPago { get; set; } = "Tarjeta";

        public string? NotasAdicionales { get; set; }
    }
}