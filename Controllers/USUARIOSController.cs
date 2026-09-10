using control_asistencia.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace control_asistencia.Controllers
{
    public class UsuariosController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsuariosController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Logout()
        {
            return View();
        }

       


        public IActionResult LogoutAsistencia()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string correo, string password)
        {
            // Validamos que los campos no vengan vacíos
            if (string.IsNullOrEmpty(correo) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Por favor, complete todos los campos.";
                return View("Index");
            }

            // Consulta ajustada: Como eliminamos el correo de la tabla Usuarios, 
            // ahora accedemos a él a través de la relación u.Personal.Correo
            var usuario = await _context.Usuarios
                .Include(u => u.Personal)
                    .ThenInclude(p => p.Rol)
                .FirstOrDefaultAsync(u => u.Personal.Correo == correo && u.Password == password && u.Estado == true);

            if (usuario != null)
            {
                string nombreCompleto = $"{usuario.Personal.Nombre} {usuario.Personal.Apellido}";
                string nombreRol = usuario.Personal.Rol.Nombre;

                TempData["UsuarioLogueado"] = nombreCompleto;
                TempData["RolUsuario"] = nombreRol;

                // Redirección limpia basada en el rol de la base de datos
                if (nombreRol == "ADMIN")
                {
                    TempData["Mensaje"] = $"¡Bienvenido Administrador: {nombreCompleto}!";
                    return RedirectToAction("Index", "Administrador");
                }
                else if (nombreRol == "EMPLEADO")
                {
                    TempData["Mensaje"] = $"¡Hola {nombreCompleto}!";
                     return RedirectToAction("Index", "Trabajador");
                    
                }
                else if (nombreRol == "RRHH")
                {
                    TempData["Mensaje"] = $"¡Bienvenido Recursos Humanos: {nombreCompleto}!";
                    // return RedirectToAction("Index", "RecursosHumanos");
                    return View("Index");
                }
                else
                {
                    return RedirectToAction("Index", "Usuarios");
                }
            }

            // Si las credenciales son incorrectas, recarga el formulario de Login con el error
            ViewBag.Error = "Correo o contraseña incorrectos.";
            return View("Logout");
        }
    }
}