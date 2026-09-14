using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace control_asistencia.Models
{
    [Table("log_modificacion")]
    public class LogModificacion
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("id_log")]
        public int IdLog { get; set; }

        [ForeignKey("IdLog")]
        public virtual Log? Log { get; set; }

        [Required]
        [Column("tabla")]
        [StringLength(100)]
        public string Tabla { get; set; } = null!;

        [Required]
        [Column("columna")]
        [StringLength(100)]
        public string Columna { get; set; } = null!;

        [Required]
        [Column("valor_antiguo")]
        [StringLength(255)]
        public string ValorAntiguo { get; set; } = null!;

        [Required]
        [Column("valor_nuevo")]
        [StringLength(255)]
        public string ValorNuevo { get; set; } = null!;
    }
}