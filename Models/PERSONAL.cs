using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace control_asistencia.Models
{
    [Table("personal")]
    public class Personal
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("estado")]
        public bool Estado { get; set; }

        [Required(ErrorMessage = "El ID del rol es obligatorio")]
        [Column("id_rol")]
        public int IdRol { get; set; }

        [ForeignKey("IdRol")]
        public virtual Rol? Rol { get; set; }

        [Required(ErrorMessage = "El RUT es obligatorio")]
        [StringLength(15, ErrorMessage = "El RUT no puede superar los 15 caracteres")]
        [Column("rut")]
        public string Rut { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100, ErrorMessage = "El nombre no puede superar los 100 caracteres")]
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido es obligatorio")]
        [StringLength(100, ErrorMessage = "El apellido no puede superar los 100 caracteres")]
        [Column("apellido")]
        public string Apellido { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo es obligatorio")]
        [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido")]
        [StringLength(150, ErrorMessage = "El correo no puede superar los 150 caracteres")]
        [Column("correo")]
        public string Correo { get; set; } = string.Empty;

        [Required(ErrorMessage = "La dirección es obligatoria")]
        [StringLength(200, ErrorMessage = "La dirección no puede superar los 200 caracteres")]
        [Column("direccion")]
        public string Direccion { get; set; } = string.Empty;

        [Required(ErrorMessage = "El celular es obligatorio")]
        [StringLength(15, ErrorMessage = "El celular no puede superar los 15 caracteres")]
        [Column("celular")]
        public string Celular { get; set; } = string.Empty;
    }
}