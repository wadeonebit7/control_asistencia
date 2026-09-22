using control_asistencia.Data;
using control_asistencia.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

namespace control_asistencia.Controllers
{
    [Authorize(Roles = "EMPLEADO")]
    public class SolicitudesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        public SolicitudesController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CrearSolicitud(
            string tipoSolicitud, IFormFile documentoRespaldo, string motivo,

            // Parámetros Licencia Médica
            string folio, string profesional, string fechaOtorgamiento, string fechaInicio, int? diasReposo, string tipificacion,

            // Parámetros Ajuste Asistencia
            string fechaAfectada, string tipoIncidencia, string horaEntrada, string horaSalida, string emitidoPor)
        {
            // 1. Identificar Usuario
            var claimId = User.FindFirst(ClaimTypes.NameIdentifier);
            if (claimId == null) return RedirectToAction("Logout", "Auth");
            int idUsuarioLogueado = int.Parse(claimId.Value);

            // =========================================================================
            // 2. GUARDAR DOCUMENTO (CON CLASIFICACIÓN DINÁMICA DE CARPETAS)
            // =========================================================================
            string rutaGuardada = "";
            if (documentoRespaldo != null && documentoRespaldo.Length > 0)
            {
                // Determinamos el nombre de la subcarpeta según el tipo de solicitud
                string subCarpeta = tipoSolicitud == "LICENCIA_MEDICA" ? "LicenciaMedica" : "PermisoAdministrativo";

                // Armamos la ruta física incluyendo la subcarpeta
                string carpetaDestino = Path.Combine(_env.WebRootPath, "documentos_respaldo", subCarpeta);
                if (!Directory.Exists(carpetaDestino)) Directory.CreateDirectory(carpetaDestino);

                string nombreArchivo = Guid.NewGuid().ToString() + Path.GetExtension(documentoRespaldo.FileName);
                string rutaCompleta = Path.Combine(carpetaDestino, nombreArchivo);

                using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                {
                    await documentoRespaldo.CopyToAsync(stream);
                }

                // Guardamos la ruta relativa exacta en la base de datos para que luego pueda ser leída en la web
                rutaGuardada = $"/documentos_respaldo/{subCarpeta}/" + nombreArchivo;
            }

            // =========================================================================
            // 3. Crear Solicitud Padre
            // =========================================================================
            var nuevaSolicitud = new Solicitudes
            {
                IdUsuario = idUsuarioLogueado,
                TipoSolicitud = tipoSolicitud,
                Motivo = motivo,
                RutaDocumento = rutaGuardada, // Ahora guardará "/documentos_respaldo/LicenciaMedica/archivo.pdf" etc.
                EstadoSolicitud = "PENDIENTE",
                Estado = true,
                CreateAt = DateTime.Now
            };

            _context.Add(nuevaSolicitud);
            await _context.SaveChangesAsync();

            // =========================================================================
            // 4. BIFURCACIÓN DE TABLAS HIJAS
            // =========================================================================
            if (tipoSolicitud == "LICENCIA_MEDICA")
            {
                if (!DateTime.TryParseExact(fechaInicio, "dd-MM-yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime dtInicio) ||
                    !DateTime.TryParseExact(fechaOtorgamiento, "dd-MM-yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime dtOtor))
                {
                    TempData["Error"] = "Formato de fecha inválido en la Licencia Médica.";
                    return RedirectToAction("Index", "Trabajador");
                }

                int dias = diasReposo ?? 0;
                var nuevaLicencia = new LicenciaMedica
                {
                    IdSolicitudes = nuevaSolicitud.Id,
                    Folio = folio,
                    Profesional = profesional,
                    FechaOtorgamiento = dtOtor,
                    FechaInicio = dtInicio,
                    FechaTermino = dtInicio.AddDays(dias - 1),
                    Tipificacion = tipificacion
                };
                _context.Add(nuevaLicencia);
                await _context.SaveChangesAsync();
            }
            else if (tipoSolicitud == "AJUSTE_ASISTENCIA")
            {
                // Validación segura de Fecha y Horas
                if (!DateTime.TryParseExact(fechaAfectada, "dd-MM-yyyy", null, System.Globalization.DateTimeStyles.None, out DateTime dtAfectada))
                {
                    TempData["Error"] = "La fecha del incidente no es válida.";
                    return RedirectToAction("Index", "Trabajador");
                }

                if (!TimeSpan.TryParse(horaEntrada, out TimeSpan tsEntrada) ||
                    !TimeSpan.TryParse(horaSalida, out TimeSpan tsSalida))
                {
                    TempData["Error"] = "Las horas de entrada o salida no tienen un formato válido (HH:MM).";
                    return RedirectToAction("Index", "Trabajador");
                }

                var nuevoAjuste = new AjusteAsistencia
                {
                    IdSolicitudes = nuevaSolicitud.Id,
                    FechaAfectada = dtAfectada,
                    TipoIncidencia = tipoIncidencia ?? "NO MARCADA",
                    HoraEntrada = tsEntrada,
                    HoraSalida = tsSalida,
                    Motivo = motivo,
                    EmitidoPor = emitidoPor
                };
                _context.Add(nuevoAjuste);
                await _context.SaveChangesAsync();
            }

            TempData["Mensaje"] = "Documento enviado a Recursos Humanos exitosamente.";
            return RedirectToAction("Index", "Trabajador");
        }






    }
}