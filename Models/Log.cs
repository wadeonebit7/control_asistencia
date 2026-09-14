using System;
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
        public virtual Solicitudes? Solicitud { get; set; }

        [Required]
        [Column("revisado_por")]
        public int RevisadoPor { get; set; }

        [ForeignKey("RevisadoPor")]
        public virtual Usuarios? AdministradorRevisor { get; set; }

        [Column("respuesta")]
        [StringLength(255)]
        public string? Respuesta { get; set; }

        [Required]
        [Column("CreateAt")]
        public DateTime CreateAt { get; set; } = DateTime.Now;
    }
}