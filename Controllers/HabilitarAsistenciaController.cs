using control_asistencia.Data;
using control_asistencia.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace control_asistencia.Controllers
{
    public class HabilitarAsistenciaController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HabilitarAsistenciaController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =======================================================
        // ACCIÓN: GENERAR ASISTENCIA MASIVA POR RANGO (FECHA INICIO - FECHA SALIDA)
        // =======================================================
        [HttpPost]
        public async Task<IActionResult> GenerarAsistenciaMasiva(DateTime fechaInicio, DateTime fechaSalida, TimeSpan horaEntrada, TimeSpan horaSalida)
        {
            // Evitamos usar Session directamente si no está configurada, asignando un ID por defecto seguro (ej: 3)
            int idAdminSession = 3;

            if (fechaSalida < fechaInicio)
            {
                fechaSalida = fechaInicio; // Validación de seguridad para que la salida no sea menor al inicio
            }

            // Calculamos la cantidad de días entre la fecha de inicio y la fecha de salida
            int cantidadDias = (fechaSalida.Date - fechaInicio.Date).Days + 1;

            for (int i = 0; i < cantidadDias; i++)
            {
                DateTime fechaCalculada = fechaInicio.AddDays(i);

                // Validamos que no exista ya un registro para esa misma fecha (evita duplicados)
                bool existe = await _context.habilitar_asistencia
                    .AnyAsync(h => h.Fecha.Date == fechaCalculada.Date);

                if (!existe)
                {
                    var nuevaJornada = new habilitar_asistencia
                    {
                        AbiertoPor = idAdminSession,
                        Fecha = fechaCalculada.Date,
                        HoraEntrada = horaEntrada,
                        HoraSalida = horaSalida,
                        CreatAt = DateTime.Now
                    };

                    _context.habilitar_asistencia.Add(nuevaJornada);
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Index", "Administrador");
        }

        // =======================================================
        // ENDPOINT JSON: OBTENER HABILITACIONES ACTIVAS (HISTORIAL)
        // =======================================================
        [HttpGet]
        public async Task<IActionResult> ObtenerHabilitacionesDinamico()
        {
            var lista = await _context.habilitar_asistencia
                .Include(h => h.UsuarioAdministrador)
                    .ThenInclude(u => u.Personal)
                .OrderByDescending(h => h.Fecha)
                .ToListAsync();

            var resultado = lista.Select(h => new {
                id = h.Id,
                fecha = h.Fecha.ToString("dd/MM/yyyy"),
                horaEntrada = h.HoraEntrada.ToString(@"hh\:mm"),
                horaSalida = h.HoraSalida.ToString(@"hh\:mm"),
                abiertoPor = h.UsuarioAdministrador?.Personal != null
                    ? $"{h.UsuarioAdministrador.Personal.Nombre} {h.UsuarioAdministrador.Personal.Apellido}"
                    : "Administrador Sistema"
            });

            return Json(resultado);
        }

        // =======================================================
        // ACCIÓN: ELIMINAR / DESACTIVAR UNA HABILITACIÓN
        // =======================================================



        // =======================================================
        // ACCIÓN: DESACTIVAR HABILITACIÓN Y SUS DEPENDIENTES (ASISTENCIAS)
        // =======================================================
        [HttpPost]
        public async Task<IActionResult> EliminarHabilitacion(int id)
        {
            var jornada = await _context.habilitar_asistencia.FindAsync(id);
            if (jornada != null)
            {
                // Buscamos registros de asistencia dependientes ligados a esta misma fecha
                var asistenciasDependientes = await _context.Asistencia
                    .Where(a => a.CreateAt.Date == jornada.Fecha.Date)
                    .ToListAsync();

                // Si manejas un estado lógico (ej: off / inasistencia / eliminación), 
                // puedes removerlos o actualizar sus estados dependientes:
                if (asistenciasDependientes.Any())
                {
                    _context.Asistencia.RemoveRange(asistenciasDependientes);
                }

                // Eliminamos finalmente la habilitación maestra
                _context.habilitar_asistencia.Remove(jornada);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("Index", "Administrador");
        }




    }
}