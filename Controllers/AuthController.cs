using control_asistencia.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Threading.Tasks;

namespace control_asistencia.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuthController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            // Verificamos si el usuario ya tiene una sesión activa (cookies)
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                // Buscamos el claim del Rol
                var rol = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

                // Redirigimos según su rol
                if (rol == "ADMIN") return RedirectToAction("Index", "Administrador");
                if (rol == "EMPLEADO") return RedirectToAction("Index", "Trabajador");
                if (rol == "RRHH") return RedirectToAction("Index", "RecursosHumanos");
            }

            // Si no está logueado, le mostramos el formulario de Login normal
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

                // 1. CREACIÓN DE CLAIMS (Carnet de identidad del usuario en el sistema)
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                    new Claim(ClaimTypes.Name, nombreCompleto),
                    new Claim(ClaimTypes.Role, nombreRol),
                    new Claim("IdPersonal", usuario.IdPersonal.ToString()) // Guardamos esto porque nos servirá en RRHH
                };

                // 2. CREACIÓN DE LA COOKIE
                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                // 3. REDIRECCIÓN
                if (nombreRol == "ADMIN") return RedirectToAction("Index", "Administrador");
                else if (nombreRol == "EMPLEADO") return RedirectToAction("Index", "Trabajador");
                else if (nombreRol == "RRHH") return RedirectToAction("Index", "RecursosHumanos"); // Descomentado
                else return RedirectToAction("Index", "Auth");
            }

            // Si las credenciales son incorrectas, recarga el formulario de Login con el error
            ViewBag.Error = "Correo o contraseña incorrectos.";
            return View("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            // Esto destruye la cookie de autenticación del navegador por completo
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Opcional: Limpiar también los mensajes temporales por si acaso
            TempData.Clear();

            // Lo mandamos de vuelta al Login
            return RedirectToAction("Index", "Auth");
        }












        [HttpGet]
        public async Task<IActionResult> LogoutAsistencia()
        {
            var fechaHoy = DateTime.Now.Date;
            var horaActual = DateTime.Now.TimeOfDay;

            var jornada = await _context.habilitar_asistencia
                .FirstOrDefaultAsync(h => h.Fecha.Date == fechaHoy);

            bool sistemaAbierto = false;

            if (jornada != null)
            {
                // Replicamos la misma lógica de los márgenes de tiempo del TrabajadorController
                TimeSpan horaEntradaBD = jornada.HoraEntrada;
                TimeSpan horaSalidaBD = jornada.HoraSalida;

                TimeSpan inicioEntrada = horaEntradaBD.Subtract(new TimeSpan(2, 0, 0));
                if (inicioEntrada < TimeSpan.Zero) inicioEntrada = TimeSpan.Zero;

                TimeSpan cierreSistema = horaSalidaBD.Add(new TimeSpan(3, 0, 0));
                if (cierreSistema > new TimeSpan(23, 59, 59)) cierreSistema = new TimeSpan(23, 59, 59);

                // Si la hora actual está dentro de los límites, abrimos el sistema
                if (horaActual >= inicioEntrada && horaActual <= cierreSistema)
                {
                    sistemaAbierto = true;
                }
            }

            // Le pasamos al HTML el resultado real considerando FECHA y HORA
            ViewBag.JornadaHabilitada = sistemaAbierto;

            return View();
        }
    }
}