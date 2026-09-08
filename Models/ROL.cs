using System.ComponentModel.DataAnnotations;

namespace control_asistencia.Models
{




    public class ROL
    {
        [Key]
        public int ID { get; set; }


        [Required]
        public bool State { get; set; }

        [Required(ErrorMessage = "El nombre del rol es obligatorio")]
        [StringLength(35 , MinimumLength =4 , ErrorMessage ="El minimo es entre 4 y 35 caracteres para asignar nombre a un rol")]
        public string Nombre { get; set; }




    }
}