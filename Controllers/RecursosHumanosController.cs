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


        [Authorize(Roles = "RRHH, ADMIN")]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // 1. Calcular KPIs de la parte superior
            ViewBag.TotalPendientes = await _context.Solicitudes.CountAsync(s => s.EstadoSolicitud == "PENDIENTE" && s.Estado == true);
            ViewBag.TotalAprobadas = await _context.Solicitudes.CountAsync(s => s.EstadoSolicitud == "APROBADO" && s.Estado == true);
            ViewBag.TotalRechazadas = await _context.Solicitudes.CountAsync(s => s.EstadoSolicitud == "RECHAZADO" && s.Estado == true);

            // 2. Traer Solicitudes Pendientes para la Bandeja de Entrada (Modelo principal)
            var solicitudesPendientes = await _context.Solicitudes
                .Include(s => s.Usuario)
                    .ThenInclude(u => u.Personal)
                .Where(s => s.EstadoSolicitud == "PENDIENTE" && s.Estado == true)
                .OrderBy(s => s.CreateAt)
                .ToListAsync();

            // 3. NUEVO: Traer Historial de Resoluciones para la segunda tabla
            var historial = await _context.Solicitudes
                .Include(s => s.Usuario)
                    .ThenInclude(u => u.Personal)
                .Include(s => s.Logs) // Fundamental para poder leer "Mi Respuesta" en la tabla
                .Where(s => s.EstadoSolicitud != "PENDIENTE" && s.Estado == true)
                .OrderByDescending(s => s.CreateAt)
                .ToListAsync();

            // 4. Enviar el historial a la vista
            ViewBag.Historial = historial;

            return View(solicitudesPendientes);
        }


        public async Task<IActionResult> Personal()
        {
            var personal = await _context.Personal
                .Include(p => p.Rol)
                .Where(p => p.Estado == true)
                .ToListAsync();

            return View(personal);
        }

        [HttpGet]
        public async Task<IActionResult> CrearPersonal()
        {
            ViewBag.Roles = await _context.Rol.Where(r => r.Estado == true).ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearPersonal(Personal nuevoPersonal)
        {
            if (ModelState.IsValid)
            {
                nuevoPersonal.Estado = true;
                _context.Personal.Add(nuevoPersonal);
                await _context.SaveChangesAsync();

                TempData["Mensaje"] = "Personal creado correctamente. El administrador ahora puede generarle un usuario.";
                return RedirectToAction(nameof(Personal));
            }

            ViewBag.Roles = await _context.Rol.Where(r => r.Estado == true).ToListAsync();
            return View(nuevoPersonal);
        }

        [HttpGet]
        public async Task<IActionResult> EditarPersonal(int id)
        {
            var empleado = await _context.Personal.FindAsync(id);

            if (empleado == null || empleado.Estado == false)
            {
                return NotFound();
            }

            ViewBag.Roles = await _context.Rol.Where(r => r.Estado == true).ToListAsync();
            return View(empleado);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditarPersonal(int id, Personal personalActualizado)
        {
            if (id != personalActualizado.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var empleadoDb = await _context.Personal.FindAsync(id);
                    if (empleadoDb == null) return NotFound();

                    empleadoDb.Rut = personalActualizado.Rut;
                    empleadoDb.Nombre = personalActualizado.Nombre;
                    empleadoDb.Apellido = personalActualizado.Apellido;
                    empleadoDb.Correo = personalActualizado.Correo;
                    empleadoDb.Direccion = personalActualizado.Direccion;
                    empleadoDb.Celular = personalActualizado.Celular;
                    empleadoDb.IdRol = personalActualizado.IdRol;

                    _context.Update(empleadoDb);
                    await _context.SaveChangesAsync();

                    TempData["Mensaje"] = "Los datos del trabajador se actualizaron correctamente.";
                    return RedirectToAction(nameof(Personal));
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError("", "Error al actualizar. Verifica que el RUT o Correo no estén siendo usados por otra persona.");
                }
            }

            ViewBag.Roles = await _context.Rol.Where(r => r.Estado == true).ToListAsync();
            return View(personalActualizado);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EliminarPersonal(int id)
        {
            var empleado = await _context.Personal.FindAsync(id);
            if (empleado != null)
            {
                empleado.Estado = false;

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
            var solicitud = await _context.Solicitudes
                .Include(s => s.Usuario)
                    .ThenInclude(u => u.Personal)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (solicitud == null)
            {
                return NotFound();
            }

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
            // Capturamos el ID del RRHH que está aceptando (para poder usarlo al crear el log o habilitar día)
            var claimId = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claimId == null) return RedirectToAction("Logout", "Auth");
            int idAdminLogueado = int.Parse(claimId.Value);

            var solicitud = await _context.Solicitudes
                .Include(s => s.Usuario)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (solicitud == null)
            {
                TempData["Error"] = "La solicitud no fue encontrada.";
                return RedirectToAction("Index", "RecursosHumanos");
            }

            if (solicitud.EstadoSolicitud != "PENDIENTE")
            {
                TempData["Error"] = "Esta solicitud ya fue procesada anteriormente.";
                return RedirectToAction("Index", "RecursosHumanos");
            }

            if (solicitud.TipoSolicitud == "LICENCIA_MEDICA")
            {
                var licencia = await _context.LicenciaMedica
                    .FirstOrDefaultAsync(l => l.IdSolicitudes == solicitud.Id);

                if (licencia == null)
                {
                    TempData["Error"] = "No se encontraron los detalles de la licencia médica.";
                    return RedirectToAction("Index", "RecursosHumanos");
                }

                // VALIDACIÓN Y ACTUALIZACIÓN/CREACIÓN DE RANGO DE FECHAS
                for (var dt = licencia.FechaInicio.Date; dt <= licencia.FechaTermino.Date; dt = dt.AddDays(1))
                {
                    var asistenciaDia = await _context.Asistencia
                        .FirstOrDefaultAsync(a => a.IdUsuario == solicitud.IdUsuario && a.CreateAt.Date == dt);

                    if (asistenciaDia != null)
                    {
                        // Si ya existe (ej. día actual o pasado), actualizamos a Licencia
                        asistenciaDia.EstadoEntrada = "LICENCIA";
                        asistenciaDia.EstadoSalida = "LICENCIA";
                        _context.Asistencia.Update(asistenciaDia);
                    }
                    else
                    {
                        // Si NO existe, verificamos si el día ya fue "habilitado" en la tabla habilitar_asistencia
                        var jornada = await _context.habilitar_asistencia
                            .FirstOrDefaultAsync(h => h.Fecha.Date == dt);

                        // Si el día futuro no ha sido abierto, lo abrimos a nombre del RRHH (idAdminLogueado)
                        if (jornada == null)
                        {
                            jornada = new habilitar_asistencia
                            {
                                AbiertoPor = idAdminLogueado,
                                Fecha = dt,
                                HoraEntrada = new TimeSpan(9, 30, 0), // Hora por defecto
                                HoraSalida = new TimeSpan(17, 30, 0), // Hora por defecto
                                CreatAt = DateTime.Now
                            };
                            _context.habilitar_asistencia.Add(jornada);
                            // Guardamos para que EF genere el nuevo ID de la jornada que necesitamos como FK
                            await _context.SaveChangesAsync();
                        }

                        // Ahora SÍ podemos crear la asistencia del día futuro vinculada al ID de la jornada
                        var nuevaAsistencia = new Asistencia
                        {
                            IdUsuario = solicitud.IdUsuario,
                            IdHabilitarAsistencia = jornada.Id, // <-- FK solucionada
                            CreateAt = dt,
                            HoraEntradaReal = TimeSpan.Zero,
                            HoraSalidaReal = TimeSpan.Zero,
                            EstadoEntrada = "LICENCIA",
                            EstadoSalida = "LICENCIA"
                        };
                        _context.Asistencia.Add(nuevaAsistencia);
                    }
                }

                // Cambiar status general a licencia si aplica hoy o a futuro
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
                    TempData["Error"] = "No se encontraron los detalles del ajuste de asistencia.";
                    return RedirectToAction("Index", "RecursosHumanos");
                }

                var asistenciaAfectada = await _context.Asistencia
                    .FirstOrDefaultAsync(a => a.IdUsuario == solicitud.IdUsuario && a.CreateAt.Date == ajuste.FechaAfectada.Date);

                if (asistenciaAfectada == null)
                {
                    TempData["Error"] = $"Error al aceptar: El usuario no registra ninguna asistencia en la fecha afectada ({ajuste.FechaAfectada:dd-MM-yyyy}).";
                    return RedirectToAction("Index", "RecursosHumanos");
                }

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

            solicitud.EstadoSolicitud = "APROBADO";
            _context.Solicitudes.Update(solicitud);

            var nuevoLog = new Log
            {
                IdSolicitudes = solicitud.Id,
                RevisadoPor = idAdminLogueado,
                Respuesta = "Solicitud aprobada y aplicada al sistema de asistencia.",
                CreateAt = DateTime.Now
            };
            _context.Add(nuevoLog);

            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "¡Solicitud aceptada con éxito! Los registros de asistencia fueron actualizados automáticamente.";
            return RedirectToAction("Index", "RecursosHumanos");
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GestionarSolicitud(int id, string accion, string respuestaRrgg)
        {
            var claimId = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claimId == null) return RedirectToAction("Logout", "Auth");
            int idUsuarioRevisor = int.Parse(claimId.Value);

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

            string estadoAntiguo = solicitud.EstadoSolicitud;
            string estadoNuevo = accion == "APROBAR" ? "APROBADO" : "RECHAZADO";

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
                        else
                        {
                            // Lógica de validación/creación FK para habilitar_asistencia también aquí
                            var jornada = await _context.habilitar_asistencia
                                .FirstOrDefaultAsync(h => h.Fecha.Date == dt);

                            if (jornada == null)
                            {
                                jornada = new habilitar_asistencia
                                {
                                    AbiertoPor = idUsuarioRevisor,
                                    Fecha = dt,
                                    HoraEntrada = new TimeSpan(9, 30, 0),
                                    HoraSalida = new TimeSpan(17, 30, 0),
                                    CreatAt = DateTime.Now
                                };
                                _context.habilitar_asistencia.Add(jornada);
                                await _context.SaveChangesAsync();
                            }

                            var nuevaAsistencia = new Asistencia
                            {
                                IdUsuario = solicitud.IdUsuario,
                                IdHabilitarAsistencia = jornada.Id, // <-- FK solucionada
                                CreateAt = dt,
                                HoraEntradaReal = TimeSpan.Zero,
                                HoraSalidaReal = TimeSpan.Zero,
                                EstadoEntrada = "LICENCIA",
                                EstadoSalida = "LICENCIA"
                            };
                            _context.Asistencia.Add(nuevaAsistencia);
                        }
                    }

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

            solicitud.EstadoSolicitud = estadoNuevo;
            _context.Update(solicitud);

            var nuevoLog = new Log
            {
                IdSolicitudes = solicitud.Id,
                RevisadoPor = idUsuarioRevisor,
                Respuesta = string.IsNullOrEmpty(respuestaRrgg) ? "Sin observaciones emitidas" : respuestaRrgg,
                CreateAt = DateTime.Now
            };
            _context.Log.Add(nuevoLog);

            await _context.SaveChangesAsync();

            var logModificacion = new LogModificacion
            {
                IdLog = nuevoLog.Id,
                Tabla = "solicitudes",
                Columna = "estado_solicitud",
                ValorAntiguo = estadoAntiguo,
                ValorNuevo = estadoNuevo
            };
            _context.LogModificacion.Add(logModificacion);

            await _context.SaveChangesAsync();

            TempData["Mensaje"] = $"La solicitud ha sido {estadoNuevo} exitosamente y la asistencia/auditoría se actualizó.";
            return RedirectToAction(nameof(Index));
        }
    }
}