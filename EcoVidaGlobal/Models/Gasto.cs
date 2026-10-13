
using System;
using System.ComponentModel.DataAnnotations;

namespace EcoVidaGlobal.Models
{
    public class Gasto
    {
        public int GastoID { get; set; }

        [Required]
        [StringLength(150)]
        public string Concepto { get; set; } = string.Empty;

        [Required]
        [StringLength(60)]
        public string Categoria { get; set; } = string.Empty;


        [Range(
            typeof(decimal),
            "0.01",
            "999999999",
            ParseLimitsInInvariantCulture = true,
            ConvertValueInInvariantCulture = true,
            ErrorMessage = "El monto debe ser mayor que cero."
        )]
        public decimal Monto { get; set; }


        public DateTime FechaRegistro { get; set; } = DateTime.Now;
    }
}