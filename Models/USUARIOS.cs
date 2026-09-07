using System.ComponentModel.DataAnnotations;

namespace control_asistencia.Models
{

    public class USUARIOS
    {
        [Key]
        public int ID { get; set; }

        public bool State { get; set; }

        [Required(ErrorMessage = "El ID del personal es obligatorio")]
        public int ID_Personal { get; set; }

        [Required(ErrorMessage = "El estatus es obligatorio")]
        [StringLength(30, ErrorMessage = "El estatus no puede superar los 30 caracteres")]
        public string Estatus { get; set; } = string.Empty;

        [Required(ErrorMessage = "La clave dinámica es obligatoria")]
        [StringLength(10, MinimumLength = 4, ErrorMessage = "La clave dinámica debe tener entre 4 y 10 caracteres")]
        public string Clave_Dinamica { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo es obligatorio")]
        [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido")]
        [StringLength(100, ErrorMessage = "El correo no puede superar los 100 caracteres")]
        public string Correo { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener entre 8 y 100 caracteres")]
        public string Contraseña { get; set; } = string.Empty;

        [Required(ErrorMessage = "La fecha de creación es obligatoria")]
        public DateTime Createat { get; set; }
    }


}
