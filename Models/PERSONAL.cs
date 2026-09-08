using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace control_asistencia.Models
{
    public class PERSONAL
    {
        [Key]
        public int ID { get; set; }

        public bool STATE { get; set; }

        [Required(ErrorMessage = "El rol es obligatorio")]
        public int ID_ROL { get; set; }

        [ForeignKey("ID_ROL")]
        public virtual ROL? ROL { get; set; }


        [Required(ErrorMessage = "El RUT es obligatorio")]
        [StringLength(12, ErrorMessage = "El RUT no puede superar los 12 caracteres")]
        public string RUT { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 100 caracteres")]
        public string NOMBRE { get; set; } = string.Empty;

        [Required(ErrorMessage = "El apellido es obligatorio")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "El apellido debe tener entre 3 y 100 caracteres")]
        public string APELLIDO { get; set; } = string.Empty;

        [Required(ErrorMessage = "La dirección es obligatoria")]
        [StringLength(100, MinimumLength = 10, ErrorMessage = "La dirección debe tener entre 10 y 100 caracteres")]
        public string DIRECCION { get; set; } = string.Empty;

        [Required(ErrorMessage = "El teléfono es obligatorio")]
        [Phone(ErrorMessage = "Ingrese un teléfono válido")]
        public string TELEFONO { get; set; } = string.Empty;
    }
}