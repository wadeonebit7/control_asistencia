using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace control_asistencia.Models
{
    [Table("habilitar_asistencia")]
    public class habilitar_asistencia
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required(ErrorMessage = "El campo 'abierto_por' es obligatorio")]
        [Column("abierto_por")]
        public int AbiertoPor { get; set; }

        // Propiedad de navegación para relacionar con la tabla usuarios
        [ForeignKey("AbiertoPor")]
        public virtual Usuarios? UsuarioAdministrador { get; set; }

        [Required(ErrorMessage = "La fecha es obligatoria")]
        [Column("fecha", TypeName = "date")]
        public DateTime Fecha { get; set; }

        [Required(ErrorMessage = "La hora de entrada es obligatoria")]
        [Column("hora_entrada")]
        public TimeSpan HoraEntrada { get; set; } = new TimeSpan(9, 30, 0);

        [Required(ErrorMessage = "La hora de salida es obligatoria")]
        [Column("hora_salida")]
        public TimeSpan HoraSalida { get; set; } = new TimeSpan(17, 30, 0);

        [Required(ErrorMessage = "La fecha de creación es obligatoria")]
        [Column("CreatAt")]
        public DateTime CreatAt { get; set; } = DateTime.Now;
    }
}