using control_asistencia.Data;
using control_asistencia.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdfiumViewer;
using System;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tesseract;


namespace control_asistencia.Controllers
{
    [Authorize(Roles = "EMPLEADO")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class TrabajadorController : Controller
    {

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        // Inyección de dependencias para conectar con la base de datos y obtener rutas físicas
        public TrabajadorController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
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
                return RedirectToAction("LogoutAsistencia", "Auth");
            }

            var usuario = await _context.Usuarios
                .Include(u => u.Personal)
                    .ThenInclude(p => p.Rol)
                .FirstOrDefaultAsync(u => u.ClaveDinamica == claveDinamica && u.Estado == true);

            if (usuario == null)
            {
                TempData["Error"] = "Clave dinámica incorrecta o inactiva.";
                return RedirectToAction("LogoutAsistencia", "Auth");
            }

            var fechaHoy = DateTime.Now.Date;
            var horaActual = DateTime.Now.TimeOfDay;

            // 1. Obtenemos la configuración de asistencia activa desde la Base de Datos
            var jornadaHabilitada = await _context.habilitar_asistencia
                .FirstOrDefaultAsync(h => h.Fecha.Date == fechaHoy);

            if (jornadaHabilitada == null)
            {
                TempData["Error"] = "La jornada de hoy no ha sido habilitada por administración.";
                return RedirectToAction("LogoutAsistencia", "Auth");
            }

            var asistenciaExistente = await _context.Asistencia
                .FirstOrDefaultAsync(a => a.IdUsuario == usuario.Id && a.IdHabilitarAsistencia == jornadaHabilitada.Id);

            // =======================================================
            // 2. ENRUTAMIENTO CON HORA FIJA DE CAMBIO A LAS 14:00 (O 2 PM)
            // =======================================================
            TimeSpan horaEntradaDB = jornadaHabilitada.HoraEntrada;
            TimeSpan horaSalidaDB = jornadaHabilitada.HoraSalida;

            // Margen de apertura (2 horas antes de la entrada oficial) y cierre (3 horas después de la salida oficial)
            TimeSpan inicioSistema = horaEntradaDB.Subtract(new TimeSpan(2, 0, 0));
            if (inicioSistema < TimeSpan.Zero) inicioSistema = TimeSpan.Zero;

            TimeSpan cierreSistema = horaSalidaDB.Add(new TimeSpan(3, 0, 0));
            if (cierreSistema > new TimeSpan(23, 59, 59)) cierreSistema = new TimeSpan(23, 59, 59);

            // Hora fija establecida en las 14:00 hrs (2 PM) para activar el modo salida
            TimeSpan cambioTurnoFijo = new TimeSpan(14, 0, 0);

            // Validar si está fuera del horario operativo calculado
            if (horaActual < inicioSistema || horaActual > cierreSistema)
            {
                TempData["Error"] = $"Fuera de horario operativo. El turno hoy opera entre las {inicioSistema:hh\\:mm} y las {cierreSistema:hh\\:mm}.";
                return RedirectToAction("LogoutAsistencia", "Auth");
            }

            // 3. Evaluar si corresponde a Salida o Entrada usando la hora fija de las 14:00 hrs
            if (horaActual >= cambioTurnoFijo && horaActual <= cierreSistema)
            {
                // === MODO SALIDA (A partir de las 14:00 hrs) ===

                if (asistenciaExistente == null || asistenciaExistente.EstadoEntrada == "PENDIENTE")
                {
                    TempData["Error"] = "ACCESO DENEGADO: No tienes registro de entrada de hoy. Comunícate con Recursos Humanos.";
                    return RedirectToAction("LogoutAsistencia", "Auth");
                }

                if (asistenciaExistente.EstadoSalida != "PENDIENTE")
                {
                    TempData["Error"] = "Tu salida ya fue registrada anteriormente hoy. ¡Que tengas un buen descanso!";
                    return RedirectToAction("LogoutAsistencia", "Auth");
                }

                TempData["UsuarioLogueado"] = $"{usuario.Personal.Nombre} {usuario.Personal.Apellido}";
                TempData["IdUsuario"] = usuario.Id;
                return RedirectToAction("SalidaAsistencia");
            }
            else
            {
                // === MODO ENTRADA (Hasta antes de las 14:00 hrs) ===

                if (asistenciaExistente != null && asistenciaExistente.EstadoEntrada != "PENDIENTE")
                {
                    TempData["Error"] = "Ya registraste tu entrada. El cambio a modo salida será a partir de las 14:00 hrs.";
                    return RedirectToAction("LogoutAsistencia", "Auth");
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
                return RedirectToAction("LogoutAsistencia", "Auth");
            }

            // 2. Rescatar el ID del usuario desde TempData de forma correcta y segura
            int idUsuarioLogueado = 0;
            if (TempData["IdUsuario"] != null)
            {
                idUsuarioLogueado = Convert.ToInt32(TempData["IdUsuario"]);
                TempData.Keep("IdUsuario");
            }
            else
            {
                TempData["Error"] = "Su sesión ha expirado. Por favor, vuelva a ingresar su ticket.";
                return RedirectToAction("LogoutAsistencia", "Auth");
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
                return RedirectToAction("LogoutAsistencia", "Auth");
            }

            // 5. Verificamos si el empleado ya registró su entrada hoy (evitar duplicados)
            var asistenciaExistente = await _context.Asistencia
                .FirstOrDefaultAsync(a => a.IdUsuario == idUsuarioLogueado && a.IdHabilitarAsistencia == jornadaHabilitada.Id);

            if (asistenciaExistente != null && asistenciaExistente.EstadoEntrada != "PENDIENTE")
            {
                TempData["Error"] = "Usted ya registró su entrada el día de hoy.";
                return RedirectToAction("LogoutAsistencia", "Auth");
            }

            // ==========================================================
            // 6. EVALUACIÓN DE ENTRADA CON 15 MINUTOS DE TOLERANCIA
            // ==========================================================
            string estadoEntradaCalculado = "MARCADA";

            // Sumamos 15 minutos a la hora de entrada oficial configurada por administración
            TimeSpan toleranciaAtraso = new TimeSpan(0, 15, 0);
            TimeSpan horaMaximaSinAtraso = jornadaHabilitada.HoraEntrada.Add(toleranciaAtraso);

            // Si llega después de los 15 minutos de gracia, se marca como ATRASADO
            if (horaActual > horaMaximaSinAtraso)
            {
                estadoEntradaCalculado = "ATRASADO";
            }

            // 7. Hacemos el INSERT o UPDATE en la tabla Asistencia
            if (asistenciaExistente == null)
            {
                var nuevaAsistencia = new Asistencia
                {
                    Estado = true,
                    IdUsuario = idUsuarioLogueado,
                    IdHabilitarAsistencia = jornadaHabilitada.Id,
                    HoraEntradaReal = horaActual,
                    HoraSalidaReal = TimeSpan.Zero,
                    EstadoEntrada = estadoEntradaCalculado,
                    EstadoSalida = "PENDIENTE",
                    HorasReales = null,
                    HorasTrabajadas = null,
                    CreateAt = DateTime.Now
                };

                _context.Asistencia.Add(nuevaAsistencia);
            }
            else
            {
                asistenciaExistente.HoraEntradaReal = horaActual;
                asistenciaExistente.EstadoEntrada = estadoEntradaCalculado;
                _context.Update(asistenciaExistente);
            }

            await _context.SaveChangesAsync();

            TempData["Mensaje"] = "¡Entrada registrada correctamente!";
            return RedirectToAction("LogoutAsistencia", "Auth");
        }





        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarSalida(bool confirmacion)
        {
            if (!confirmacion)
            {
                TempData["Error"] = "Debe marcar la casilla para confirmar su salida.";
                return RedirectToAction("LogoutAsistencia", "Auth");
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
                return RedirectToAction("LogoutAsistencia", "Auth");
            }

            var fechaHoy = DateTime.Now.Date;
            var horaActual = DateTime.Now.TimeOfDay;

            var jornadaHabilitada = await _context.habilitar_asistencia
                .FirstOrDefaultAsync(h => h.Fecha.Date == fechaHoy);

            if (jornadaHabilitada == null)
            {
                TempData["Error"] = "La asistencia para hoy no ha sido habilitada por administración.";
                return RedirectToAction("LogoutAsistencia", "Auth");
            }

            var asistenciaExistente = await _context.Asistencia
                .FirstOrDefaultAsync(a => a.IdUsuario == idUsuarioLogueado && a.IdHabilitarAsistencia == jornadaHabilitada.Id);

            if (asistenciaExistente == null)
            {
                TempData["Error"] = "No tienes un registro de entrada previo para hoy. Por favor, comunícate con Recursos Humanos.";
                return RedirectToAction("LogoutAsistencia", "Auth");
            }

            if (asistenciaExistente.EstadoSalida != "PENDIENTE")
            {
                TempData["Error"] = "Tu salida ya fue registrada anteriormente hoy. Por seguridad, hemos cerrado esta sesión.";
                return RedirectToAction("LogoutAsistencia", "Auth");
            }

            string estadoSalidaCalculado = "MARCADA";

            // Tolerancia de 10 minutos para no castigar salidas justas
            TimeSpan tolerancia = new TimeSpan(0, 15, 0);
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
            return RedirectToAction("LogoutAsistencia", "Auth");
        }






        [HttpPost]
        public async Task<IActionResult> ExtraerDatosLicencia(IFormFile documento, [FromForm] string tipoSolicitud)
        {
            if (documento == null || documento.Length == 0) return BadRequest("Archivo vacío.");

            var tempPath = Path.GetTempFileName();
            using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await documento.CopyToAsync(stream);
            }

            string textoExtraido = "";
            string tessdataPath = Path.Combine(_env.ContentRootPath, "tessdata");

            // ==========================================================
            // LECTURA DE PDF Y JPG
            // ==========================================================
            try
            {
                string extension = Path.GetExtension(documento.FileName).ToLower();

                using (var engine = new TesseractEngine(tessdataPath, "spa", EngineMode.Default))
                {
                    engine.SetVariable("tessedit_pageseg_mode", "3");

                    if (extension == ".pdf")
                    {
                        using (var pdfDocument = PdfiumViewer.PdfDocument.Load(tempPath))
                        {
                            for (int i = 0; i < pdfDocument.PageCount; i++)
                            {
                                using (var image = pdfDocument.Render(i, 300, 300, PdfiumViewer.PdfRenderFlags.CorrectFromDpi))
                                {
                                    string pageTempPath = Path.GetTempFileName() + ".png";
                                    image.Save(pageTempPath, System.Drawing.Imaging.ImageFormat.Png);

                                    using (var img = Pix.LoadFromFile(pageTempPath))
                                    {
                                        using (var page = engine.Process(img))
                                        {
                                            textoExtraido += "\n" + page.GetText();
                                        }
                                    }
                                    System.IO.File.Delete(pageTempPath);
                                }
                            }
                        }
                    }
                    else
                    {
                        using (var img = Pix.LoadFromFile(tempPath))
                        {
                            using (var page = engine.Process(img))
                            {
                                textoExtraido = page.GetText();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath);
                System.Diagnostics.Debug.WriteLine("EXCEPCIÓN OCR: " + ex.ToString());
                return StatusCode(500, $"Error OCR: {ex.Message}");
            }

            if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath);

            // =========================================================================
            // EXTRACCIÓN DEPENDIENDO DE LO QUE SELECCIONÓ EL USUARIO
            // =========================================================================

            if (tipoSolicitud == "LICENCIA_MEDICA")
            {
                var folioMatch = Regex.Match(textoExtraido, @"Folio\s*Licencia.*?([0-9]{5,}[-.]?[0-9Kk])", RegexOptions.IgnoreCase);
                string folioStr = folioMatch.Success ? folioMatch.Groups[1].Value.Replace(".", "-").Trim().ToUpper() : "Revisar manual";

                var profesionalMatch = Regex.Match(textoExtraido, @"Profesional.*?([A-Za-zÑñÁÉÍÓÚáéíóú\s\.]+?)(?=\r|\n|Entidad|$)", RegexOptions.IgnoreCase);
                string profStr = profesionalMatch.Success ? profesionalMatch.Groups[1].Value.Trim() : "Revisar manual";

                var diasMatch = Regex.Match(textoExtraido, @"N[*°ºo\W]*\s*de\s*d[ií]as.*?(\d{1,3})", RegexOptions.IgnoreCase);
                string diasStr = diasMatch.Success ? diasMatch.Groups[1].Value.Trim() : "Revisar manual";

                var fechaOtorgamientoMatch = Regex.Match(textoExtraido, @"Fecha de Emisi[óo]n.*?(\d{2})\D*(\d{2})\D*(\d{4})", RegexOptions.IgnoreCase);
                string fechaOtorStr = "Revisar manual";
                if (fechaOtorgamientoMatch.Success) fechaOtorStr = $"{fechaOtorgamientoMatch.Groups[1].Value}-{fechaOtorgamientoMatch.Groups[2].Value}-{fechaOtorgamientoMatch.Groups[3].Value}";

                var fechaInicioMatch = Regex.Match(textoExtraido, @"Inicio de Reposo.*?(\d{2})\D*(\d{2})\D*(\d{4})", RegexOptions.IgnoreCase);
                string fechaInicioStr = "Revisar manual";
                if (fechaInicioMatch.Success) fechaInicioStr = $"{fechaInicioMatch.Groups[1].Value}-{fechaInicioMatch.Groups[2].Value}-{fechaInicioMatch.Groups[3].Value}";

                var tipoMatch = Regex.Match(textoExtraido, @"Tipo de licencia[\s:]*([A-Za-zÑñÁÉÍÓÚáéíóú\s]+)", RegexOptions.IgnoreCase);
                string tipificacionStr = "TIPO_1";
                if (tipoMatch.Success)
                {
                    string leido = tipoMatch.Groups[1].Value.ToUpper();
                    if (leido.Contains("ENFERMEDAD")) tipificacionStr = "TIPO_1";
                    else if (leido.Contains("PREVENTIVA")) tipificacionStr = "TIPO_2";
                    else if (leido.Contains("MATERNAL")) tipificacionStr = "TIPO_3";
                    else if (leido.Contains("HIJO")) tipificacionStr = "TIPO_4";
                    else if (leido.Contains("TRABAJO") || leido.Contains("ACCIDENTE")) tipificacionStr = "TIPO_5";
                    else if (leido.Contains("TRAYECTO")) tipificacionStr = "TIPO_6";
                    else if (leido.Contains("PROFESIONAL")) tipificacionStr = "TIPO_7";
                }

                return Json(new { folio = folioStr, diasReposo = diasStr, profesional = profStr, fechaInicio = fechaInicioStr, fechaOtorgamiento = fechaOtorStr, tipificacion = tipificacionStr });
            }
            else if (tipoSolicitud == "AJUSTE_ASISTENCIA")
            {
                var fechaAfectadaMatch = Regex.Match(textoExtraido, @"incidente a ajustar[\s:|]+(\d{2})\D*(\d{2})\D*(\d{4})", RegexOptions.IgnoreCase);
                var horaEntradaMatch = Regex.Match(textoExtraido, @"entrada real trabajada[\s:|]+(\d{1,2}:\d{2})", RegexOptions.IgnoreCase);
                var horaSalidaMatch = Regex.Match(textoExtraido, @"salida real trabajada[\s:|]+(\d{1,2}:\d{2})", RegexOptions.IgnoreCase);
                var emitidoPorMatch = Regex.Match(textoExtraido, @"NOMBRE DEL SUPERVISOR/JEFE[\s:|]+([A-Za-zÑñÁÉÍÓÚáéíóú\s]+?)(?=\r|\n|Cargo|$)", RegexOptions.IgnoreCase);

                string tipoIncidenciaStr = "NO MARCADA";

                // NUEVO: Regex Ultratolerante. 
                // Busca una X seguida de 0 a 4 caracteres "basura" (como corchetes rotos o espacios) y luego la palabra clave.
                bool marcoEntrada = Regex.IsMatch(textoExtraido, @"[xX][^a-zA-Z0-9]{0,4}No marc[óo]\s+entrada", RegexOptions.IgnoreCase);
                bool marcoSalida = Regex.IsMatch(textoExtraido, @"[xX][^a-zA-Z0-9]{0,4}No marc[óo]\s+salida", RegexOptions.IgnoreCase);
                bool marcoIncompleta = Regex.IsMatch(textoExtraido, @"[xX][^a-zA-Z0-9]{0,4}Marcaci[óo]n\s+incompleta", RegexOptions.IgnoreCase);
                bool marcoOtro = Regex.IsMatch(textoExtraido, @"[xX][^a-zA-Z0-9]{0,4}Otro", RegexOptions.IgnoreCase);

                if (marcoEntrada)
                {
                    tipoIncidenciaStr = "ENTRADA";
                }
                else if (marcoSalida)
                {
                    tipoIncidenciaStr = "SALIDA";
                }
                else if (marcoIncompleta)
                {
                    tipoIncidenciaStr = "NO MARCADA"; // O lo que prefieras mapear aquí
                }
                else if (marcoOtro)
                {
                    tipoIncidenciaStr = "OTRO";
                }

                string fechaAfectadaStr = fechaAfectadaMatch.Success ? $"{fechaAfectadaMatch.Groups[1].Value}-{fechaAfectadaMatch.Groups[2].Value}-{fechaAfectadaMatch.Groups[3].Value}" : "Revisar manual";
                string horaEntradaStr = horaEntradaMatch.Success ? horaEntradaMatch.Groups[1].Value : "Revisar manual";
                string horaSalidaStr = horaSalidaMatch.Success ? horaSalidaMatch.Groups[1].Value : "Revisar manual";
                string emitidoPorStr = emitidoPorMatch.Success ? emitidoPorMatch.Groups[1].Value.Trim() : "Revisar manual";

                return Json(new
                {
                    fechaAfectada = fechaAfectadaStr,
                    horaEntrada = horaEntradaStr,
                    horaSalida = horaSalidaStr,
                    emitidoPor = emitidoPorStr,
                    tipoIncidencia = tipoIncidenciaStr
                });
            }

            return BadRequest("Tipo de solicitud no válido.");
        }





        [HttpGet]
        public async Task<IActionResult> ObtenerClaveDinamicaActual()
        {
            int idUsuarioLogueado = 0;
            if (TempData["IdUsuario"] != null)
            {
                idUsuarioLogueado = Convert.ToInt32(TempData["IdUsuario"]);
                TempData.Keep("IdUsuario");
            }
            else
            {
                idUsuarioLogueado = 1; // Fallback para pruebas
            }

            var usuario = await _context.Usuarios
                .Include(u => u.Personal)
                .FirstOrDefaultAsync(u => u.Id == idUsuarioLogueado);

            if (usuario == null || string.IsNullOrEmpty(usuario.ClaveDinamica))
            {
                return Json(new { claveDinamica = "CARGANDO", segundosRestantes = 60 });
            }

            // Calcular cuántos segundos faltan exactamente para que termine el minuto actual en el servidor
            var now = DateTime.Now;
            int segundosRestantes = 60 - now.Second;

            return Json(new
            {
                claveDinamica = usuario.ClaveDinamica,
                segundosRestantes = segundosRestantes
            });
        }





    }

}