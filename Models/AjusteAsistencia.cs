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

        // Se eliminó IdAsistencia y su llave foránea para independizar el registro

        [Required]
        [Column("fecha_afectada", TypeName = "date")]
        public DateTime FechaAfectada { get; set; }

        [Required]
        [Column("tipo_incidencia", TypeName = "varchar(50)")]
        public string TipoIncidencia { get; set; } = null!; // 'ENTRADA', 'SALIDA', 'NO MARCADA'

        [Required]
        [Column("hora_entrada")]
        public TimeSpan HoraEntrada { get; set; }

        [Required]
        [Column("hora_salida")]
        public TimeSpan HoraSalida { get; set; }

        [Required]
        [Column("motivo")]
        [StringLength(255)]
        public string Motivo { get; set; } = null!;

        [Required]
        [Column("emitido_por")]
        [StringLength(255)]
        public string EmitidoPor { get; set; } = null!;
    }
}