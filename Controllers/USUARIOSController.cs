using control_asistencia.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace control_asistencia.Controllers
{
    public class USUARIOSController : Controller
    {


        private readonly ApplicationDbContext _context;

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet] 
        public IActionResult Logout()
        {

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]

        public async Task<IActionResult> Login(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Por favor, complete todos los campos.";
                return View();
            }

            var usuario = await _context.USUARIOS
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.Username == username && u.Password == password);



            if (usuario != null)
            {
                TempData["UsuarioLogueado"] = usuario.Username;
                TempData["RolUsuario"] = usuario.Rol?.Name;


                if (usuario.Rol?.Name == "Administrador")
                {
                    TempData["Mensaje"] = $"¡Bienvenido Administrador: {usuario.Username}!";
                    return RedirectToAction("Eleccion", "Home");
                }
                else
                {
                    TempData["Mensaje"] = $"¡Hola {usuario.Username}! Listo para jugar.";

                    return RedirectToAction("PanelJugador", "Home");
                }
            }

            ViewBag.Error = "Nombre de usuario o contraseña incorrectos.";
            return View();
        }







    }
}
