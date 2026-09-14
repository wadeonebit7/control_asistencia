using control_asistencia.Data;
using control_asistencia.Models;
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
                TempData["Error"] = "Por favor, ingrese su clave dinámica.";
                return RedirectToAction("LogoutAsistencia", "Usuarios");
            }

            var usuario = await _context.Usuarios
                .Include(u => u.Personal)
                    .ThenInclude(p => p.Rol)
                .FirstOrDefaultAsync(u => u.ClaveDinamica == claveDinamica && u.Estado == true);

            if (usuario == null)
            {
                TempData["Error"] = "Clave dinámica incorrecta o inactiva.";
                return RedirectToAction("LogoutAsistencia", "Usuarios");
            }

            var fechaHoy = DateTime.Now.Date;
            var horaActual = DateTime.Now.TimeOfDay;

            // 1. Obtenemos la configuración de asistencia activa desde la Base de Datos
            var jornadaHabilitada = await _context.habilitar_asistencia
                .FirstOrDefaultAsync(h => h.Fecha.Date == fechaHoy);

            if (jornadaHabilitada == null)
            {
                TempData["Error"] = "La jornada de hoy no ha sido habilitada por administración.";
                return RedirectToAction("LogoutAsistencia", "Usuarios");
            }

            var asistenciaExistente = await _context.Asistencia
                .FirstOrDefaultAsync(a => a.IdUsuario == usuario.Id && a.IdHabilitarAsistencia == jornadaHabilitada.Id);

            // =======================================================
            // 2. ENRUTAMIENTO 100% DINÁMICO BASADO EN LA TABLA
            // =======================================================
            TimeSpan horaEntradaDB = jornadaHabilitada.HoraEntrada;
            TimeSpan horaSalidaDB = jornadaHabilitada.HoraSalida;

            // Margen de apertura (2 horas antes de la entrada oficial) y cierre (3 horas después de la salida oficial)
            TimeSpan inicioSistema = horaEntradaDB.Subtract(new TimeSpan(2, 0, 0));
            if (inicioSistema < TimeSpan.Zero) inicioSistema = TimeSpan.Zero;

            TimeSpan cierreSistema = horaSalidaDB.Add(new TimeSpan(3, 0, 0));
            if (cierreSistema > new TimeSpan(23, 59, 59)) cierreSistema = new TimeSpan(23, 59, 59);

            // Punto medio exacto del turno configurado para dividir Entrada y Salida
            long ticksMitad = (horaEntradaDB.Ticks + horaSalidaDB.Ticks) / 2;
            TimeSpan cambioTurno = new TimeSpan(ticksMitad);

            // Validar si está fuera del horario operativo calculado
            if (horaActual < inicioSistema || horaActual > cierreSistema)
            {
                TempData["Error"] = $"Fuera de horario operativo. El turno hoy opera entre las {inicioSistema:hh\\:mm} y las {cierreSistema:hh\\:mm}.";
                return RedirectToAction("LogoutAsistencia", "Usuarios");
            }

            // 3. Evaluar si corresponde a Salida o Entrada según la mitad exacta del turno dinámico
            if (horaActual >= cambioTurno && horaActual <= cierreSistema)
            {
                // === MODO SALIDA DINÁMICO ===

                if (asistenciaExistente == null || asistenciaExistente.EstadoEntrada == "PENDIENTE")
                {
                    TempData["Error"] = "ACCESO DENEGADO: No tienes registro de entrada de hoy. Comunícate con Recursos Humanos.";
                    return RedirectToAction("LogoutAsistencia", "Usuarios");
                }

                if (asistenciaExistente.EstadoSalida != "PENDIENTE")
                {
                    TempData["Error"] = "Tu salida ya fue registrada anteriormente hoy. ¡Que tengas un buen descanso!";
                    return RedirectToAction("LogoutAsistencia", "Usuarios");
                }

                TempData["UsuarioLogueado"] = $"{usuario.Personal.Nombre} {usuario.Personal.Apellido}";
                TempData["IdUsuario"] = usuario.Id;
                return RedirectToAction("SalidaAsistencia");
            }
            else
            {
                // === MODO ENTRADA DINÁMICO ===

                if (asistenciaExistente != null && asistenciaExistente.EstadoEntrada != "PENDIENTE")
                {
                    TempData["Error"] = $"Ya registraste tu entrada. El cambio a modo salida será a partir de las {cambioTurno:hh\\:mm} hrs.";
                    return RedirectToAction("LogoutAsistencia", "Usuarios");
                }

                TempData["UsuarioLogueado"] = $"{usuario.Personal.Nombre} {usuario.Personal.Apellido}";
                TempData["IdUsuario"] = usuario.Id;
                return RedirectToAction("EntradaAsistencia");
            }
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




        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarEntrada(bool confirmacion)
        {





            // 1. Validar que el empleado haya marcado el checkbox
            if (!confirmacion)
            {
                TempData["Error"] = "Debe marcar la casilla para confirmar su entrada.";
                return RedirectToAction("LogoutAsistencia", "Usuarios");
            }

            // 2. Rescatar el ID del usuario desde TempData de forma correcta y segura
            int idUsuarioLogueado = 0;
            if (TempData["IdUsuario"] != null)
            {
                // Extraemos el valor y lo convertimos a entero
                idUsuarioLogueado = Convert.ToInt32(TempData["IdUsuario"]);

                // Mantener el dato vivo por si necesita marcar la salida más tarde
                TempData.Keep("IdUsuario");
            }
            else
            {
                // Si el TempData está vacío (la sesión se perdió o expiró)
                TempData["Error"] = "Su sesión ha expirado. Por favor, vuelva a ingresar su ticket.";
                // Cambia "Usuarios" por el nombre del controlador donde esté tu vista de login
                return RedirectToAction("LogoutAsistencia", "Usuarios");
            }



            // 3. Obtenemos la fecha actual y la hora exacta del marcaje
            var fechaHoy = DateTime.Now.Date;
            var horaActual = DateTime.Now.TimeOfDay;

            // 4. Buscamos si el Administrador habilitó la jornada de hoy en la BD
            var jornadaHabilitada = await _context.habilitar_asistencia
                .FirstOrDefaultAsync(h => h.Fecha.Date == fechaHoy);

            if (jornadaHabilitada == null)
            {
                TempData["Error"] = "La asistencia para hoy no ha sido habilitada por administración.";
                return RedirectToAction("LogoutAsistencia", "Usuarios");
            }

            // 5. Verificamos si el empleado ya registró su entrada hoy (evitar duplicados)
            var asistenciaExistente = await _context.Asistencia
                .FirstOrDefaultAsync(a => a.IdUsuario == idUsuarioLogueado && a.IdHabilitarAsistencia == jornadaHabilitada.Id);

            if (asistenciaExistente != null && asistenciaExistente.EstadoEntrada != "PENDIENTE")
            {
                TempData["Error"] = "Usted ya registró su entrada el día de hoy.";
                return RedirectToAction("LogoutAsistencia", "Usuarios"); 
            }



            // 6. Evaluamos si llegó atrasado comparando su hora con la hora límite (ej: 09:30:00)
            string estadoEntradaCalculado = "MARCADA";
            if (horaActual > jornadaHabilitada.HoraEntrada)
            {
                estadoEntradaCalculado = "ATRASADO";
            }

            // 7. Hacemos el INSERT o UPDATE en la tabla Asistencia
            if (asistenciaExistente == null)
            {
                // Si no hay registro previo, creamos la nueva fila
                var nuevaAsistencia = new Asistencia
                {
                    Estado = true,
                    IdUsuario = idUsuarioLogueado,
                    IdHabilitarAsistencia = jornadaHabilitada.Id,
                    HoraEntradaReal = horaActual,
                    HoraSalidaReal = TimeSpan.Zero,
                    EstadoEntrada = estadoEntradaCalculado,
                    EstadoSalida = "PENDIENTE",
                    CreateAt = DateTime.Now
                };

                _context.Asistencia.Add(nuevaAsistencia);
            }
            else
            {
                // Si ya existía una fila creada por el sistema, solo la actualizamos
                asistenciaExistente.HoraEntradaReal = horaActual;
                asistenciaExistente.EstadoEntrada = estadoEntradaCalculado;
                _context.Update(asistenciaExistente);
            }

            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "¡Entrada registrada correctamente!";

            // Lo redirigimos a su Dashboard para que vea su nueva marca en la tabla
            return RedirectToAction("LogoutAsistencia", "Usuarios");
        }






        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarSalida(bool confirmacion)
        {
            if (!confirmacion)
            {
                TempData["Error"] = "Debe marcar la casilla para confirmar su salida.";
                return RedirectToAction("LogoutAsistencia", "Usuarios");
            }

            int idUsuarioLogueado = 0;
            if (TempData["IdUsuario"] != null)
            {
                idUsuarioLogueado = Convert.ToInt32(TempData["IdUsuario"]);
                TempData.Keep("IdUsuario");
            }
            else
            {
                TempData["Error"] = "Su sesión ha expirado. Por favor, vuelva a ingresar su ticket.";
                return RedirectToAction("LogoutAsistencia", "Usuarios");
            }

            var fechaHoy = DateTime.Now.Date;
            var horaActual = DateTime.Now.TimeOfDay;

            var jornadaHabilitada = await _context.habilitar_asistencia
                .FirstOrDefaultAsync(h => h.Fecha.Date == fechaHoy);

            if (jornadaHabilitada == null)
            {
                TempData["Error"] = "La asistencia para hoy no ha sido habilitada por administración.";
                return RedirectToAction("LogoutAsistencia", "Usuarios");
            }

            var asistenciaExistente = await _context.Asistencia
                .FirstOrDefaultAsync(a => a.IdUsuario == idUsuarioLogueado && a.IdHabilitarAsistencia == jornadaHabilitada.Id);

            if (asistenciaExistente == null)
            {
                TempData["Error"] = "No tienes un registro de entrada previo para hoy. Por favor, comunícate con Recursos Humanos.";
                return RedirectToAction("LogoutAsistencia", "Usuarios");
            }

            if (asistenciaExistente.EstadoSalida != "PENDIENTE")
            {
                TempData["Error"] = "Tu salida ya fue registrada anteriormente hoy. Por seguridad, hemos cerrado esta sesión.";
                return RedirectToAction("LogoutAsistencia", "Usuarios");
            }

            string estadoSalidaCalculado = "MARCADA";

            // Tolerancia de 10 minutos para no castigar salidas justas
            TimeSpan tolerancia = new TimeSpan(0, 10, 0);
            TimeSpan horaMinimaAceptable = jornadaHabilitada.HoraSalida.Subtract(tolerancia);

            if (horaActual < horaMinimaAceptable)
            {
                estadoSalidaCalculado = "ANTICIPADO";
            }

            // ==========================================================
            // NUEVA LÓGICA: CÁLCULO DE HORAS TOTALES
            // ==========================================================
            TimeSpan tiempoTotalEnEmpresa = horaActual.Subtract(asistenciaExistente.HoraEntradaReal);

            asistenciaExistente.HoraSalidaReal = horaActual;
            asistenciaExistente.EstadoSalida = estadoSalidaCalculado;

            // Guardamos la diferencia en la base de datos (Si Recursos Humanos después define 
            // que a las 'HorasTrabajadas' se le debe restar 1 hora de colación, se haría el descuento aquí)
            asistenciaExistente.HorasReales = tiempoTotalEnEmpresa;
            asistenciaExistente.HorasTrabajadas = tiempoTotalEnEmpresa;

            _context.Update(asistenciaExistente);
            await _context.SaveChangesAsync();

            TempData.Remove("IdUsuario");
            TempData.Remove("UsuarioLogueado");

            TempData["Mensaje"] = "¡Salida registrada correctamente!";
            return RedirectToAction("LogoutAsistencia", "Usuarios");
        }




    }

}