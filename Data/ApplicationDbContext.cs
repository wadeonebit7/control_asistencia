using control_asistencia.Models;
using Microsoft.EntityFrameworkCore;

namespace control_asistencia.Data
{
    public class ApplicationDbContext : DbContext
    {

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext>options):base(options) { }

        public DbSet<ROL> ROL { get; set; }

        public DbSet<USUARIOS> USUARIOS {  get; set; }

        public DbSet<PERSONAL> PERSONAL { get; set; }

    }
}
