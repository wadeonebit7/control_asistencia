using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace control_asistencia.Models
{
    [Table("ajuste_asistencia")]
    public class AjusteAsistencia
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("id_solicitudes")]
        public int IdSolicitudes { get; set; }

        [ForeignKey("IdSolicitudes")]
        public virtual Solicitudes? Solicitud { get; set; }

        [Required]
        [Column("id_asistencia")]
        public int IdAsistencia { get; set; }

        [ForeignKey("IdAsistencia")]
        public virtual Asistencia? AsistenciaAfectada { get; set; }

        [Required]
        [Column("fecha_afectada", TypeName = "date")]
        public DateTime FechaAfectada { get; set; }

        [Required]
        [Column("motivo")]
        [StringLength(255)]
        public string Motivo { get; set; } = null!;
    }
}

