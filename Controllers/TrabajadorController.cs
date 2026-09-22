using control_asistencia.Data;
using control_asistencia.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdfiumViewer;
using System;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tesseract;

namespace control_asistencia.Controllers
{
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public class TrabajadorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public TrabajadorController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [Authorize(Roles = "EMPLEADO")]
        [HttpGet]
        public async Task<IActionResult> Index(int? mes)
        {
            int idUsuarioLogueado = 1;
            int mesConsulta = mes ?? DateTime.Now.Month;

            var registrosAsistencia = await _context.Asistencia
                .Where(a => a.IdUsuario == idUsuarioLogueado && a.CreateAt.Month == mesConsulta)
                .OrderByDescending(a => a.CreateAt)
                .ToListAsync();

            return View(registrosAsistencia);
        }

        // Quitamos el ValidateAntiForgeryToken aquí para evitar el Error 400 en la terminal pública
        [AllowAnonymous]
        [HttpPost]
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

            TempData["IdUsuario"] = usuario.Id;
            TempData["UsuarioLogueado"] = $"{usuario.Personal.Nombre} {usuario.Personal.Apellido}";
            TempData.Keep("IdUsuario");
            TempData.Keep("UsuarioLogueado");

            var fechaHoy = DateTime.Now.Date;
            var horaActual = DateTime.Now.TimeOfDay;

            var jornadaHabilitada = await _context.habilitar_asistencia
                .FirstOrDefaultAsync(h => h.Fecha.Date == fechaHoy);

            if (jornadaHabilitada == null)
            {
                TempData["Error"] = "La jornada de hoy no ha sido habilitada por administración.";
                return RedirectToAction("LogoutAsistencia", "Auth");
            }

            TimeSpan cambioTurnoFijo = new TimeSpan(14, 0, 0);

            if (horaActual >= cambioTurnoFijo)
            {
                return RedirectToAction("SalidaAsistencia");
            }
            else
            {
                return RedirectToAction("EntradaAsistencia");
            }
        }





        [AllowAnonymous]
        [HttpGet]
        public IActionResult EntradaAsistencia()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult SalidaAsistencia()
        {
            return View();
        }





        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarEntrada(bool confirmacion)
        {
            if (!confirmacion)
            {
                TempData["Error"] = "Debe marcar la casilla para confirmar su entrada.";
                TempData.Keep("Error");
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
                TempData.Keep("Error");
                return RedirectToAction("LogoutAsistencia", "Auth");
            }

            var fechaHoy = DateTime.Now.Date;
            var horaActual = DateTime.Now.TimeOfDay;

            var jornadaHabilitada = await _context.habilitar_asistencia
                .FirstOrDefaultAsync(h => h.Fecha.Date == fechaHoy);

            if (jornadaHabilitada == null)
            {
                TempData["Error"] = "La asistencia para hoy no ha sido habilitada por administración.";
                TempData.Keep("Error");
                return RedirectToAction("LogoutAsistencia", "Auth");
            }

            var asistenciaExistente = await _context.Asistencia
                .FirstOrDefaultAsync(a => a.IdUsuario == idUsuarioLogueado && a.IdHabilitarAsistencia == jornadaHabilitada.Id);

            if (asistenciaExistente != null && asistenciaExistente.EstadoEntrada != "PENDIENTE")
            {
                TempData["Error"] = "Usted ya registró su entrada el día de hoy.";
                TempData.Keep("Error");
                return RedirectToAction("LogoutAsistencia", "Auth");
            }

            string estadoEntradaCalculado = "MARCADA";
            TimeSpan toleranciaAtraso = new TimeSpan(0, 15, 0);
            TimeSpan horaMaximaSinAtraso = jornadaHabilitada.HoraEntrada.Add(toleranciaAtraso);

            if (horaActual > horaMaximaSinAtraso)
            {
                estadoEntradaCalculado = "ATRASADO";
            }

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
            TempData.Keep("Mensaje");
            return RedirectToAction("LogoutAsistencia", "Auth");
        }





        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GuardarSalida(bool confirmacion)
        {
            if (!confirmacion)
            {
                TempData["Error"] = "Debe marcar la casilla para confirmar su salida.";
                TempData.Keep("Error");
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
                TempData.Keep("Error");
                return RedirectToAction("LogoutAsistencia", "Auth");
            }

            var fechaHoy = DateTime.Now.Date;
            var horaActual = DateTime.Now.TimeOfDay;

            var jornadaHabilitada = await _context.habilitar_asistencia
                .FirstOrDefaultAsync(h => h.Fecha.Date == fechaHoy);

            if (jornadaHabilitada == null)
            {
                TempData["Error"] = "La asistencia para hoy no ha sido habilitada por administración.";
                TempData.Keep("Error");
                return RedirectToAction("LogoutAsistencia", "Auth");
            }

            var asistenciaExistente = await _context.Asistencia
                .FirstOrDefaultAsync(a => a.IdUsuario == idUsuarioLogueado && a.IdHabilitarAsistencia == jornadaHabilitada.Id);

            if (asistenciaExistente == null)
            {
                TempData["Error"] = "No tienes un registro de entrada previo para hoy. Por favor, comunícate con Recursos Humanos.";
                TempData.Keep("Error");
                return RedirectToAction("LogoutAsistencia", "Auth");
            }

            if (asistenciaExistente.EstadoSalida != "PENDIENTE")
            {
                TempData["Error"] = "Tu salida ya fue registrada anteriormente hoy. Por seguridad, hemos cerrado esta sesión.";
                TempData.Keep("Error");
                return RedirectToAction("LogoutAsistencia", "Auth");
            }

            // AQUÍ PUEDES VALIDAR TU RESTRICCIÓN DE HORARIO SI LO DESEAS:
            // Por ejemplo, si quieres bloquear si intenta salir antes de tiempo o fuera de rango:
            // TimeSpan tolerancia = new TimeSpan(0, 15, 0);
            // if (horaActual < jornadaHabilitada.HoraSalida.Subtract(tolerancia)) { ... }

            string estadoSalidaCalculado = "MARCADA";
            TimeSpan toleranciaSalida = new TimeSpan(0, 15, 0);
            TimeSpan horaMinimaAceptable = jornadaHabilitada.HoraSalida.Subtract(toleranciaSalida);

            if (horaActual < horaMinimaAceptable)
            {
                estadoSalidaCalculado = "ANTICIPADO";
            }

            TimeSpan tiempoTotalEnEmpresa = horaActual.Subtract(asistenciaExistente.HoraEntradaReal);

            asistenciaExistente.HoraSalidaReal = horaActual;
            asistenciaExistente.EstadoSalida = estadoSalidaCalculado;
            asistenciaExistente.HorasReales = tiempoTotalEnEmpresa;
            asistenciaExistente.HorasTrabajadas = tiempoTotalEnEmpresa;

            _context.Update(asistenciaExistente);
            await _context.SaveChangesAsync();

            TempData.Remove("IdUsuario");
            TempData.Remove("UsuarioLogueado");

            TempData["Mensaje"] = "¡Salida registrada correctamente!";
            TempData.Keep("Mensaje");
            return RedirectToAction("LogoutAsistencia", "Auth");
        }







        [HttpPost]
        public async Task<IActionResult> ExtraerDatosLicencia(IFormFile documento, [FromForm] string tipoSolicitud)
        {
            if (documento == null || documento.Length == 0) return BadRequest("Archivo vacío.");

            string extension = Path.GetExtension(documento.FileName).ToLower();
            string tempPath = Path.GetTempFileName();
            string tempPathWithExt = tempPath + extension;
            string outputPrefix = Path.Combine(Path.GetTempPath(), "ocr_page_" + Guid.NewGuid());

            try
            {
                using (var stream = new FileStream(tempPathWithExt, FileMode.Create))
                {
                    await documento.CopyToAsync(stream);
                }

                string textoExtraido = "";

                if (extension == ".pdf")
                {
                    var psiPdf = new ProcessStartInfo
                    {
                        FileName = "pdftoppm",
                        Arguments = $"-png -r 300 \"{tempPathWithExt}\" \"{outputPrefix}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (var processPdf = Process.Start(psiPdf))
                    {
                        processPdf.WaitForExit();
                    }

                    string tempDir = Path.GetDirectoryName(outputPrefix);
                    string filePrefix = Path.GetFileName(outputPrefix);
                    var generatedImages = Directory.GetFiles(tempDir, filePrefix + "*.png").OrderBy(f => f).ToArray();

                    foreach (var imgPath in generatedImages)
                    {
                        string outputBase = Path.GetTempFileName();
                        var psiTess = new ProcessStartInfo
                        {
                            FileName = "tesseract",
                            Arguments = $"\"{imgPath}\" \"{outputBase}\" -l spa",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        using (var processTess = Process.Start(psiTess))
                        {
                            processTess.WaitForExit();
                        }

                        string txtFile = outputBase + ".txt";
                        if (System.IO.File.Exists(txtFile))
                        {
                            textoExtraido += "\n" + await System.IO.File.ReadAllTextAsync(txtFile);
                            System.IO.File.Delete(txtFile);
                        }
                        if (System.IO.File.Exists(outputBase)) System.IO.File.Delete(outputBase);
                        if (System.IO.File.Exists(imgPath)) System.IO.File.Delete(imgPath);
                    }
                }
                else
                {
                    string outputBase = Path.GetTempFileName();
                    var psi = new ProcessStartInfo
                    {
                        FileName = "tesseract",
                        Arguments = $"\"{tempPathWithExt}\" \"{outputBase}\" -l spa",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (var process = Process.Start(psi))
                    {
                        process.WaitForExit();
                    }

                    string txtFile = outputBase + ".txt";
                    if (System.IO.File.Exists(txtFile))
                    {
                        textoExtraido = await System.IO.File.ReadAllTextAsync(txtFile);
                        System.IO.File.Delete(txtFile);
                    }
                    if (System.IO.File.Exists(outputBase)) System.IO.File.Delete(outputBase);
                }

                if (System.IO.File.Exists(tempPathWithExt)) System.IO.File.Delete(tempPathWithExt);
                if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath);

                // =========================================================================
                // DEPURACIÓN: Ver qué leyó exactamente Tesseract en la consola de Render
                // =========================================================================
                Console.WriteLine("========== TEXTO EXTRAÍDO POR OCR ==========");
                Console.WriteLine(textoExtraido);
                Console.WriteLine("============================================");

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

                    bool marcoEntrada = Regex.IsMatch(textoExtraido, @"[xX][^a-zA-Z0-9]{0,4}No marc[óo]\s+entrada", RegexOptions.IgnoreCase);
                    bool marcoSalida = Regex.IsMatch(textoExtraido, @"[xX][^a-zA-Z0-9]{0,4}No marc[óo]\s+salida", RegexOptions.IgnoreCase);
                    bool marcoIncompleta = Regex.IsMatch(textoExtraido, @"[xX][^a-zA-Z0-9]{0,4}Marcaci[óo]n\s+incompleta", RegexOptions.IgnoreCase);
                    bool marcoOtro = Regex.IsMatch(textoExtraido, @"[xX][^a-zA-Z0-9]{0,4}Otro", RegexOptions.IgnoreCase);

                    if (marcoEntrada) tipoIncidenciaStr = "ENTRADA";
                    else if (marcoSalida) tipoIncidenciaStr = "SALIDA";
                    else if (marcoIncompleta) tipoIncidenciaStr = "NO MARCADA";
                    else if (marcoOtro) tipoIncidenciaStr = "OTRO";

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
            catch (Exception ex)
            {
                if (System.IO.File.Exists(tempPathWithExt)) System.IO.File.Delete(tempPathWithExt);
                if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath);

                Console.WriteLine("================ EXCEPCIÓN OCR CLI CRÍTICA ================");
                Console.WriteLine(ex.ToString());
                Console.WriteLine("===========================================================");

                return StatusCode(500, $"Error OCR Interno: {ex.Message}");
            }
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
                idUsuarioLogueado = 1;
            }

            var usuario = await _context.Usuarios
                .Include(u => u.Personal)
                .FirstOrDefaultAsync(u => u.Id == idUsuarioLogueado);

            if (usuario == null || string.IsNullOrEmpty(usuario.ClaveDinamica))
            {
                return Json(new { claveDinamica = "CARGANDO", segundosRestantes = 60 });
            }

            int segundosRestantes = 60 - DateTime.Now.Second;

            return Json(new
            {
                claveDinamica = usuario.ClaveDinamica,
                segundosRestantes = segundosRestantes
            });
        }
    }
}