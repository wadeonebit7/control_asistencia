using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace control_asistencia.Models
{
    [Table("usuarios")]
    public class Usuarios
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("estado")]
        public bool Estado { get; set; }

        [Required(ErrorMessage = "El ID del personal es obligatorio")]
        [Column("id_personal")]
        public int IdPersonal { get; set; }

        [ForeignKey("IdPersonal")]
        public virtual Personal? Personal { get; set; }

        [Required(ErrorMessage = "El estatus es obligatorio")]
        [StringLength(35, ErrorMessage = "El estatus no puede superar los 35 caracteres")]
        [Column("status")]
        public string Status { get; set; } = string.Empty;

        // IMPORTANTE: Quitamos el [Required] y agregamos '?' al string porque ahora acepta NULL
        [StringLength(100, ErrorMessage = "La clave dinámica no puede superar los 100 caracteres")]
        [Column("clave_dinamica")]
        public string? ClaveDinamica { get; set; }

        // ELIMINAMOS LA PROPIEDAD CORREO DE AQUÍ PORQUE YA NO EXISTE EN LA TABLA USUARIOS

        [Required(ErrorMessage = "La contraseña es obligatoria")]
        [StringLength(255, ErrorMessage = "La contraseña no puede superar los 255 caracteres")]
        [Column("password")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "La fecha de creación es obligatoria")]
        [Column("CreateAt")]
        public DateTime CreateAt { get; set; }
    }
}