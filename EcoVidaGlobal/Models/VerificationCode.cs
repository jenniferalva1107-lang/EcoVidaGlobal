using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EcoVidaGlobal.Models
{
    [Table("VerificationCodes")]
    public class VerificationCode
    {
        [Key]
        [Column("VerificationCodeID")]
        public int VerificationCodeID { get; set; }

        [Column("UsuarioID")]
        public int UsuarioID { get; set; }

        [Required]
        [StringLength(10)]
        [Column("Code")]
        public string Code { get; set; } = string.Empty;

        [Column("ExpirationDate")]
        public DateTime ExpirationDate { get; set; }

        [Column("IsUsed")]
        public bool IsUsed { get; set; } = false;

        [StringLength(30)]
        [Column("TipoAccion")]
        public string TipoAccion { get; set; } = "LOGIN";

        [ForeignKey("UsuarioID")]
        public virtual Usuario? Usuario { get; set; }
    }
}