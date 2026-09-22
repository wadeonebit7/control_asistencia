using control_asistencia.Data;
using control_asistencia.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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
            // 1. Calculamos los contadores para las tarjetas informativas
            ViewBag.TotalPendientes = await _context.Solicitudes.CountAsync(s => s.EstadoSolicitud == "PENDIENTE" && s.Estado == true);
            ViewBag.TotalAprobadas = await _context.Solicitudes.CountAsync(s => s.EstadoSolicitud == "APROBADO" && s.Estado == true);
            ViewBag.TotalRechazadas = await _context.Solicitudes.CountAsync(s => s.EstadoSolicitud == "RECHAZADO" && s.Estado == true);

            // 2. Traemos solo las solicitudes PENDIENTES para mostrarlas en la tabla principal
            // Hacemos un Include para poder acceder al Nombre y RUT del trabajador que la envió
            var solicitudesPendientes = await _context.Solicitudes
                .Include(s => s.Usuario)
                    .ThenInclude(u => u.Personal)
                .Where(s => s.EstadoSolicitud == "PENDIENTE" && s.Estado == true)
                .OrderBy(s => s.CreateAt) // Las más antiguas primero, para que se atiendan en orden
                .ToListAsync();

            return View(solicitudesPendientes);
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






        [HttpGet]
        public async Task<IActionResult> RevisarSolicitud(int id)
        {
            // Traemos la solicitud incluyendo al usuario y su personal
            var solicitud = await _context.Solicitudes
                .Include(s => s.Usuario)
                    .ThenInclude(u => u.Personal)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (solicitud == null)
            {
                return NotFound();
            }

            // Buscamos de forma directa los datos OCR específicos según la relación de la base de datos
            ViewBag.LicenciaMedica = await _context.LicenciaMedica
                .FirstOrDefaultAsync(l => l.IdSolicitudes == id);

            ViewBag.AjusteAsistencia = await _context.AjusteAsistencia
                .FirstOrDefaultAsync(a => a.IdSolicitudes == id);

            return View(solicitud);
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AceptarSolicitud(int id)
        {
            // 1. Buscar la solicitud principal
            var solicitud = await _context.Solicitudes
                .FirstOrDefaultAsync(s => s.Id == id);

            if (solicitud == null)
            {
                TempData["Error"] = "La solicitud no fue encontrada.";
                return RedirectToAction("Index", "RecursosHumanos"); // Cambia a tu vista correspondiente
            }

            // Validar que esté pendiente
            if (solicitud.EstadoSolicitud != "PENDIENTE")
            {
                TempData["Error"] = "Esta solicitud ya fue procesada anteriormente.";
                return RedirectToAction("Index", "RecursosHumanos");
            }

            // 2. Evaluar según el tipo de solicitud
            if (solicitud.TipoSolicitud == "LICENCIA_MEDICA")
            {
                // Buscar los datos en la tabla hija 'licencia_medica'
                var licencia = await _context.LicenciaMedica // Asegúrate de que el DbSet se llame así en tu DbContext
                    .FirstOrDefaultAsync(l => l.IdSolicitudes == solicitud.Id);

                if (licencia == null)
                {
                    TempData["Error"] = "No se encontraron los detalles de la licencia médica.";
                    return RedirectToAction("Index", "RecursosHumanos");
                }

                // VALIDACIÓN DE RANGO DE FECHAS: Desde FechaInicio hasta FechaTermino
                for (var dt = licencia.FechaInicio.Date; dt <= licencia.FechaTermino.Date; dt = dt.AddDays(1))
                {
                    var asistenciaDia = await _context.Asistencia
                        .FirstOrDefaultAsync(a => a.IdUsuario == solicitud.IdUsuario && a.CreateAt.Date == dt);

                    // Si en ALGÚN día del rango el usuario no marcó/tiene asistencia, se rechaza la aprobación
                    if (asistenciaDia == null)
                    {
                        TempData["Error"] = $"Error al aceptar: El usuario no registra asistencia el día {dt:dd-MM-yyyy} (dentro del rango de la licencia). No se puede aplicar.";
                        return RedirectToAction("Index", "RecursosHumanos");
                    }
                }

                // Si pasó la validación para todos los días, procedemos a ACTUALIZAR la asistencia de ese rango
                for (var dt = licencia.FechaInicio.Date; dt <= licencia.FechaTermino.Date; dt = dt.AddDays(1))
                {
                    var asistenciaDia = await _context.Asistencia
                        .FirstOrDefaultAsync(a => a.IdUsuario == solicitud.IdUsuario && a.CreateAt.Date == dt);

                    if (asistenciaDia != null)
                    {
                        asistenciaDia.EstadoEntrada = "LICENCIA";
                        asistenciaDia.EstadoSalida = "LICENCIA";
                        _context.Asistencia.Update(asistenciaDia);
                    }
                }
            }
            else if (solicitud.TipoSolicitud == "AJUSTE_ASISTENCIA")
            {
                // Buscar los datos en la tabla hija 'ajuste_asistencia'
                var ajuste = await _context.AjusteAsistencia // Asegúrate de que el DbSet se llame así en tu DbContext
                    .FirstOrDefaultAsync(a => a.IdSolicitudes == solicitud.Id);

                if (ajuste == null)
                {
                    TempData["Error"] = "No se encontraron los detalles del ajuste de asistencia.";
                    return RedirectToAction("Index", "RecursosHumanos");
                }

                // Buscar la asistencia de la fecha afectada específica
                var asistenciaAfectada = await _context.Asistencia
                    .FirstOrDefaultAsync(a => a.IdUsuario == solicitud.IdUsuario && a.CreateAt.Date == ajuste.FechaAfectada.Date);

                // VALIDACIÓN: Si no hay asistencia ese día, se bloquea
                if (asistenciaAfectada == null)
                {
                    TempData["Error"] = $"Error al aceptar: El usuario no registra ninguna asistencia en la fecha afectada ({ajuste.FechaAfectada:dd-MM-yyyy}).";
                    return RedirectToAction("Index", "RecursosHumanos");
                }

                // ACTUALIZAR la asistencia con las horas oficiales enviadas en el ajuste
                asistenciaAfectada.HoraEntradaReal = ajuste.HoraEntrada;
                asistenciaAfectada.HoraSalidaReal = ajuste.HoraSalida;
                asistenciaAfectada.EstadoEntrada = "AJUSTADO";
                asistenciaAfectada.EstadoSalida = "AJUSTADO";

                // Recalcular horas reales trabajadas
                var spanHoras = ajuste.HoraSalida - ajuste.HoraEntrada;
                if (spanHoras > TimeSpan.Zero)
                {
                    asistenciaAfectada.HorasReales = spanHoras;
                    asistenciaAfectada.HorasTrabajadas = spanHoras;
                }

                _context.Asistencia.Update(asistenciaAfectada);
            }

            // 3. Cambiar el estado de la solicitud principal a APROBADO
            solicitud.EstadoSolicitud = "APROBADO";
            _context.Solicitudes.Update(solicitud);

            // 4. (Opcional) Registrar en la tabla LOG si lo requieres para la auditoría
            var claimId = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claimId != null)
            {
                int idAdminLogueado = int.Parse(claimId.Value);
                var nuevoLog = new Log
                {
                    IdSolicitudes = solicitud.Id,
                    RevisadoPor = idAdminLogueado,
                    Respuesta = "Solicitud aprobada y aplicada al sistema de asistencia.",
                    CreateAt = DateTime.Now
                };
                _context.Add(nuevoLog);
            }

            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "¡Solicitud aceptada con éxito! Los registros de asistencia fueron actualizados automáticamente.";
            return RedirectToAction("Index", "RecursosHumanos");
        }





        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GestionarSolicitud(int id, string accion, string respuestaRrgg)
        {
            // 1. Identificar quién está revisando (RRHH)
            var claimId = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claimId == null) return RedirectToAction("Logout", "Auth");
            int idUsuarioRevisor = int.Parse(claimId.Value);

            // 2. Buscar la solicitud original incluyendo al usuario y personal asociado
            var solicitud = await _context.Solicitudes
                .Include(s => s.Usuario)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (solicitud == null)
            {
                TempData["Error"] = "La solicitud no fue encontrada.";
                return RedirectToAction(nameof(Index));
            }

            if (solicitud.EstadoSolicitud != "PENDIENTE")
            {
                TempData["Error"] = "Esta solicitud ya fue procesada anteriormente.";
                return RedirectToAction(nameof(Index));
            }

            // 3. Capturar estado para trazabilidad
            string estadoAntiguo = solicitud.EstadoSolicitud;
            string estadoNuevo = accion == "APROBAR" ? "APROBADO" : "RECHAZADO";

            // =========================================================================
            // LÓGICA DE APROBACIÓN Y CAMBIOS EN ASISTENCIA / USUARIO
            // =========================================================================
            if (accion == "APROBAR")
            {
                if (solicitud.TipoSolicitud == "LICENCIA_MEDICA")
                {
                    var licencia = await _context.LicenciaMedica
                        .FirstOrDefaultAsync(l => l.IdSolicitudes == solicitud.Id);

                    if (licencia == null)
                    {
                        TempData["Error"] = "No se encontraron los datos de la licencia médica.";
                        return RedirectToAction(nameof(Index));
                    }

                    // Validación del rango completo: desde FechaInicio hasta FechaTermino
                    for (var dt = licencia.FechaInicio.Date; dt <= licencia.FechaTermino.Date; dt = dt.AddDays(1))
                    {
                        var asistenciaDia = await _context.Asistencia
                            .FirstOrDefaultAsync(a => a.IdUsuario == solicitud.IdUsuario && a.CreateAt.Date == dt);

                        if (asistenciaDia == null)
                        {
                            TempData["Error"] = $"Error al aceptar: El usuario no registra asistencia el día {dt:dd-MM-yyyy} (dentro del rango de la licencia).";
                            return RedirectToAction(nameof(Index));
                        }
                    }

                    // Aplicar el cambio a LICENCIA en el rango de fechas de asistencia
                    for (var dt = licencia.FechaInicio.Date; dt <= licencia.FechaTermino.Date; dt = dt.AddDays(1))
                    {
                        var asistenciaDia = await _context.Asistencia
                            .FirstOrDefaultAsync(a => a.IdUsuario == solicitud.IdUsuario && a.CreateAt.Date == dt);

                        if (asistenciaDia != null)
                        {
                            asistenciaDia.EstadoEntrada = "LICENCIA";
                            asistenciaDia.EstadoSalida = "LICENCIA";
                            _context.Asistencia.Update(asistenciaDia);
                        }
                    }

                    // CAMBIAR EL STATUS DEL USUARIO A 'LICENCIA' SOLO SI LA LICENCIA CUBRE HOY O DÍAS FUTUROS
                    var hoy = DateTime.Now.Date;
                    if (solicitud.Usuario != null && licencia.FechaTermino.Date >= hoy)
                    {
                        solicitud.Usuario.Status = "LICENCIA";
                        _context.Usuarios.Update(solicitud.Usuario);
                    }
                }
                else if (solicitud.TipoSolicitud == "AJUSTE_ASISTENCIA")
                {
                    var ajuste = await _context.AjusteAsistencia
                        .FirstOrDefaultAsync(a => a.IdSolicitudes == solicitud.Id);

                    if (ajuste == null)
                    {
                        TempData["Error"] = "No se encontraron los datos del ajuste de asistencia.";
                        return RedirectToAction(nameof(Index));
                    }

                    var asistenciaAfectada = await _context.Asistencia
                        .FirstOrDefaultAsync(a => a.IdUsuario == solicitud.IdUsuario && a.CreateAt.Date == ajuste.FechaAfectada.Date);

                    if (asistenciaAfectada == null)
                    {
                        TempData["Error"] = $"Error al aceptar: El usuario no registra asistencia en la fecha afectada ({ajuste.FechaAfectada:dd-MM-yyyy}).";
                        return RedirectToAction(nameof(Index));
                    }

                    // Aplicar las horas reales del ajuste
                    asistenciaAfectada.HoraEntradaReal = ajuste.HoraEntrada;
                    asistenciaAfectada.HoraSalidaReal = ajuste.HoraSalida;
                    asistenciaAfectada.EstadoEntrada = "AJUSTADO";
                    asistenciaAfectada.EstadoSalida = "AJUSTADO";

                    var spanHoras = ajuste.HoraSalida - ajuste.HoraEntrada;
                    if (spanHoras > TimeSpan.Zero)
                    {
                        asistenciaAfectada.HorasReales = spanHoras;
                        asistenciaAfectada.HorasTrabajadas = spanHoras;
                    }

                    _context.Asistencia.Update(asistenciaAfectada);
                }
            }

            // 4. Actualizar solicitud principal
            solicitud.EstadoSolicitud = estadoNuevo;
            _context.Update(solicitud);

            // 5. Inserción en tabla LOG
            var nuevoLog = new Log
            {
                IdSolicitudes = solicitud.Id,
                RevisadoPor = idUsuarioRevisor,
                Respuesta = string.IsNullOrEmpty(respuestaRrgg) ? "Sin observaciones emitidas" : respuestaRrgg,
                CreateAt = DateTime.Now
            };
            _context.Log.Add(nuevoLog);

            // Guardamos para generar el ID autoincremental de 'nuevoLog'
            await _context.SaveChangesAsync();

            // 6. Inserción en tabla LOG_MODIFICACION
            var logModificacion = new LogModificacion
            {
                IdLog = nuevoLog.Id,
                Tabla = "solicitudes",
                Columna = "estado_solicitud",
                ValorAntiguo = estadoAntiguo,
                ValorNuevo = estadoNuevo
            };
            _context.LogModificacion.Add(logModificacion);

            // Guardado transaccional final
            await _context.SaveChangesAsync();

            TempData["Mensaje"] = $"La solicitud ha sido {estadoNuevo} exitosamente y la asistencia/auditoría se actualizó.";
            return RedirectToAction(nameof(Index));
        }





    }
}

   
