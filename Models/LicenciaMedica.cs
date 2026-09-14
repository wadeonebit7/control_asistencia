using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace control_asistencia.Models
{
    [Table("licencia_medica")]
    public class LicenciaMedica
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
        [Column("folio")]
        public int Folio { get; set; }

        [Required]
        [Column("profesional")]
        [StringLength(100)]
        public string Profesional { get; set; } = null!;

        [Required]
        [Column("fecha_otorgamiento", TypeName = "date")]
        public DateTime FechaOtorgamiento { get; set; }

        [Required]
        [Column("fecha_inicio", TypeName = "date")]
        public DateTime FechaInicio { get; set; }

        [Required]
        [Column("fecha_termino", TypeName = "date")]
        public DateTime FechaTermino { get; set; }

        [Required]
        [Column("tipificacion", TypeName = "varchar(50)")]
        public string Tipificacion { get; set; } = null!; // 'TIPO_1' al 'TIPO_7'
    }
}