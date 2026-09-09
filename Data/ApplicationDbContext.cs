using control_asistencia.Models;
using Microsoft.EntityFrameworkCore;

namespace control_asistencia.Data
{
    public class ApplicationDbContext : DbContext
    {

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext>options):base(options) { }


        public DbSet<Personal> Personal { get; set; }
        public DbSet<Rol> Rol { get; set; }
        public DbSet<Usuarios> Usuarios { get; set; }

    }
}
