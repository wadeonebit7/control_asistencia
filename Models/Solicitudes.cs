using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace control_asistencia.Models
{
    [Table("solicitudes")]
    public class Solicitudes
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Required]
        [Column("estado")]
        public bool Estado { get; set; } = true;

        [Required]
        [Column("id_usuario")]
        public int IdUsuario { get; set; }

        [ForeignKey("IdUsuario")]
        public virtual Usuarios? Usuario { get; set; }

        [Required]
        [Column("tipo_solicitud", TypeName = "varchar(50)")]
        public string TipoSolicitud { get; set; } = null!; // 'LICENCIA_MEDICA', 'AJUSTE_ASISTENCIA'

        [Required]
        [Column("motivo")]
        [StringLength(255)]
        public string Motivo { get; set; } = null!;

        [Required]
        [Column("ruta_documento")]
        [StringLength(255)]
        public string RutaDocumento { get; set; } = null!;

        [Required]
        [Column("estado_solicitud", TypeName = "varchar(50)")]
        public string EstadoSolicitud { get; set; } = "PENDIENTE"; // 'PENDIENTE', 'APROBADO', 'RECHAZADO'

        [Required]
        [Column("CreateAt")]
        public DateTime CreateAt { get; set; } = DateTime.Now;
    }
}