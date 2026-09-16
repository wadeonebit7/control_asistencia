using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace control_asistencia.Models
{
    [Table("log")]
    public class Log
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("id_solicitudes")]
        public int IdSolicitudes { get; set; }

        [ForeignKey("IdSolicitudes")]
        public virtual Solicitudes? Solicitudes { get; set; }

        [Required]
        [Column("revisado_por")]
        public int RevisadoPor { get; set; } // Esta es la llave foránea numérica (int)

        [ForeignKey("RevisadoPor")]
        public virtual Usuarios? AdministradorRevisor { get; set; } // Esta es la navegación al objeto Usuario

        [Column("respuesta")]
        [StringLength(255)]
        public string? Respuesta { get; set; }

        [Required]
        [Column("CreateAt")]
        public DateTime CreateAt { get; set; } = DateTime.Now;

        // Relación con los detalles de modificación (Obligatorio para el Include)
        public virtual ICollection<LogModificacion> LogModificaciones { get; set; } = new List<LogModificacion>();
    }
}