using control_asistencia.Data;
using control_asistencia.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace control_asistencia.Controllers
{
    [Authorize(Roles = "RRHH")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class RecursosHumanosController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RecursosHumanosController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            return View();
        }

        // 2. LISTAR PERSONAL
        public async Task<IActionResult> Personal()
        {
            // Traemos todo el personal que esté activo (estado = true)
            // Incluimos el Rol para poder mostrar si es ADMIN, EMPLEADO o RRHH
            var personal = await _context.Personal
                .Include(p => p.Rol)
                .Where(p => p.Estado == true)
                .ToListAsync();

            return View(personal);
        }

        // 3. VISTA PARA CREAR NUEVO PERSONAL
        [HttpGet]
        public async Task<IActionResult> CrearPersonal()
        {
            // Pasamos la lista de roles a la vista para llenar un <select>
            ViewBag.Roles = await _context.Rol.Where(r => r.Estado == true).ToListAsync();
            return View();
        }

        // 4. GUARDAR EL NUEVO PERSONAL EN BASE DE DATOS
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearPersonal(Personal nuevoPersonal)
        {
            if (ModelState.IsValid)
            {
                nuevoPersonal.Estado = true; // Por defecto activo
                _context.Personal.Add(nuevoPersonal);
                await _context.SaveChangesAsync();

                TempData["Mensaje"] = "Personal creado correctamente. El administrador ahora puede generarle un usuario.";
                return RedirectToAction(nameof(Personal));
            }

            // Si hay error, recargamos los roles y volvemos a mostrar el formulario
            ViewBag.Roles = await _context.Rol.Where(r => r.Estado == true).ToListAsync();
            return View(nuevoPersonal);
        }

        // 5. VISTA PARA EDITAR PERSONAL (GET)
        [HttpGet]
        public async Task<IActionResult> EditarPersonal(int id)
        {
            var empleado = await _context.Personal.FindAsync(id);

            // Si no existe o está desactivado (eliminado lógicamente), devolvemos un 404
            if (empleado == null || empleado.Estado == false)
            {
                return NotFound();
            }

            // Cargamos los roles activos para el <select>
            ViewBag.Roles = await _context.Rol.Where(r => r.Estado == true).ToListAsync();
            return View(empleado);
        }

        // 6. ACTUALIZAR PERSONAL EN BASE DE DATOS (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarPersonal(int id, Personal personalActualizado)
        {
            if (id != personalActualizado.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Buscamos al empleado original en la base de datos
                    var empleadoDb = await _context.Personal.FindAsync(id);
                    if (empleadoDb == null) return NotFound();

                    // Actualizamos solo los campos permitidos (no tocamos el Id ni el Estado aquí)
                    empleadoDb.Rut = personalActualizado.Rut;
                    empleadoDb.Nombre = personalActualizado.Nombre;
                    empleadoDb.Apellido = personalActualizado.Apellido;
                    empleadoDb.Correo = personalActualizado.Correo;
                    empleadoDb.Direccion = personalActualizado.Direccion;
                    empleadoDb.Celular = personalActualizado.Celular;
                    empleadoDb.IdRol = personalActualizado.IdRol; // Asegúrate de que tu modelo tenga esto como IdRol

                    _context.Update(empleadoDb);
                    await _context.SaveChangesAsync();

                    TempData["Mensaje"] = "Los datos del trabajador se actualizaron correctamente.";
                    return RedirectToAction(nameof(Personal));
                }
                catch (DbUpdateException)
                {
                    // En caso de que pongan un RUT o Correo que ya existe (por tus restricciones UNIQUE)
                    ModelState.AddModelError("", "Error al actualizar. Verifica que el RUT o Correo no estén siendo usados por otra persona.");
                }
            }

            // Si hay error en las validaciones, recargamos la vista
            ViewBag.Roles = await _context.Rol.Where(r => r.Estado == true).ToListAsync();
            return View(personalActualizado);
        }

        // 7. ELIMINACIÓN LÓGICA (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarPersonal(int id)
        {
            var empleado = await _context.Personal.FindAsync(id);
            if (empleado != null)
            {
                empleado.Estado = false; // Eliminación lógica en Personal

                // BUENA PRÁCTICA: Si desactivas al empleado, también debes desactivar su acceso al sistema
                var usuarioVinculado = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdPersonal == id);
                if (usuarioVinculado != null)
                {
                    usuarioVinculado.Estado = false;
                }

                await _context.SaveChangesAsync();
                TempData["Mensaje"] = "Personal desactivado correctamente.";
            }
            return RedirectToAction(nameof(Personal));
        }
    }
}
