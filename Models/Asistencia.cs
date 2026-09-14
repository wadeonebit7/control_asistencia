using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace control_asistencia.Models
{
    [Table("asistencia")]
    public class Asistencia
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("estado")]
        public bool Estado { get; set; } = true;

        [Required(ErrorMessage = "El ID del usuario es obligatorio")]
        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        [ForeignKey("IdUsuario")]
        public virtual Usuarios? Usuario { get; set; }

        [Required(ErrorMessage = "El ID de habilitar asistencia es obligatorio")]
        [Column("id_habilitar_asistencia")]
        public int IdHabilitarAsistencia { get; set; }

        [Column("hora_entrada_real")]
        public TimeSpan HoraEntradaReal { get; set; } = TimeSpan.Zero;

        [Column("hora_salida_real")]
        public TimeSpan HoraSalidaReal { get; set; } = TimeSpan.Zero;

        [Required]
        [Column("estado_entrada")]
        public string EstadoEntrada { get; set; } = "PENDIENTE";

        [Required]
        [Column("estado_salida")]
        public string EstadoSalida { get; set; } = "PENDIENTE";

        // NUEVOS CAMPOS AGREGADOS (Permiten Null)
        [Column("horas_reales")]
        public TimeSpan? HorasReales { get; set; }

        [Column("horas_trabajadas")]
        public TimeSpan? HorasTrabajadas { get; set; }

        [Required]
        [Column("CreatAt")]
        public DateTime CreateAt { get; set; } = DateTime.Now;
    }
}