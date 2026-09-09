using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace control_asistencia.Models
{
    [Table("rol")]
    public class Rol
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("estado")]
        public bool Estado { get; set; }

        [Required(ErrorMessage = "El nombre del rol es obligatorio")]
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;
    }
}