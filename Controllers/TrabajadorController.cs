using control_asistencia.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace control_asistencia.Controllers
{
    public class TrabajadorController : Controller
    {
        private readonly ApplicationDbContext _context;

        // Inyección de dependencias para conectar con la base de datos
        public TrabajadorController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? mes)
        {
            // 1. ID del usuario logueado actual (ejemplo temporal)
            int idUsuarioLogueado = 1;

            // 2. Si no eligen mes, tomamos el mes actual por defecto
            int mesConsulta = mes ?? DateTime.Now.Month;

            // 3. Consultamos la base de datos filtrando por usuario y mes
            var registrosAsistencia = await _context.Asistencia
                .Where(a => a.IdUsuario == idUsuarioLogueado && a.CreateAt.Month == mesConsulta)
                .OrderByDescending(a => a.CreateAt)
                .ToListAsync();

            // 4. Mandamos los datos a la vista
            return View(registrosAsistencia);
        }



        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginClaveDinamica(string claveDinamica)
        {
            if (string.IsNullOrEmpty(claveDinamica))
            {
                ViewBag.Error = "Por favor, ingrese su clave dinámica.";
           
                return View("Usuarios/LogoutAsistencia");
            }

          
            var usuario = await _context.Usuarios
                .Include(u => u.Personal)
                    .ThenInclude(p => p.Rol)
                .FirstOrDefaultAsync(u => u.ClaveDinamica == claveDinamica && u.Estado == true);

            if (usuario != null)
            {
                string nombreCompleto = $"{usuario.Personal.Nombre} {usuario.Personal.Apellido}";
                string nombreRol = usuario.Personal.Rol.Nombre;

              
                TempData["UsuarioLogueado"] = nombreCompleto;
                TempData["RolUsuario"] = nombreRol;
                TempData["ClaveDinamica"] = usuario.ClaveDinamica;

                
                return RedirectToAction("EntradaAsistencia");
            }

            ViewBag.Error = "Clave dinámica incorrecta o inactiva.";
            return RedirectToAction("LogoutAsistencia", "Usuarios");
        }

        
        [HttpGet]
        public IActionResult EntradaAsistencia()
        {
            return View();
        }

        
        [HttpGet]
        public IActionResult SalidaAsistencia()
        {
            return View();
        }


















    }
}