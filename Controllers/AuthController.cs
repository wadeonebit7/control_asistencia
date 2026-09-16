using control_asistencia.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;

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
            if (string.IsNullOrEmpty(correo) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Por favor, complete todos los campos.";
                return View("Index");
            }

            var usuario = await _context.Usuarios
                .Include(u => u.Personal)
                    .ThenInclude(p => p.Rol)
                .FirstOrDefaultAsync(u => u.Personal.Correo == correo && u.Password == password && u.Estado == true);

            if (usuario != null)
            {
                string nombreCompleto = $"{usuario.Personal.Nombre} {usuario.Personal.Apellido}";
                string nombreRol = usuario.Personal.Rol.Nombre;

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                    new Claim(ClaimTypes.Name, nombreCompleto),
                    new Claim(ClaimTypes.Role, nombreRol),
                    new Claim("IdPersonal", usuario.IdPersonal.ToString())
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                if (nombreRol == "ADMIN") return RedirectToAction("Index", "Administrador");
                else if (nombreRol == "EMPLEADO") return RedirectToAction("Index", "Trabajador");
                else if (nombreRol == "RRHH") return RedirectToAction("Index", "RecursosHumanos");
                else return RedirectToAction("Index", "Auth");
            }

            ViewBag.Error = "Correo o contraseña incorrectos.";
            return View("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            // Esto destruye la cookie de autenticación del navegador por completo
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData.Clear();
            return RedirectToAction("Index", "Auth");
        }




        [HttpGet]
        public async Task<IActionResult> LogoutAsistencia()
        {
            // Limpiamos sesión previa por seguridad en la terminal
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Rescatamos los mensajes de TempData por si vienen de un intento de marcaje
            if (TempData["Error"] != null)
            {
                ViewBag.Error = TempData["Error"];
            }
            if (TempData["Mensaje"] != null)
            {
                ViewBag.MensajeExito = TempData["Mensaje"];
            }

            var fechaHoy = DateTime.Now.Date;
            var horaActual = DateTime.Now.TimeOfDay;

            var jornada = await _context.habilitar_asistencia
                .FirstOrDefaultAsync(h => h.Fecha.Date == fechaHoy);

            bool sistemaAbierto = false;

            if (jornada != null)
            {
                TimeSpan horaEntradaBD = jornada.HoraEntrada;
                TimeSpan horaSalidaBD = jornada.HoraSalida;

                TimeSpan inicioEntrada = horaEntradaBD.Subtract(new TimeSpan(2, 0, 0));
                if (inicioEntrada < TimeSpan.Zero) inicioEntrada = TimeSpan.Zero;

                TimeSpan cierreSistema = horaSalidaBD.Add(new TimeSpan(3, 0, 0));
                if (cierreSistema > new TimeSpan(23, 59, 59)) cierreSistema = new TimeSpan(23, 59, 59);

                if (horaActual >= inicioEntrada && horaActual <= cierreSistema)
                {
                    sistemaAbierto = true;
                }
            }

            ViewBag.JornadaHabilitada = sistemaAbierto;

            return View();
        }


        [HttpGet]
        public async Task<IActionResult> AdminVerificacion()
        {
            // Limpiamos la sesión al entrar para forzar la introducción de credenciales
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            TempData.Clear();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerificarAdminLogin(string correo, string password)
        {
            if (string.IsNullOrEmpty(correo) || string.IsNullOrEmpty(password))
            {
                TempData["Error"] = "Por favor, complete todos los campos de acceso.";
                return RedirectToAction("AdminVerificacion");
            }

            var usuario = await _context.Usuarios
                .Include(u => u.Personal)
                    .ThenInclude(p => p.Rol)
                .FirstOrDefaultAsync(u => u.Personal.Correo == correo && u.Password == password && u.Estado == true);

            // Verificamos estrictamente que exista y que su rol sea exclusivamente ADMIN
            if (usuario != null && usuario.Personal.Rol.Nombre == "ADMIN")
            {
                string nombreCompleto = $"{usuario.Personal.Nombre} {usuario.Personal.Apellido}";

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                    new Claim(ClaimTypes.Name, nombreCompleto),
                    new Claim(ClaimTypes.Role, "ADMIN"),
                    new Claim("IdPersonal", usuario.IdPersonal.ToString())
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                return RedirectToAction("Index", "Administrador");
            }

            // Si falla o no es admin, lo devolvemos a la pantalla de verificación aislada
            TempData["Error"] = "Credenciales incorrectas o permisos de administrador insuficientes.";
            return RedirectToAction("AdminVerificacion");
        }
    }
}